#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Logic.Schema
{
    /// <summary>
    /// Engine-side seam used by the Desktop's Seed tab to enumerate user tables and
    /// run bounded preview queries without exposing ADO.NET types to callers.
    /// Mirrors the <c>IConnectionTester</c> pattern: callers hand a connection string
    /// and SQL; the engine opens the <c>SqlConnection</c>.
    /// </summary>
    public interface IDatabaseExplorer
    {
        Task<IReadOnlyList<TableRef>> ListTablesAsync(string connectionString, CancellationToken ct = default);

        Task<PreviewResult> PreviewQueryAsync(string connectionString, string sql, int maxRows, CancellationToken ct = default);
    }

    public sealed record TableRef(string Schema, string Name);

    public sealed record PreviewResult(
        IReadOnlyList<string> ColumnNames,
        IReadOnlyList<string?[]> Rows,
        bool Truncated);

    /// <summary>
    /// Wraps SQL / argument failures from <see cref="IDatabaseExplorer"/> so callers
    /// don't need to reference <c>Microsoft.Data.SqlClient</c>. <see cref="OperationCanceledException"/>
    /// is NOT wrapped — cancellation propagates so callers can distinguish it from logical failure.
    /// </summary>
    public sealed class DatabaseExplorerException : Exception
    {
        public DatabaseExplorerException(string message, Exception? inner = null)
            : base(message, inner)
        {
        }
    }
}
