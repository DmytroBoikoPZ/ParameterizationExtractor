#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Logic.Schema
{
    /// <summary>
    /// Engine-side seam used by the Desktop's Graph tab to compute the FK-reachable subgraph
    /// from a set of seed tables. Mirrors the <c>IDatabaseExplorer</c> pattern: callers hand
    /// a connection string + seed list; the engine opens the <c>SqlConnection</c>.
    /// </summary>
    public interface IGraphBuilder
    {
        /// <summary>
        /// BFS-walks the FK graph from each seed and returns the reachable subgraph.
        /// Both directions are traversed (parent → child AND child → parent). Each edge is
        /// recorded with its actual SQL Server FK direction (FK-holding table → referenced table).
        /// </summary>
        Task<ReachableGraph> BuildAsync(
            string connectionString,
            IReadOnlyList<TableRef> seedTables,
            CancellationToken ct = default);
    }

    public sealed record ReachableGraph(
        IReadOnlyList<GraphNode> Nodes,
        IReadOnlyList<GraphEdge> Edges);

    public sealed record GraphNode(string Schema, string Name);

    public sealed record GraphEdge(
        string FromSchema, string FromName,
        string ToSchema, string ToName,
        string ConstraintName);
}
