#nullable enable
using System;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Desktop.Services.Threading;

/// <summary>
/// Marshals work onto the WPF UI thread. The single seam ViewModels use to satisfy
/// the "no <c>Dispatcher.Invoke</c> in VMs" tripwire (see <c>docs/methodology/wpf-desktop.md</c>).
/// </summary>
internal interface IUiDispatcher
{
    /// <summary>True when the calling thread is the UI thread.</summary>
    bool CheckAccess();

    /// <summary>Marshal an action onto the UI thread. Awaits its completion.</summary>
    Task InvokeAsync(Action action);

    /// <summary>Marshal a function onto the UI thread. Awaits its result.</summary>
    Task<T> InvokeAsync<T>(Func<T> func);
}
