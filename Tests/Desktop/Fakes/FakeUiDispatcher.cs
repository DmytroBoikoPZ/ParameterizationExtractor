#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Quipu.ParameterizationExtractor.Desktop.Services.Threading;

namespace Tests.Desktop.Fakes;

/// <summary>
/// Test fake for <see cref="IUiDispatcher"/> — runs every call inline so tests don't need
/// a real WPF Dispatcher. Records the number of action / func invocations so assertions can
/// verify marshalling actually happened.
/// </summary>
internal sealed class FakeUiDispatcher : IUiDispatcher
{
    private int _actionCalls;
    private int _funcCalls;

    public int ActionInvocations => _actionCalls;
    public int FuncInvocations => _funcCalls;
    public int TotalInvocations => _actionCalls + _funcCalls;

    public bool CheckAccess() => true;

    public Task InvokeAsync(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        Interlocked.Increment(ref _actionCalls);
        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (func is null) throw new ArgumentNullException(nameof(func));
        Interlocked.Increment(ref _funcCalls);
        return Task.FromResult(func());
    }
}
