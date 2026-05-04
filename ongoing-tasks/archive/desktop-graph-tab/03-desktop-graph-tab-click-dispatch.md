# 03 — desktop-graph-tab — Click-dispatch (Pending → expand; Configured/Excluded → InspectedNode)

## Goal

Wire the single-click contextual behaviour: clicking a Pending node expands it; clicking a Configured / Excluded node sets `InspectedNode` (which step 05 will bind to a slide-in pane). **No inspector view yet** — step 04 builds the VM, step 05 the view.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- `SelectedNodeId` DP on `GraphHostView` already raises change notifications when the operator clicks a node.
- `GraphViewModel.ExpandNodeCommand` (step 01).
- `GraphViewModel.AllNodes` (step 01 rename).

## What to Build

### `GraphViewModel` — `InspectedNode` + click dispatch

Add observable property:

```csharp
[ObservableProperty]
private GraphNodeViewModel? _inspectedNode;
```

Override `OnSelectedNodeIdChanged` (CTK partial method):

```csharp
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
        // Configured or Excluded → open inspector.
        InspectedNode = node;
    }
}
```

Add `CloseInspectorCommand`:

```csharp
[RelayCommand]
private void CloseInspector() => InspectedNode = null;
```

### Tests

Extend `Tests/Desktop/GraphViewModelTests.cs`:

1. `OnSelectedNodeIdChanged_PendingNode_TriggersExpand_NoInspector` — set `SelectedNodeId` to a Pending node's id; assert `_expandedNodeIds` (via `VisibleNodes` containing its neighbours) AND `InspectedNode == null`.
2. `OnSelectedNodeIdChanged_ConfiguredNode_SetsInspectedNode_NoExpand` — set `SelectedNodeId` to a Configured node; `InspectedNode` is the node; `_expandedNodeIds` unchanged.
3. `OnSelectedNodeIdChanged_ExcludedNode_SetsInspectedNode` — same for Excluded.
4. `OnSelectedNodeIdChanged_NullOrEmpty_ClearsInspector` — clearing the selection closes the inspector.
5. `OnSelectedNodeIdChanged_UnknownId_NoOp` — bogus id doesn't crash, no state change.
6. `CloseInspector_ClearsInspectedNode`.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-02 N + 6, all green.
- The Graph tab still renders the seed + 1 hop after step 02; click-dispatch is observable via VM state but not yet visible in UI (inspector pane lands in step 05).

## Acceptance Criteria

- [ ] `GraphViewModel.InspectedNode` exists; `CloseInspectorCommand` exists.
- [ ] `OnSelectedNodeIdChanged` dispatches: Pending → ExpandNodeCommand; Configured / Excluded → InspectedNode.
- [ ] Selecting null / unknown id is safe.
- [ ] All 6 new tests pass.
- [ ] No view changes; no new tripwires.

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 3.
- Depends on: [01 — Focus filter](./01-desktop-graph-tab-focus-filter.md).
