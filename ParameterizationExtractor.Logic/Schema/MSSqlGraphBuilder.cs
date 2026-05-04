#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Quipu.ParameterizationExtractor.Logic.Schema
{
    public sealed class MSSqlGraphBuilder : IGraphBuilder
    {
        private const int ConnectTimeoutSeconds = 10;
        private const int CommandTimeoutSeconds = 30;

        // Same FK metadata query as ObjectMetaDataProvider.sqlFKs — duplicated as a const here
        // to keep the graph builder self-contained (no IUnitOfWorkFactory dependency, no DI
        // shared state with DependencyBuilder).
        private const string FkSql = @"
SELECT
    fk.name as [Name],
    OBJECT_SCHEMA_NAME(fk.parent_object_id) 'ParentSchema',
    OBJECT_NAME(fk.parent_object_id) 'ParentTable',
    OBJECT_SCHEMA_NAME(fk.referenced_object_id) 'ReferencedSchema',
    OBJECT_NAME(fk.referenced_object_id) 'ReferencedTable'
FROM sys.foreign_keys fk";

        private readonly ILogger<MSSqlGraphBuilder> _log;

        public MSSqlGraphBuilder(ILogger<MSSqlGraphBuilder> log)
        {
            _log = log;
        }

        public async Task<ReachableGraph> BuildAsync(
            string connectionString,
            IReadOnlyList<TableRef> seedTables,
            CancellationToken ct = default)
        {
            if (seedTables is null) throw new ArgumentNullException(nameof(seedTables));

            if (seedTables.Count == 0)
            {
                return new ReachableGraph(Array.Empty<GraphNode>(), Array.Empty<GraphEdge>());
            }

            var connStr = BuildConnectionString(connectionString);

            try
            {
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(ct).ConfigureAwait(false);

                var fks = await LoadFksAsync(conn, ct).ConfigureAwait(false);
                var graph = Walk(fks, seedTables);
                _log.LogInformation(
                    "Graph build from {SeedCount} seed(s) → {TotalFks} total FKs in DB → reached {Nodes} nodes, {Edges} edges",
                    seedTables.Count, fks.Count, graph.Nodes.Count, graph.Edges.Count);
                return graph;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SqlException ex)
            {
                _log.LogWarning(ex, "BuildAsync (graph) failed");
                throw new DatabaseExplorerException("Failed to build graph: " + ex.Message, ex);
            }
            catch (InvalidOperationException ex)
            {
                _log.LogWarning(ex, "BuildAsync (graph) rejected configuration");
                throw new DatabaseExplorerException("Failed to build graph: " + ex.Message, ex);
            }
            catch (ArgumentException ex)
            {
                _log.LogWarning(ex, "BuildAsync (graph) received bad argument");
                throw new DatabaseExplorerException("Failed to build graph: " + ex.Message, ex);
            }
        }

        private static async Task<IReadOnlyList<RawFk>> LoadFksAsync(SqlConnection conn, CancellationToken ct)
        {
            await using var cmd = new SqlCommand(FkSql, conn) { CommandTimeout = CommandTimeoutSeconds };
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var result = new List<RawFk>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                result.Add(new RawFk(
                    Name: reader.GetString(0),
                    ParentSchema: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    ParentTable: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    ReferencedSchema: reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ReferencedTable: reader.IsDBNull(4) ? string.Empty : reader.GetString(4)));
            }
            return result;
        }

        private static ReachableGraph Walk(IReadOnlyList<RawFk> fks, IReadOnlyList<TableRef> seeds)
        {
            var cmp = SchemaNameComparer.Instance;

            // Adjacency lists: for each (schema,name), the FKs touching it (both directions).
            var byEndpoint = new Dictionary<(string Schema, string Name), List<RawFk>>(cmp);
            foreach (var fk in fks)
            {
                AddEndpoint(byEndpoint, (fk.ParentSchema, fk.ParentTable), fk);
                AddEndpoint(byEndpoint, (fk.ReferencedSchema, fk.ReferencedTable), fk);
            }

            var visited = new HashSet<(string Schema, string Name)>(cmp);
            var visitedEdges = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<(string Schema, string Name)>();

            foreach (var seed in seeds)
            {
                var key = (seed.Schema, seed.Name);
                if (visited.Add(key)) queue.Enqueue(key);
            }

            var edges = new List<GraphEdge>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!byEndpoint.TryGetValue(current, out var touching)) continue;

                foreach (var fk in touching)
                {
                    if (!visitedEdges.Add(fk.Name)) continue;

                    edges.Add(new GraphEdge(
                        fk.ParentSchema, fk.ParentTable,
                        fk.ReferencedSchema, fk.ReferencedTable,
                        fk.Name));

                    var other = cmp.Equals(current, (fk.ParentSchema, fk.ParentTable))
                        ? (fk.ReferencedSchema, fk.ReferencedTable)
                        : (fk.ParentSchema, fk.ParentTable);

                    if (visited.Add(other)) queue.Enqueue(other);
                }
            }

            var nodes = visited
                .Select(v => new GraphNode(v.Schema, v.Name))
                .OrderBy(n => n.Schema, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            edges.Sort((a, b) =>
            {
                var c = StringComparer.OrdinalIgnoreCase.Compare(a.FromSchema, b.FromSchema);
                if (c != 0) return c;
                c = StringComparer.OrdinalIgnoreCase.Compare(a.FromName, b.FromName);
                if (c != 0) return c;
                c = StringComparer.OrdinalIgnoreCase.Compare(a.ToSchema, b.ToSchema);
                if (c != 0) return c;
                c = StringComparer.OrdinalIgnoreCase.Compare(a.ToName, b.ToName);
                if (c != 0) return c;
                return StringComparer.Ordinal.Compare(a.ConstraintName, b.ConstraintName);
            });

            return new ReachableGraph(nodes, edges);
        }

        private static void AddEndpoint(
            Dictionary<(string Schema, string Name), List<RawFk>> map,
            (string Schema, string Name) key,
            RawFk fk)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<RawFk>();
                map[key] = list;
            }
            list.Add(fk);
        }

        private static string BuildConnectionString(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = ConnectTimeoutSeconds,
                };
                return builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                throw new DatabaseExplorerException(
                    "BuildAsync (graph) received an invalid connection string: " + ex.Message, ex);
            }
            catch (KeyNotFoundException ex)
            {
                throw new DatabaseExplorerException(
                    "BuildAsync (graph) received an invalid connection string: " + ex.Message, ex);
            }
            catch (FormatException ex)
            {
                throw new DatabaseExplorerException(
                    "BuildAsync (graph) received an invalid connection string: " + ex.Message, ex);
            }
        }

        private sealed record RawFk(
            string Name,
            string ParentSchema, string ParentTable,
            string ReferencedSchema, string ReferencedTable);

        private sealed class SchemaNameComparer : IEqualityComparer<(string Schema, string Name)>
        {
            public static readonly SchemaNameComparer Instance = new();
            public bool Equals((string Schema, string Name) x, (string Schema, string Name) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.Schema, y.Schema) &&
                StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);
            public int GetHashCode((string Schema, string Name) obj) =>
                HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Schema ?? string.Empty),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty));
        }
    }
}
