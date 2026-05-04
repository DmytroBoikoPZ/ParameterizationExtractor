#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Logic.Connectivity
{
    /// <summary>
    /// Tests a SQL Server connection without exposing ADO.NET types to callers.
    /// Used by the Desktop's connection-editor UI; engine-internal so the Desktop
    /// preserves the "no <c>SqlConnection</c> outside Logic" tripwire.
    /// </summary>
    public interface IConnectionTester
    {
        /// <summary>
        /// Opens the connection, counts user tables and foreign keys, and returns the result.
        /// Catches expected ADO.NET / argument failures and packages them into the result;
        /// <see cref="System.OperationCanceledException"/> propagates so callers can distinguish
        /// cancellation from logical failure.
        /// </summary>
        Task<ConnectionTestResult> TestAsync(string connectionString, CancellationToken ct = default);
    }

    /// <summary>
    /// Outcome of <see cref="IConnectionTester.TestAsync"/>. On success,
    /// <see cref="TableCount"/> and <see cref="FkCount"/> are populated and
    /// <see cref="ErrorMessage"/> is null. On failure, the counts are zero and
    /// <see cref="ErrorMessage"/> carries the underlying exception's message.
    /// </summary>
    public sealed record ConnectionTestResult(
        bool Success,
        int TableCount,
        int FkCount,
        string? ErrorMessage);
}
