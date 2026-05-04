# 07 — desktop-graph-viz — `GraphNodeViewModel` + `GraphEdgeViewModel`

## Goal

Add the per-node and per-edge VMs that surface visual state to the `GraphView` rendering path. Computes node `State` (`Configured` / `Pending` / `Excluded`) by intersecting `ReachableGraph.Nodes` with `Workspace.Package.Scripts[*].TablesToProcess[*]`; computes edge `Style` (`Follow` / `Stop` / `Pending`) from endpoint states. **Pure mapping logic** — no view, no engine call. `GraphViewModel.MapAndApply` from step 06 fills in to populate the `Nodes` and `Edges` collections.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- `GraphViewModel` (step 06) — has `Nodes`, `Edges`, and a stub `MapAndApply` ready to fill in.
- `Logic/Schema/IGraphBuilder.cs` records (`ReachableGraph`, `GraphNode`, `GraphEdge`).
- Engine `Workspace.Package.Scripts[*].TablesToProcess[*]` — already carries `Schema`, `TableName`, `ExtractStrategy`, and (since step 02) `Excluded`.
- `Tests/Desktop/SeedViewModelTests.cs` — pattern exemplar for harness + multiple-scenario tests.

## What to Build

### `Desktop/Views/Graph/NodeState.cs`

```csharp
internal enum NodeState { Configured, Pending, Excluded }
```

### `Desktop/Views/Graph/EdgeStyle.cs`

```csharp
internal enum EdgeStyle { Follow, Stop, Pending }
```

### `Desktop/Views/Graph/GraphNodeViewModel.cs`

`internal sealed partial class GraphNodeViewModel : ObservableObject`. Plain immutable-after-construction:

```csharp
public GraphNodeViewModel(
    string schema, string name,
    bool isSeed,
    NodeState state,
    string strategyChip);

public string NodeId => $"{Schema}.{Name}";  // stable id matching GraphHost
public string Schema { get; }
public string Name { get; }
public bool IsSeed { get; }
public NodeState State { get; }
public string StrategyChip { get; }
public string Display => $"{Schema}.{Name}";
```

No setters — the VM is immutable per refresh. v2's edge-click changes the persistence model; v1 just rebuilds on `RefreshCommand`.

### `Desktop/Views/Graph/GraphEdgeViewModel.cs`

```csharp
internal sealed class GraphEdgeViewModel
{
    public GraphEdgeViewModel(string fromNodeId, string toNodeId, EdgeStyle style, string constraintName);
    public string FromNodeId { get; }
    public string ToNodeId { get; }
    public EdgeStyle Style { get; }
    public string ConstraintName { get; }  // tooltip / debug
}
```

### `GraphViewModel.MapAndApply` — fill in the body

