using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Desktop.Controls.GraphHost;
using Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;
using Quipu.ParameterizationExtractor.Desktop.Services.Threading;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Quipu.ParameterizationExtractor.Desktop.Views.Graph;

internal sealed partial class GraphViewModel : ObservableObject
{
    private readonly IGraphBuilder _builder;
    private readonly IUiDispatcher _ui;
    private readonly IDialogService _dialog;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GraphViewModel> _log;

    private WorkspaceModel? _workspace;
    private string? _connectionString;
    private Func<Task>? _saveCallback;

    public GraphViewModel(
        IGraphBuilder builder,
        IUiDispatcher ui,
        IDialogService dialog,
        ILoggerFactory loggerFactory)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _log = loggerFactory.CreateLogger<GraphViewModel>();
    }

    public void Bind(Func<Task> saveCallback) =>
        _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));

    public ObservableCollection<GraphNodeViewModel> AllNodes { get; } = new();
    public ObservableCollection<GraphEdgeViewModel> AllEdges { get; } = new();
    public ObservableCollection<GraphNodeViewModel> VisibleNodes { get; } = new();
    public ObservableCollection<GraphEdgeViewModel> VisibleEdges { get; } = new();

    [ObservableProperty]
    private FocusMode _focusModeChoice = FocusMode.SeedPlusOneHop;

    private readonly HashSet<string> _expandedNodeIds = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private GraphLayoutKind _layoutChoice = GraphLayoutKind.Hierarchical;

    [ObservableProperty]
    private string? _selectedNodeId;

    [ObservableProperty]
    private GraphNodeViewModel? _inspectedNode;

    [ObservableProperty]
    private NodeInspectorViewModel? _inspectorVm;

    [ObservableProperty]
    private string? _rightClickedNodeId;

    [ObservableProperty]
    private SourceForScript? _anchorScript;

    public void SetAnchor(SourceForScript? script)
    {
        AnchorScript = script;
        _ = RefreshCommand.ExecuteAsync(null);
    }

    [ObservableProperty]
    private ReachableGraph? _reachable;

    [ObservableProperty]
    private string _status = string.Empty;

    public bool IsLoading => RefreshCommand.IsRunning;

    public void Hydrate(WorkspaceModel? workspace, string? plaintextPassword)
    {
        if (RefreshCommand.IsRunning && RefreshCancelCommand.CanExecute(null))
        {
            RefreshCancelCommand.Execute(null);
        }

        AllNodes.Clear();
        AllEdges.Clear();
        VisibleNodes.Clear();
        VisibleEdges.Clear();
        _expandedNodeIds.Clear();
        Reachable = null;
        SelectedNodeId = null;
        AnchorScript = null;

        if (workspace is null)
        {
            _workspace = null;
            _connectionString = null;
            Status = string.Empty;
            return;
        }

        _workspace = workspace;
        _connectionString = WorkspaceConnectionStringBuilder.Build(workspace.Source, plaintextPassword);
        _log.LogInformation(
            "Graph hydrating against {Server}/{Database} as auth={Auth} user={User} hasPassword={HasPassword} hasEncryptedAtRest={HasEncrypted}",
            workspace.Source.Server,
            workspace.Source.Database,
            workspace.Source.Auth,
            workspace.Source.User ?? "(null)",
            !string.IsNullOrEmpty(plaintextPassword),
            !string.IsNullOrEmpty(workspace.Source.PasswordEncrypted));
        _ = RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (_workspace is null || string.IsNullOrEmpty(_connectionString))
        {
            Status = string.Empty;
            return;
        }

        var seeds = CollectSeeds(_workspace, AnchorScript);
        if (seeds.Count == 0)
        {
            AllNodes.Clear();
            AllEdges.Clear();
            VisibleNodes.Clear();
            VisibleEdges.Clear();
            Reachable = null;
            Status = "No seed roots configured";
            return;
        }

        Status = "Loading…";
        try
        {
            var graph = await _builder.BuildAsync(_connectionString, seeds, ct).ConfigureAwait(false);
            await _ui.InvokeAsync(() => MapAndApply(graph, seeds));
        }
        catch (OperationCanceledException)
        {
            await _ui.InvokeAsync(() => Status = "Cancelled");
        }
        catch (DatabaseExplorerException ex)
        {
            _log.LogWarning(ex, "Graph refresh failed");
            await _ui.InvokeAsync(() => Status = $"Failed: {ex.Message}");
        }
    }

    private static IReadOnlyList<TableRef> CollectSeeds(WorkspaceModel workspace, SourceForScript? anchor)
    {
        var script = anchor ?? workspace.Package?.Scripts?.FirstOrDefault();
        var seen = new HashSet<(string Schema, string Name)>(SchemaNameComparer.Instance);
        var result = new List<TableRef>();
        if (script?.RootRecords is null) return result;
        foreach (var root in script.RootRecords)
        {
            var schema = root.Schema ?? string.Empty;
            var name = root.TableName ?? string.Empty;
            if (string.IsNullOrEmpty(name)) continue;
            if (seen.Add((schema, name)))
            {
                result.Add(new TableRef(schema, name));
            }
        }
        return result;
    }

    private void MapAndApply(ReachableGraph graph, IReadOnlyList<TableRef> seeds)
    {
        Reachable = graph;

        // Build a (Schema, Name) → TableToExtract dictionary across all scripts. Last-wins
        // when a table appears in multiple scripts (pragmatic v1; desktop-graph-tab can refine
        // to per-script scoping if it matters).
        var configuredByKey = new Dictionary<(string Schema, string Name), TableToExtract>(SchemaNameComparer.Instance);
        if (_workspace?.Package?.Scripts is not null)
        {
            foreach (var script in _workspace.Package.Scripts)
            {
                if (script.TablesToProcess is null) continue;
                foreach (var t in script.TablesToProcess)
                {
                    var name = t.TableName ?? string.Empty;
                    if (string.IsNullOrEmpty(name)) continue;
                    configuredByKey[(t.Schema ?? string.Empty, name)] = t;
                }
            }
        }

        var seedKeys = new HashSet<(string Schema, string Name)>(
            seeds.Select(s => (s.Schema, s.Name)),
            SchemaNameComparer.Instance);

        var nodeStates = new Dictionary<string, NodeState>(StringComparer.OrdinalIgnoreCase);

        AllNodes.Clear();
        foreach (var n in graph.Nodes)
        {
            var key = (n.Schema, n.Name);
            // Exact (Schema, Name) match first; fall back to bare-name when the operator's
            // config has empty Schema (ADR-011 — empty Schema in operator config matches any
            // discovered Schema for the lookup's purpose).
            TableToExtract? t = null;
            if (configuredByKey.TryGetValue(key, out var exact)) t = exact;
            else if (configuredByKey.TryGetValue((string.Empty, n.Name), out var bare)) t = bare;
            var hasEntry = t is not null;
            var state = !hasEntry ? NodeState.Pending
                      : t!.Excluded ? NodeState.Excluded
                      : NodeState.Configured;
            var chip = hasEntry ? ShortStrategyName(t!.ExtractStrategy) : string.Empty;
            var isSeed = seedKeys.Contains(key);

            var nodeId = $"{n.Schema}.{n.Name}";
            nodeStates[nodeId] = state;
            AllNodes.Add(new GraphNodeViewModel(n.Schema, n.Name, isSeed, state, chip));
        }

        AllEdges.Clear();
        foreach (var e in graph.Edges)
        {
            var fromId = $"{e.FromSchema}.{e.FromName}";
            var toId = $"{e.ToSchema}.{e.ToName}";
            var style = ComputeEdgeStyle(
                nodeStates.TryGetValue(fromId, out var fState) ? fState : NodeState.Pending,
                nodeStates.TryGetValue(toId, out var tState) ? tState : NodeState.Pending);
            AllEdges.Add(new GraphEdgeViewModel(fromId, toId, style, e.ConstraintName));
        }

        var configuredCount = AllNodes.Count(n => n.State == NodeState.Configured);
        Status = $"{configuredCount}/{AllNodes.Count}";

        RecomputeVisible();
    }

    [RelayCommand]
    private void ExpandNode(string? nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return;
        if (_expandedNodeIds.Add(nodeId)) RecomputeVisible();
    }

    [RelayCommand]
    private void CloseInspector() => InspectedNode = null;

    partial void OnInspectedNodeChanged(GraphNodeViewModel? value)
    {
        if (value is null || _workspace is null)
        {
            InspectorVm = null;
            return;
        }

        var script = _workspace.Package?.Scripts?.FirstOrDefault();
        if (script is null)
        {
            InspectorVm = null;
            return;
        }

        var vm = new NodeInspectorViewModel(
            _saveCallback ?? (() => Task.CompletedTask),
            () => RefreshCommand.ExecuteAsync(null),
            _loggerFactory.CreateLogger<NodeInspectorViewModel>());
        vm.Open(value, _workspace, script);
        InspectorVm = vm;
    }

    partial void OnSelectedNodeIdChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            InspectedNode = null;
            return;
        }

        var node = AllNodes.FirstOrDefault(n =>
            n.NodeId.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (node is null) return;

        if (node.State == NodeState.Pending)
        {
            ExpandNodeCommand.Execute(node.NodeId);
            InspectedNode = null;
        }
        else
        {
            InspectedNode = node;
        }
    }

    [RelayCommand]
    private void ResetFocus()
    {
        _expandedNodeIds.Clear();
        FocusModeChoice = FocusMode.SeedPlusOneHop;
        // OnFocusModeChoiceChanged calls RecomputeVisible if the value actually changed.
        // Force a recompute in case the choice was already SeedPlusOneHop (clearing expanded ids must apply).
        RecomputeVisible();
    }

    partial void OnFocusModeChoiceChanged(FocusMode value) => RecomputeVisible();

    [RelayCommand]
    private async Task ExcludeNodeAsync(string? nodeId)
    {
        var (node, entry) = FindNodeAndEntry(nodeId);
        if (node is null || entry is null) return;
        entry.Excluded = true;
        await PersistAndRefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task IncludeNodeAsync(string? nodeId)
    {
        var (node, entry) = FindNodeAndEntry(nodeId);
        if (node is null || entry is null) return;
        entry.Excluded = false;
        await PersistAndRefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ResetNodeAsync(string? nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || _workspace?.Package?.Scripts is null) return;
        var node = AllNodes.FirstOrDefault(n => n.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null) return;

        var removed = false;
        foreach (var script in _workspace.Package.Scripts)
        {
            if (script.TablesToProcess is null) continue;
            for (var i = script.TablesToProcess.Count - 1; i >= 0; i--)
            {
                var t = script.TablesToProcess[i];
                var schemaMatch = string.IsNullOrEmpty(t.Schema)
                    || string.Equals(t.Schema, node.Schema, StringComparison.OrdinalIgnoreCase);
                if (schemaMatch && string.Equals(t.TableName, node.Name, StringComparison.OrdinalIgnoreCase))
                {
                    script.TablesToProcess.RemoveAt(i);
                    removed = true;
                }
            }
        }

        if (removed) await PersistAndRefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void ShowOnWholeGraph(string? nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return;
        var node = AllNodes.FirstOrDefault(n => n.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null) return;
        FocusModeChoice = FocusMode.WholeSubgraph;
        InspectedNode = node;
    }

    private (GraphNodeViewModel? Node, TableToExtract? Entry) FindNodeAndEntry(string? nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || _workspace?.Package?.Scripts is null) return (null, null);
        var node = AllNodes.FirstOrDefault(n => n.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null) return (null, null);

        foreach (var script in _workspace.Package.Scripts)
        {
            if (script.TablesToProcess is null) continue;
            foreach (var t in script.TablesToProcess)
            {
                var schemaMatch = string.IsNullOrEmpty(t.Schema)
                    || string.Equals(t.Schema, node.Schema, StringComparison.OrdinalIgnoreCase);
                if (schemaMatch && string.Equals(t.TableName, node.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return (node, t);
                }
            }
        }
        return (node, null);
    }

    private async Task PersistAndRefreshAsync()
    {
        if (_saveCallback is not null) await _saveCallback().ConfigureAwait(true);
        await RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private void RecomputeVisible()
    {
        var allowed = ComputeAllowedSet();
        var allowedNodes = AllNodes.Where(n => allowed.Contains(n.NodeId)).ToList();
        var allowedNodeIds = allowedNodes.Select(n => n.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedEdges = AllEdges
            .Where(e => allowedNodeIds.Contains(e.FromNodeId) && allowedNodeIds.Contains(e.ToNodeId))
            .ToList();

        UpdateUnexpandedCounts(allowedNodes, allowedNodeIds);

        VisibleNodes.Clear();
        foreach (var n in allowedNodes) VisibleNodes.Add(n);
        VisibleEdges.Clear();
        foreach (var e in allowedEdges) VisibleEdges.Add(e);
    }

    private HashSet<string> ComputeAllowedSet()
    {
        if (FocusModeChoice == FocusMode.WholeSubgraph)
            return AllNodes.Select(n => n.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seedIds = AllNodes.Where(n => n.IsSeed).Select(n => n.NodeId).ToList();
        var hops = FocusModeChoice == FocusMode.SeedPlusOneHop ? 1 : 2;

        var visible = new HashSet<string>(seedIds, StringComparer.OrdinalIgnoreCase);
        var frontier = new HashSet<string>(seedIds, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < hops; i++)
        {
            var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in AllEdges)
            {
                if (frontier.Contains(e.FromNodeId)) next.Add(e.ToNodeId);
                if (frontier.Contains(e.ToNodeId)) next.Add(e.FromNodeId);
            }
            next.ExceptWith(visible);
            visible.UnionWith(next);
            frontier = next;
        }

        foreach (var expandedId in _expandedNodeIds)
        {
            visible.Add(expandedId);
            foreach (var e in AllEdges)
            {
                if (e.FromNodeId.Equals(expandedId, StringComparison.OrdinalIgnoreCase))
                    visible.Add(e.ToNodeId);
                if (e.ToNodeId.Equals(expandedId, StringComparison.OrdinalIgnoreCase))
                    visible.Add(e.FromNodeId);
            }
        }

        return visible;
    }

    private void UpdateUnexpandedCounts(List<GraphNodeViewModel> visibleList, HashSet<string> visibleIds)
    {
        var degree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in AllEdges)
        {
            degree[e.FromNodeId] = degree.GetValueOrDefault(e.FromNodeId) + 1;
            degree[e.ToNodeId] = degree.GetValueOrDefault(e.ToNodeId) + 1;
        }

        foreach (var n in visibleList)
        {
            if (n.State != NodeState.Pending) { n.UnexpandedNeighbourCount = 0; continue; }
            var inVisible = AllEdges.Count(e =>
                (e.FromNodeId.Equals(n.NodeId, StringComparison.OrdinalIgnoreCase) && visibleIds.Contains(e.ToNodeId)) ||
                (e.ToNodeId.Equals(n.NodeId, StringComparison.OrdinalIgnoreCase) && visibleIds.Contains(e.FromNodeId)));
            n.UnexpandedNeighbourCount = degree.GetValueOrDefault(n.NodeId) - inVisible;
        }
    }

    private static EdgeStyle ComputeEdgeStyle(NodeState from, NodeState to)
    {
        if (from == NodeState.Excluded || to == NodeState.Excluded) return EdgeStyle.Stop;
        if (from == NodeState.Configured && to == NodeState.Configured) return EdgeStyle.Follow;
        return EdgeStyle.Pending;
    }

    private static string ShortStrategyName(ExtractStrategy? s) => s switch
    {
        FKDependencyExtractStrategy _ => "FKDependency",
        OnlyChildrenExtractStrategy _ => "OnlyChildren",
        OnlyParentExtractStrategy _ => "OnlyParent",
        OnlyOneTableExtractStrategy _ => "OnlyOneTable",
        null => string.Empty,
        _ => s.GetType().Name,
    };

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
