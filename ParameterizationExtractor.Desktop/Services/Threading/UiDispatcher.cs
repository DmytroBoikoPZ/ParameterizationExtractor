#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Quipu.ParameterizationExtractor.Desktop.Services.Threading;

internal sealed class UiDispatcher : IUiDispatcher
{
    public bool CheckAccess()
    {
        // Service-level access to Application.Current is acceptable: this seam IS the
        // dispatcher abstraction; ViewModels only see the interface.
        return Application.Current?.Dispatcher?.CheckAccess() ?? true;
    }

    public Task InvokeAsync(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            // No WPF Application present (e.g. unit tests resolving the service through DI),
            // or already on the UI thread — run inline. No extra hop.
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (func is null) throw new ArgumentNullException(nameof(func));

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return Task.FromResult(func());
        }

        return dispatcher.InvokeAsync(func).Task;
    }
}
