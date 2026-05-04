#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quipu.ParameterizationExtractor.Logic.Connectivity;

namespace Tests.Desktop.Fakes;

/// <summary>
/// Queue-driven test double for <see cref="IConnectionTester"/>. Mirrors the
/// <see cref="FakeDialogService"/> shape — enqueue results / exceptions; assertion
/// is on <see cref="ConnectionStringsCalled"/>.
/// </summary>
internal sealed class FakeConnectionTester : IConnectionTester
{
    private readonly Queue<ConnectionTestResult> _results = new();
    private readonly Queue<Exception> _exceptions = new();

    public List<string> ConnectionStringsCalled { get; } = new();

    public void EnqueueResult(ConnectionTestResult result) => _results.Enqueue(result);

    public void EnqueueException(Exception ex) => _exceptions.Enqueue(ex);

    public Task<ConnectionTestResult> TestAsync(string connectionString, CancellationToken ct = default)
    {
        ConnectionStringsCalled.Add(connectionString);

        if (_exceptions.Count > 0)
        {
            throw _exceptions.Dequeue();
        }

        if (_results.Count == 0)
        {
            throw new InvalidOperationException(
                "FakeConnectionTester: no enqueued result or exception");
        }

        return Task.FromResult(_results.Dequeue());
    }
}
