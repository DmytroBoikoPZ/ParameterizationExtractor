#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Tests.Desktop.Fakes;

/// <summary>
/// Queue-driven test double for <see cref="IDatabaseExplorer"/>. Mirrors
/// <see cref="FakeConnectionTester"/>: enqueue results or exceptions per call;
/// assertion is on the recorded call lists.
/// </summary>
internal sealed class FakeDatabaseExplorer : IDatabaseExplorer
{
    private readonly Queue<IReadOnlyList<TableRef>> _listResults = new();
    private readonly Queue<Exception> _listExceptions = new();
    private readonly Queue<PreviewResult> _previewResults = new();
    private readonly Queue<Exception> _previewExceptions = new();

    public List<string> ListConnectionStringsCalled { get; } = new();

    public sealed record PreviewCall(string ConnectionString, string Sql, int MaxRows);

    public List<PreviewCall> PreviewCalls { get; } = new();

    public void EnqueueListResult(IReadOnlyList<TableRef> tables) => _listResults.Enqueue(tables);

    public void EnqueueListException(Exception ex) => _listExceptions.Enqueue(ex);

    public void EnqueuePreviewResult(PreviewResult result) => _previewResults.Enqueue(result);

    public void EnqueuePreviewException(Exception ex) => _previewExceptions.Enqueue(ex);

    public Task<IReadOnlyList<TableRef>> ListTablesAsync(string connectionString, CancellationToken ct = default)
    {
        ListConnectionStringsCalled.Add(connectionString);

        if (_listExceptions.Count > 0)
        {
            throw _listExceptions.Dequeue();
        }

        if (_listResults.Count == 0)
        {
            throw new InvalidOperationException("FakeDatabaseExplorer: no enqueued list result or exception");
        }

        return Task.FromResult(_listResults.Dequeue());
    }

    public Task<PreviewResult> PreviewQueryAsync(string connectionString, string sql, int maxRows, CancellationToken ct = default)
    {
        PreviewCalls.Add(new PreviewCall(connectionString, sql, maxRows));

        if (_previewExceptions.Count > 0)
        {
            throw _previewExceptions.Dequeue();
        }

        if (_previewResults.Count == 0)
        {
            throw new InvalidOperationException("FakeDatabaseExplorer: no enqueued preview result or exception");
        }

        return Task.FromResult(_previewResults.Dequeue());
    }
}
