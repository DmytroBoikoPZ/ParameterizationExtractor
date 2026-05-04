# 06 — desktop-graph-tab — Right-click context menu (Exclude / Include / Reset / Show on whole graph)

## Goal

Land a minimal right-click context menu on graph nodes for the cross-cutting actions that don't fit click-dispatch. Four items: **Exclude**, **Include**, **Reset to Pending** (only for Configured/Excluded), **Show on whole graph**.

## Track

`desktop` (WPF / C#).

## What Exists

- `GraphHostView.MouseDown` already fires; need to distinguish left vs right click and surface the right-clicked node.
- `GraphViewModel.AllNodes` / `_workspace` / `_currentScript` from steps 01–04.

## What to Build

### `GraphHostView` — surface right-click events

Add a routed event or new DP `RightClickedNodeId` (string?, two-way) that's set when the operator right-clicks a node. AGL's `MouseDown` already gives access to `ObjectUnderMouseCursor`; check `e.RightButtonIsPressed` (or similar — verify against AGL's `MsaglMouseEventArgs`).

When right-clicked: set `RightClickedNodeId` AND raise the WPF `ContextMenuOpening` event so a `<ContextMenu>` resource on the host can show.

### `GraphViewModel` — context menu commands

```csharp
[RelayCommand] private async Task ExcludeNodeAsync(string? nodeId);
[RelayCommand] private async Task IncludeNodeAsync(string? nodeId);
[RelayCommand] private async Task ResetNodeAsync(string? nodeId);   // remove from TablesToProcess → goes to Pending
[RelayCommand] private void ShowOnWholeGraph(string? nodeId);       // FocusMode = WholeSubgraph + InspectedNode = node
```

Each mutates the workspace appropriately and calls `_saveCallback`.

### `GraphView.xaml` — `<ContextMenu>` resource

Attach to `<graphHost:GraphHostView>` via the standard `ContextMenu` property. Items bind to the four commands above with `CommandParameter="{Binding RightClickedNodeId, Source=...}"`. Item visibility per node state (Exclude only on Configured; Include only on Excluded; Reset only on Configured/Excluded).

### Tests

Extend `Tests/Desktop/GraphViewModelTests.cs`:

1. `ExcludeNode_SetsExcludedTrue_PersistsAndRecomputes`.
2. `IncludeNode_SetsExcludedFalse_PersistsAndRecomputes`.
3. `ResetNode_RemovesFromTablesToProcess_NodeGoesToPending`.
4. `ShowOnWholeGraph_SetsFocusModeWholeSubgraph_AndInspectedNode`.
5. `ContextCommands_NullNodeId_NoOp`.
6. `ContextCommands_UnknownNodeId_NoOp_NoCrash`.

### Verification

- `dotnet build` / `dotnet test` — pre-step-05 N + 6, all green.
- Manual smoke: right-click on Configured → menu shows Exclude / Reset / Show on whole graph; click Exclude → state flips. Right-click on Pending → only Show on whole graph (Exclude/Include/Reset are not applicable to Pending — verify gating).

## Acceptance Criteria

- [ ] `GraphHostView` raises right-click + surfaces `RightClickedNodeId`.
- [ ] Four commands on `GraphViewModel`; behave per spec.
- [ ] `<ContextMenu>` shows + items gated by node state.
- [ ] All 6 tests pass.

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 9 (Risks).
- Depends on: [03 — Click-dispatch](./03-desktop-graph-tab-click-dispatch.md), [04 — Inspector VM](./04-desktop-graph-tab-inspector-vm.md).
