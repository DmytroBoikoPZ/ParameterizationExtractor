using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Desktop.Controls.StrategyPicker;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Quipu.ParameterizationExtractor.Desktop.Views.Graph;

internal sealed partial class NodeInspectorViewModel : ObservableObject
{
    private readonly Func<Task> _saveCallback;
    private readonly Func<Task> _recomputeGraphCallback;
    private readonly ILogger<NodeInspectorViewModel> _log;
    private readonly TimeSpan _saveDebounce;

    private WorkspaceModel? _workspace;
    private SourceForScript? _currentScript;
    private bool _loading;
    private CancellationTokenSource? _saveCts;

    public NodeInspectorViewModel(
        Func<Task> saveCallback,
        Func<Task> recomputeGraphCallback,
        ILogger<NodeInspectorViewModel> log,
        TimeSpan? saveDebounce = null)
    {
        _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
        _recomputeGraphCallback = recomputeGraphCallback ?? throw new ArgumentNullException(nameof(recomputeGraphCallback));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _saveDebounce = saveDebounce ?? TimeSpan.FromMilliseconds(500);
    }

    [ObservableProperty]
    private GraphNodeViewModel? _node;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddToExtractCommand))]
    [NotifyPropertyChangedFor(nameof(IsAddable))]
    private TableToExtract? _source;

    [ObservableProperty]
    private StrategyKind _strategyChoice = StrategyKind.FKDependency;

    [ObservableProperty]
    private string _where = string.Empty;

    [ObservableProperty]
    private bool _excluded;

    public bool IsAddable => Source is null;

    public void Open(GraphNodeViewModel node, WorkspaceModel workspace, SourceForScript currentScript)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _currentScript = currentScript ?? throw new ArgumentNullException(nameof(currentScript));

        Source = currentScript.TablesToProcess?
            .FirstOrDefault(t =>
                (string.IsNullOrEmpty(t.Schema) || string.Equals(t.Schema, node.Schema, StringComparison.OrdinalIgnoreCase))
                && string.Equals(t.TableName, node.Name, StringComparison.OrdinalIgnoreCase));

        HydrateFromSource();
    }

    private void HydrateFromSource()
    {
        _loading = true;
        try
        {
            if (Source is null)
            {
                StrategyChoice = StrategyKind.FKDependency;
                Where = string.Empty;
                Excluded = false;
                return;
            }
            StrategyChoice = ToStrategyKind(Source.ExtractStrategy);
            Where = Source.ExtractStrategy?.Where ?? string.Empty;
            Excluded = Source.Excluded;
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnStrategyChoiceChanged(StrategyKind value) => OnAnyEditableChanged(replaceStrategy: true);
    partial void OnWhereChanged(string value) => OnAnyEditableChanged();
    partial void OnExcludedChanged(bool value) => OnAnyEditableChanged();

    private void OnAnyEditableChanged(bool replaceStrategy = false)
    {
        if (_loading || Source is null) return;

        if (replaceStrategy)
        {
            Source.ExtractStrategy = StrategyChoice switch
            {
                StrategyKind.FKDependency => new FKDependencyExtractStrategy { Where = Where },
                StrategyKind.OnlyChildren => new OnlyChildrenExtractStrategy { Where = Where },
                StrategyKind.OnlyParent => new OnlyParentExtractStrategy { Where = Where },
                StrategyKind.OnlyOneTable => new OnlyOneTableExtractStrategy { Where = Where },
                _ => Source.ExtractStrategy,
            };
        }
        else if (Source.ExtractStrategy is { } s)
        {
            s.Where = Where;
        }

        Source.Excluded = Excluded;

        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        _ = SaveDebouncedAsync(_saveCts.Token);
    }

    private async Task SaveDebouncedAsync(CancellationToken ct)
    {
        try
        {
            if (_saveDebounce > TimeSpan.Zero)
            {
                await Task.Delay(_saveDebounce, ct).ConfigureAwait(true);
            }
            if (ct.IsCancellationRequested) return;
            await _saveCallback().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer edit supersedes this debounce.
        }
    }

    [RelayCommand(CanExecute = nameof(IsAddable))]
    private async Task AddToExtractAsync()
    {
        if (_workspace is null || _currentScript is null || Node is null) return;

        var entry = new TableToExtract(Node.Name, new FKDependencyExtractStrategy(), new SqlBuildStrategy())
        {
            Schema = Node.Schema,
        };
        _currentScript.TablesToProcess.Add(entry);
        Source = entry;

        await _saveCallback().ConfigureAwait(true);
        await _recomputeGraphCallback().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RecomputeGraphAsync() => await _recomputeGraphCallback().ConfigureAwait(true);

    private static StrategyKind ToStrategyKind(ExtractStrategy? s) => s switch
    {
        FKDependencyExtractStrategy _ => StrategyKind.FKDependency,
        OnlyChildrenExtractStrategy _ => StrategyKind.OnlyChildren,
        OnlyParentExtractStrategy _ => StrategyKind.OnlyParent,
        OnlyOneTableExtractStrategy _ => StrategyKind.OnlyOneTable,
        _ => StrategyKind.FKDependency,
    };
}
