using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;
using Quipu.ParameterizationExtractor.Desktop.Services.Threading;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Quipu.ParameterizationExtractor.Desktop.Views.Extras;

/// <summary>
/// Singleton VM for the Extras tab. Edits <c>TablesToProcess</c> entries that are NOT
/// FK-reachable from the seed (the standalone-tables half of M7). Filters the live
/// <see cref="GraphViewModel.AllNodes"/> set; subscribes to its <c>INotifyCollectionChanged</c>
/// for re-filter on graph hydrate. Anchor follows <c>Seed.SelectedScript</c>.
/// </summary>
internal sealed partial class ExtrasViewModel : ObservableObject
{
    private const string EmptyHintText =
        "No standalone tables yet — everything in this script's TablesToProcess is reachable from the seed via FKs.";

    private readonly GraphViewModel _graph;
    private readonly IDatabaseExplorer _explorer;
    private readonly IDialogService _dialog;
    private readonly IUiDispatcher _ui;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ExtrasViewModel> _log;

    private WorkspaceModel? _workspace;
    private string? _connectionString;
    private Func<Task>? _saveCallback;

    public ExtrasViewModel(
        GraphViewModel graph,
        IDatabaseExplorer explorer,
        IDialogService dialog,
        IUiDispatcher ui,
        ILoggerFactory loggerFactory)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _explorer = explorer ?? throw new ArgumentNullException(nameof(explorer));
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _log = loggerFactory.CreateLogger<ExtrasViewModel>();

        _graph.AllNodes.CollectionChanged += OnGraphAllNodesChanged;
    }

    public ObservableCollection<StandaloneTableViewModel> StandaloneTables { get; } = new();

    /// <summary>Available tables for the Add dialog. Mirrors <c>SeedViewModel.Tables</c>.</summary>
    public ObservableCollection<TableRef> Tables { get; } = new();

    [ObservableProperty]
    private SourceForScript? _anchorScript;

    [ObservableProperty]
    private string _emptyHint = string.Empty;

    public void Bind(Func<Task> saveCallback) =>
        _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));

    public void Hydrate(WorkspaceModel? workspace, string? plaintextPassword)
    {
        StandaloneTables.Clear();
        Tables.Clear();
        AnchorScript = null;
        EmptyHint = string.Empty;

        if (workspace is null)
        {
            _workspace = null;
            _connectionString = null;
            AddStandaloneTableCommand.NotifyCanExecuteChanged();
            return;
        }

        _workspace = workspace;
        _connectionString = WorkspaceConnectionStringBuilder.Build(workspace.Source, plaintextPassword);

        RecomputeStandalone();
        AddStandaloneTableCommand.NotifyCanExecuteChanged();

        _ = RefreshTablesAsync();
    }

    public void SetAnchor(SourceForScript? script)
    {
        AnchorScript = script;
        RecomputeStandalone();
        AddStandaloneTableCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshTablesAsync()
    {
        if (string.IsNullOrEmpty(_connectionString)) return;
        try
        {
            var refs = await _explorer.ListTablesAsync(_connectionString).ConfigureAwait(true);
            await _ui.InvokeAsync(() =>
            {
                Tables.Clear();
                foreach (var t in refs) Tables.Add(t);
            });
        }
        catch (DatabaseExplorerException ex)
        {
            _log.LogWarning(ex, "Extras tables refresh failed");
        }
    }

    private void OnGraphAllNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Marshal to UI thread; ObservableCollection mutations from the graph hydrate may
        // already be on the UI thread, but the contract is to never assume.
        _ = _ui.InvokeAsync(RecomputeStandalone);
    }

    private void RecomputeStandalone()
    {
        var script = AnchorScript ?? _workspace?.Package?.Scripts?.FirstOrDefault();
        var all = script?.TablesToProcess;

        StandaloneTables.Clear();

        if (all is null || all.Count == 0)
        {
            EmptyHint = _workspace is null ? string.Empty : EmptyHintText;
            return;
        }

        var reachableKeys = _graph.AllNodes
            .Select(n => (Schema: n.Schema ?? string.Empty, Name: n.Name ?? string.Empty))
            .ToHashSet(SchemaNameComparer.Instance);

        // Graceful fallback: while graph is empty (not yet hydrated), show ALL TablesToProcess
        // as standalone — better than confusing emptiness.
        IEnumerable<TableToExtract> visible = reachableKeys.Count == 0
            ? all
            : all.Where(t => !reachableKeys.Contains((t.Schema ?? string.Empty, t.TableName ?? string.Empty)));

        foreach (var t in visible)
        {
            StandaloneTables.Add(new StandaloneTableViewModel(
                t,
                SaveAsync,
                RemoveStandaloneTableAsync,
                _loggerFactory.CreateLogger<StandaloneTableViewModel>()));
        }

        EmptyHint = StandaloneTables.Count == 0 ? EmptyHintText : string.Empty;
    }

    private bool CanAdd() =>
        _workspace is not null
        && (AnchorScript ?? _workspace.Package?.Scripts?.FirstOrDefault()) is not null;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddStandaloneTableAsync()
    {
        if (_workspace is null) return;
        var script = AnchorScript ?? _workspace.Package?.Scripts?.FirstOrDefault();
        if (script is null) return;

        var pick = await _dialog.ShowAddStandaloneTableDialogAsync(Tables).ConfigureAwait(true);
        if (pick is null) return;

        var entry = new TableToExtract(pick.Name, new OnlyOneTableExtractStrategy(), new SqlBuildStrategy())
        {
            Schema = pick.Schema,
        };
        script.TablesToProcess ??= new List<TableToExtract>();
        script.TablesToProcess.Add(entry);

        RecomputeStandalone();
        await SaveAsync().ConfigureAwait(true);
    }

    private async Task RemoveStandaloneTableAsync(StandaloneTableViewModel row)
    {
        if (_workspace is null) return;
        var script = AnchorScript ?? _workspace.Package?.Scripts?.FirstOrDefault();
        if (script?.TablesToProcess is null) return;

        script.TablesToProcess.Remove(row.Source);
        StandaloneTables.Remove(row);
        EmptyHint = StandaloneTables.Count == 0 ? EmptyHintText : string.Empty;

        await SaveAsync().ConfigureAwait(true);
    }

    private Task SaveAsync() => _saveCallback?.Invoke() ?? Task.CompletedTask;

    private sealed class SchemaNameComparer : IEqualityComparer<(string Schema, string Name)>
    {
        public static readonly SchemaNameComparer Instance = new();
        public bool Equals((string Schema, string Name) x, (string Schema, string Name) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Schema, y.Schema) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);
        public int GetHashCode((string Schema, string Name) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Schema ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty));
    }
}
