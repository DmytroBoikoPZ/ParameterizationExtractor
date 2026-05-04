#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Quipu.ParameterizationExtractor.Logic.Schema
{
    public sealed class MSSqlDatabaseExplorer : IDatabaseExplorer
    {
        private const int ConnectTimeoutSeconds = 10;
        private const int CommandTimeoutSeconds = 30;

        private const string ListTablesSql =
            "SELECT s.name AS schema_name, t.name AS table_name " +
            "FROM sys.tables t " +
            "INNER JOIN sys.schemas s ON s.schema_id = t.schema_id " +
            "WHERE t.is_ms_shipped = 0 " +
            "ORDER BY s.name, t.name";

        private readonly ILogger<MSSqlDatabaseExplorer> _log;

        public MSSqlDatabaseExplorer(ILogger<MSSqlDatabaseExplorer> log)
        {
            _log = log;
        }

        public async Task<IReadOnlyList<TableRef>> ListTablesAsync(string connectionString, CancellationToken ct = default)
        {
            var connStr = BuildConnectionString(connectionString, "ListTablesAsync");

            try
            {
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(ct).ConfigureAwait(false);

                await using var cmd = new SqlCommand(ListTablesSql, conn) { CommandTimeout = CommandTimeoutSeconds };
                await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

                var result = new List<TableRef>();
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var schema = reader.GetString(0);
                    var name = reader.GetString(1);
                    result.Add(new TableRef(schema, name));
                }

                _log.LogInformation("Listed {Count} tables", result.Count);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SqlException ex)
            {
                _log.LogWarning(ex, "ListTablesAsync failed");
                throw new DatabaseExplorerException("Failed to list tables: " + ex.Message, ex);
            }
            catch (InvalidOperationException ex)
            {
                _log.LogWarning(ex, "ListTablesAsync rejected configuration");
                throw new DatabaseExplorerException("Failed to list tables: " + ex.Message, ex);
            }
            catch (ArgumentException ex)
            {
                _log.LogWarning(ex, "ListTablesAsync received bad argument");
                throw new DatabaseExplorerException("Failed to list tables: " + ex.Message, ex);
            }
        }

        public async Task<PreviewResult> PreviewQueryAsync(
            string connectionString,
            string sql,
            int maxRows,
            CancellationToken ct = default)
        {
            if (sql is null) throw new ArgumentNullException(nameof(sql));
            if (maxRows < 0) throw new ArgumentOutOfRangeException(nameof(maxRows));

            var connStr = BuildConnectionString(connectionString, "PreviewQueryAsync");

            try
            {
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(ct).ConfigureAwait(false);

                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = CommandTimeoutSeconds };
                await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

                var fieldCount = reader.FieldCount;
                var columns = new string[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    columns[i] = reader.GetName(i);
                }

                var rows = new List<string?[]>(Math.Min(maxRows, 256));
                while (rows.Count < maxRows && await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var row = new string?[fieldCount];
                    for (var i = 0; i < fieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        row[i] = (value is null || value is DBNull)
                            ? null
                            : Convert.ToString(value, CultureInfo.InvariantCulture);
                    }
                    rows.Add(row);
                }

                // Probe for one extra row to detect truncation without materialising it.
                var truncated = rows.Count == maxRows && await reader.ReadAsync(ct).ConfigureAwait(false);

                return new PreviewResult(columns, rows, truncated);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SqlException ex)
            {
                _log.LogWarning(ex, "PreviewQueryAsync failed");
                throw new DatabaseExplorerException("Preview query failed: " + ex.Message, ex);
            }
            catch (InvalidOperationException ex)
            {
                _log.LogWarning(ex, "PreviewQueryAsync rejected configuration");
                throw new DatabaseExplorerException("Preview query failed: " + ex.Message, ex);
            }
            catch (ArgumentException ex)
            {
                _log.LogWarning(ex, "PreviewQueryAsync received bad argument");
                throw new DatabaseExplorerException("Preview query failed: " + ex.Message, ex);
            }
        }

        private static string BuildConnectionString(string connectionString, string opName)
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
                    opName + " received an invalid connection string: " + ex.Message, ex);
            }
            catch (KeyNotFoundException ex)
            {
                throw new DatabaseExplorerException(
                    opName + " received an invalid connection string: " + ex.Message, ex);
            }
            catch (FormatException ex)
            {
                throw new DatabaseExplorerException(
                    opName + " received an invalid connection string: " + ex.Message, ex);
            }
        }
    }
}
