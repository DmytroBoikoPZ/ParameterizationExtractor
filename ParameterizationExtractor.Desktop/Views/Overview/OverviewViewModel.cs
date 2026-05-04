using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;

namespace Quipu.ParameterizationExtractor.Desktop.Views.Overview;

/// <summary>
/// View-model for the Overview tab. Bound to the workspace summary fields shown in mockup
/// <c>docs/design/desktop-ui/03-shell.md</c>. Most fields are placeholders until later
/// features add the underlying state (per-node decisions, dry-run history, extras).
/// </summary>
internal sealed partial class OverviewViewModel : ObservableObject
{
    private const string NoWorkspace = "<no workspace>";
    private const string EmptyStateText =
        "Pass a .bws path on the command line to load a workspace. " +
        "(Welcome view lands in desktop-startup-and-open-workspace.)";

    [ObservableProperty] private string _workspaceName = NoWorkspace;
    [ObservableProperty] private string _sourceSummary = NoWorkspace;
    [ObservableProperty] private string _seedSummary = "<not yet configured>";
    [ObservableProperty] private string _pathSummary = NoWorkspace;
    [ObservableProperty] private string _extrasSummary = "0 standalone tables · 0 scripts";
    [ObservableProperty] private string _lastDryRunSummary = "never";
    [ObservableProperty] private string? _pendingWarning;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _emptyStateMessage = EmptyStateText;

    public void Show(WorkspaceModel? workspace)
    {
        if (workspace is null)
        {
            WorkspaceName = NoWorkspace;
            SourceSummary = NoWorkspace;
            SeedSummary = "<not yet configured>";
            PathSummary = NoWorkspace;
            ExtrasSummary = "0 standalone tables · 0 scripts";
            LastDryRunSummary = "never";
            PendingWarning = null;
            IsEmpty = true;
            EmptyStateMessage = EmptyStateText;
            return;
        }

        WorkspaceName = workspace.Name;
        SourceSummary = $"{workspace.Source.Database} @ {workspace.Source.Server}";
        SeedSummary = "<not yet configured>";

        var tableCount = workspace.Package.Scripts
            .SelectMany(s => s.TablesToProcess ?? new System.Collections.Generic.List<Quipu.ParameterizationExtractor.Logic.Model.TableToExtract>())
            .Count();
        PathSummary = $"{tableCount} tables in package";

        ExtrasSummary = "0 standalone tables · 0 scripts";
        LastDryRunSummary = "never";
        PendingWarning = null;
        IsEmpty = false;
        EmptyStateMessage = string.Empty;
    }
}