```csharp
private void MapAndApply(ReachableGraph graph, IReadOnlyList<TableRef> seeds)
{
    Reachable = graph;

    // Build a (Schema, Name) → TableToExtract dictionary across all scripts. Last-wins
    // when a table appears in multiple scripts (pragmatic v1 — the desktop-graph-tab
    // feature can refine to per-script scoping if it matters).
    var configuredByKey = new Dictionary<(string Schema, string Name), TableToExtract>(SchemaNameComparer.Instance);
    foreach (var script in _workspace!.Package.Scripts)
    {
        foreach (var t in script.TablesToProcess)
        {
            configuredByKey[(t.Schema ?? string.Empty, t.TableName)] = t;
        }
    }

    var seedKeys = new HashSet<(string Schema, string Name)>(
        seeds.Select(s => (s.Schema, s.Name)),
        SchemaNameComparer.Instance);

    var nodeStates = new Dictionary<string, NodeState>();
    Nodes.Clear();
    foreach (var n in graph.Nodes)
    {
        var key = (n.Schema, n.Name);
        var configured = configuredByKey.TryGetValue(key, out var t);
        var state = !configured ? NodeState.Pending
                  : t!.Excluded ? NodeState.Excluded
                  : NodeState.Configured;
        var chip = configured ? ShortName(t!.ExtractStrategy) : string.Empty;
        var isSeed = seedKeys.Contains(key);

        var nodeId = $"{n.Schema}.{n.Name}";
        nodeStates[nodeId] = state;
        Nodes.Add(new GraphNodeViewModel(n.Schema, n.Name, isSeed, state, chip));
    }

    Edges.Clear();
    foreach (var e in graph.Edges)
    {
        var fromId = $"{e.FromSchema}.{e.FromName}";
        var toId = $"{e.ToSchema}.{e.ToName}";
        var style = ComputeEdgeStyle(nodeStates.GetValueOrDefault(fromId, NodeState.Pending),
                                     nodeStates.GetValueOrDefault(toId, NodeState.Pending));
        Edges.Add(new GraphEdgeViewModel(fromId, toId, style, e.ConstraintName));
    }

    var configuredCount = Nodes.Count(n => n.State == NodeState.Configured);
    Status = $"{configuredCount}/{Nodes.Count}";
}

private static EdgeStyle ComputeEdgeStyle(NodeState from, NodeState to)
{
    if (from == NodeState.Excluded || to == NodeState.Excluded) return EdgeStyle.Stop;
    if (from == NodeState.Configured && to == NodeState.Configured) return EdgeStyle.Follow;
    return EdgeStyle.Pending;
}

private static string ShortName(ExtractStrategy s) => s switch
{
    FKDependencyExtractStrategy   _ => "FKDependency",
    OnlyChildrenExtractStrategy   _ => "OnlyChildren",
    OnlyParentExtractStrategy     _ => "OnlyParent",
    OnlyOneTableExtractStrategy   _ => "OnlyOneTable",
    _                                => s?.GetType().Name ?? string.Empty,
};
```

`SchemaNameComparer` — small `IEqualityComparer<(string,string)>` keyed `OrdinalIgnoreCase` on both fields. Lives next to the VM.

### Tests

Extend `Tests/Desktop/GraphViewModelTests.cs` (started in step 06) with a state-matrix suite. New tests:

13. `MapAndApply_NodeReachableButNotInTablesToProcess_StateIsPending`.
14. `MapAndApply_NodeInTablesToProcess_NotExcluded_StateIsConfigured`.
15. `MapAndApply_NodeInTablesToProcess_Excluded_StateIsExcluded`.
16. `MapAndApply_SeedNode_IsSeedTrue`.
17. `MapAndApply_StrategyChip_FKDependency_RenderedAsFKDependency` (and one for OnlyChildren).
18. `MapAndApply_EdgeBetweenConfigured_StyleFollow`.
19. `MapAndApply_EdgeWithExcludedEndpoint_StyleStop`.
20. `MapAndApply_EdgeWithPendingEndpoint_StylePending`.
21. `MapAndApply_StatusFormat_ConfiguredOverTotal` — e.g. graph has 3 Configured + 5 total → `Status == "3/5"`.
22. `MapAndApply_DuplicateTableAcrossScripts_LastWinsForState`.

Test data uses fake `ReachableGraph` instances built inline (no DB call) — `FakeGraphBuilder` returns these.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-06 N + 10 (new state-matrix tests added on top of step 06's 12), all green.

## Acceptance Criteria

- [ ] `NodeState`, `EdgeStyle`, `GraphNodeViewModel`, `GraphEdgeViewModel`, `SchemaNameComparer` exist under `Desktop/Views/Graph/`.
- [ ] `GraphViewModel.MapAndApply` fully implemented; node-state and edge-style matrix matches `feature-architecture.md` § 6 / § 7.
- [ ] Status string format: `{configured}/{total}`.
- [ ] All 22 `GraphViewModelTests` pass (12 from step 06 + 10 new).
- [ ] No tripwires introduced.

## References

- Architecture: [`feature-architecture.md`](./feature-architecture.md) § 6 (node-state matrix), § 7 (edge-style matrix).
- Pattern exemplars: `ScriptEditorViewModel.RootRef` (TableRef ↔ schema/name decomposition); `ConnectionEditorViewModel` (enum-driven state).
- Depends on: [06 — GraphViewModel](./06-desktop-graph-viz-graph-view-model.md).
