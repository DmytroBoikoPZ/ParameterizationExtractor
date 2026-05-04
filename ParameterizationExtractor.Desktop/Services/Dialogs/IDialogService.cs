using System.Collections.Generic;
using System.Threading.Tasks;
using Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;

/// <summary>
/// Abstraction over message popups, OS file pickers, and typed application dialogs.
/// Pre-spec lives in <c>docs/methodology/wpf-desktop.md § Service abstractions</c>;
/// first WPF impl landed by the <c>desktop-startup-and-open-workspace</c> feature.
/// Typed dialog methods (<see cref="ShowNewWorkspaceDialogAsync"/>, <see cref="ShowEditConnectionDialogAsync"/>)
/// landed by <c>desktop-connection-management</c>.
/// </summary>
internal interface IDialogService
{
    Task ShowMessageAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message);

    Task<string?> OpenFileAsync(string title, string filter);

    Task<string?> SaveFileAsync(string title, string filter, string? defaultName = null);

    /// <summary>Modal New-workspace dialog. Returns the path of the freshly-created <c>.bws</c>, or <c>null</c> on cancel.</summary>
    Task<NewWorkspaceResult?> ShowNewWorkspaceDialogAsync();

    /// <summary>
    /// Modal Edit-connection dialog (re-uses the New-workspace UI in edit mode).
    /// Returns the updated <see cref="WorkspaceSource"/>, or <c>null</c> on cancel.
    /// </summary>
    Task<WorkspaceSource?> ShowEditConnectionDialogAsync(WorkspaceSource current, string? plaintextPassword);

    /// <summary>
    /// Modal Add-standalone-table dialog. Presents the supplied table list (typically the
    /// workspace's discovered tables); returns the operator's pick or <c>null</c> on cancel.
    /// Landed by <c>desktop-extras-tab</c>.
    /// </summary>
    Task<TableRef?> ShowAddStandaloneTableDialogAsync(IReadOnlyList<TableRef> available);
}
