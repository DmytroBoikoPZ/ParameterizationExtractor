# 01 — desktop-graph-tab — Focus filter in `GraphViewModel`

## Goal

Add the focus-filter machinery to `GraphViewModel`: `FocusMode` enum, `ExpandedNodeIds` set, `VisibleNodes` / `VisibleEdges` computed collections, and `+N` count badge data. Pure VM logic — **no view changes yet**, **no host wiring**, **no inspector**. Step 02 wires the toolbar; step 03 wires the click dispatch.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`Desktop/Views/Graph/GraphViewModel.cs`](../../ParameterizationExtractor.Desktop/Views/Graph/GraphViewModel.cs) — v1 holds full `Nodes` + `Edges`. Renamed in this step.
- [`Desktop/Views/Graph/GraphNodeViewModel.cs`](../../ParameterizationExtractor.Desktop/Views/Graph/GraphNodeViewModel.cs) — extend with `UnexpandedNeighbourCount` (int).
- [`Tests/Desktop/GraphViewModelTests.cs`](../../Tests/Desktop/GraphViewModelTests.cs) — extend with focus-filter tests.

## What to Build

### `Desktop/Views/Graph/FocusMode.cs` (new)

```csharp
internal enum FocusMode { SeedPlusOneHop, SeedPlusTwoHops, WholeSubgraph }
```

### `GraphNodeViewModel` — add `UnexpandedNeighbourCount`

```csharp
public int UnexpandedNeighbourCount { get; internal set; }
```

Set by the focus-filter recompute (step 01). Internal setter so only the VM mutates it.

### `GraphViewModel` — extended members

Rename existing `Nodes` / `Edges` collections to **`AllNodes`** / **`AllEdges`** (full mapped subset). Add new `VisibleNodes` / `VisibleEdges` (`ObservableCollection<...>`) — these are what the host will bind to (step 02).

Add:

```csharp
[ObservableProperty]
private FocusMode _focusModeChoice = FocusMode.SeedPlusOneHop;

private readonly HashSet<string> _expandedNodeIds = new(StringComparer.OrdinalIgnoreCase);

public ObservableCollection<GraphNodeViewModel> VisibleNodes { get; } = new();
public ObservableCollection<GraphEdgeViewModel> VisibleEdges { get; } = new();

[RelayCommand]
private void ExpandNode(string? nodeId)
{
    if (string.IsNullOrEmpty(nodeId)) return;
    if (_expandedNodeIds.Add(nodeId)) RecomputeVisible();
}

[RelayCommand]
private void ResetFocus()
{
    _expandedNodeIds.Clear();
    FocusModeChoice = FocusMode.SeedPlusOneHop;
    RecomputeVisible();
}

partial void OnFocusModeChoiceChanged(FocusMode value) => RecomputeVisible();

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

    // Expanded nodes pull in their immediate neighbours regardless of hop limit.
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
```

`MapAndApply` (existing) ends with `RecomputeVisible()` so the visible collections are populated immediately after `BuildAsync` returns.

### Tests

Extend `Tests/Desktop/GraphViewModelTests.cs`:

1. `Hydrate_DefaultFocusMode_IsSeedPlusOneHop`.
2. `Hydrate_FocusModeSeedPlusOneHop_VisibleIncludesSeedAndDirectNeighbours_OnlyThose` — graph has Seed + N1 (1 hop) + N2 (2 hops). Visible = {Seed, N1}. N2 hidden.
3. `Hydrate_FocusModeSeedPlusTwoHops_VisibleIncludesUpToTwoHops`.
4. `Hydrate_FocusModeWholeSubgraph_VisibleEqualsAllNodes`.
5. `ExpandNode_AddsNodeAndItsNeighboursToVisible` — start in SeedPlusOneHop with N1 visible. Expand N1. Now N2 (one of N1's neighbours) is visible.
6. `ResetFocus_ClearsExpandedAndRestoresSeedPlusOneHop` — after expansion, ResetFocus drops everything beyond the seed neighbours.
7. `UnexpandedNeighbourCount_PendingNode_ExcludesAlreadyVisibleNeighbours` — Pending N1 has 5 FK partners, 2 are visible → count = 3.
8. `UnexpandedNeighbourCount_ConfiguredNode_AlwaysZero` — clicks on Configured don't expand; badge irrelevant.
9. `RecomputeVisible_PreservesNodeIdentity_ObservableCollectionDeltaMinimal` — re-running RecomputeVisible with no state change yields the same VM instances (no churn). [Optional / nice-to-have]
10. `MapAndApply_PopulatesVisibleNodesAndEdges` — after `Hydrate` of a workspace, `VisibleNodes` is non-empty (was empty before this step's RecomputeVisible call).

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-existing N + 10, all green. Existing 21 GraphViewModelTests must continue to pass (renaming `Nodes` → `AllNodes` may require small touch-ups in tests that asserted on `.Nodes` directly; update them to use `AllNodes`).

## Acceptance Criteria

- [ ] `FocusMode` enum exists; `GraphViewModel` extended with `FocusModeChoice`, `_expandedNodeIds`, `VisibleNodes`, `VisibleEdges`, `ExpandNodeCommand`, `ResetFocusCommand`.
- [ ] Existing `Nodes` / `Edges` renamed to `AllNodes` / `AllEdges` (host bindings are step 02; tests updated).
- [ ] `RecomputeVisible` runs at end of `MapAndApply` so the visible collections track the loaded graph.
- [ ] `UnexpandedNeighbourCount` populated on Pending nodes.
- [ ] All 10 new tests pass; all existing tests still pass byte-identically (after the `Nodes` → `AllNodes` rename in tests).
- [ ] No new tripwires.

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 1, § 2, § 6.
- Pattern exemplar: existing v1 `MapAndApply` mapping logic (last-wins lookup matrix).
- Depends on: `desktop-graph-viz` (provides the v1 baseline). No intra-feature dependencies.
