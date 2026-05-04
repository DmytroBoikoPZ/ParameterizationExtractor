#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Tests.Desktop.Fakes;

/// <summary>
/// Queue-driven test double for <see cref="IGraphBuilder"/>. Mirrors
/// <see cref="FakeDatabaseExplorer"/>: enqueue results or exceptions per call;
/// assertion is on the recorded call lists.
/// </summary>
internal sealed class FakeGraphBuilder : IGraphBuilder
{
    private readonly Queue<ReachableGraph> _results = new();
    private readonly Queue<Exception> _exceptions = new();

    public List<string> ConnectionStringsCalled { get; } = new();
    public List<IReadOnlyList<TableRef>> SeedsCalled { get; } = new();

    public void EnqueueResult(ReachableGraph graph) => _results.Enqueue(graph);

    public void EnqueueException(Exception ex) => _exceptions.Enqueue(ex);

    public Task<ReachableGraph> BuildAsync(
        string connectionString,
        IReadOnlyList<TableRef> seedTables,
        CancellationToken ct = default)
    {
        ConnectionStringsCalled.Add(connectionString);
        SeedsCalled.Add(seedTables);

        if (_exceptions.Count > 0)
        {
            throw _exceptions.Dequeue();
        }

        if (_results.Count == 0)
        {
            throw new InvalidOperationException("FakeGraphBuilder: no enqueued result or exception");
        }

        return Task.FromResult(_results.Dequeue());
    }
}
