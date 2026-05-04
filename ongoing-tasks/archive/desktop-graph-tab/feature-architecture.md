# Desktop Graph Tab — Architecture Overview

> Reframes `desktop-graph-viz` v1 from "render the whole reachable subgraph" into "render a focused subgraph that grows as the operator explores." Adds the M6 slide-in inspector pane as the payoff for clicking on configured / excluded nodes — the engine already honours `Excluded`; this feature ships the UI that flips it.

---

## 1. Focus filter — the core idea

The engine seam `IGraphBuilder.BuildAsync` returns the **whole** FK-reachable subgraph in one call (it's cheap; 328 tables × 490 FKs in the test DB returns in milliseconds). The Graph tab now filters that graph **client-side** to a visible subset and grows that subset as the operator clicks Pending nodes.

```
Loaded graph (ReachableGraph from IGraphBuilder.BuildAsync)
    │
    │ contains every (Schema, Name) reachable from any seed,
    │ regardless of how far away
    │
    ▼
GraphViewModel
    Reachable                 (full graph, never re-fetched while workspace open)
    AllNodes / AllEdges       (full mapped Node/Edge VMs from the matrix in v1)
    ────────────────────────────────────
    FocusMode = SeedPlusOneHop / SeedPlusTwoHops / WholeSubgraph
    ExpandedNodeIds : HashSet<string>   (nodes the operator has clicked to expand)
    ────────────────────────────────────
    VisibleNodes / VisibleEdges (computed; bound to the host control)

Visibility rules (per current FocusMode):

  WholeSubgraph:    Visible = All
  SeedPlusOneHop:   Visible = seeds ∪ {neighbours of seeds} ∪ ExpansionFrontier
  SeedPlusTwoHops:  Visible = seeds ∪ {n where dist(n, seed) ≤ 2} ∪ ExpansionFrontier

  ExpansionFrontier = ⋃ over n ∈ ExpandedNodeIds of {n} ∪ {immediate neighbours of n}

Edges visible = edges where BOTH endpoints are in Visible nodes.
```

`FocusMode` and `ExpandedNodeIds` together describe a **manually-grown subgraph** that gets recomputed whenever either changes. The recompute is O(|edges|) — fast.

---

## 2. +N count badge

Each visible **Pending** node carries a `+N` badge — the count of FK partners NOT currently in `VisibleNodes`. Computed during the visible-subset recompute:

```
foreach node n in VisibleNodes where n.State == Pending:
    n.UnexpandedNeighbourCount = |neighbours(n)| - |neighbours(n) ∩ VisibleNodes|
```

Configured / Excluded nodes don't carry a badge — clicking them opens the inspector instead of expanding.

---

## 3. Click dispatch

Single click on a node is **contextual**:

```
GraphHostView.MouseDown
    ↓ raises SelectedNodeId DP change
GraphViewModel.OnSelectedNodeIdChanged(nodeId)
    ↓
    n = FindNode(nodeId)
    │
    ├── n.State == Pending
    │     → ExpandNode(nodeId)
    │     → ExpandedNodeIds.Add(nodeId)
    │     → recompute VisibleNodes / VisibleEdges
    │     → InspectedNode stays null (no inspector)
    │
    └── n.State == Configured | Excluded
          → InspectedNode = n
          → inspector flyout opens
          → ExpandedNodeIds unchanged
```

Right-click opens a context menu with cross-cutting actions (Exclude, Include, Reset to Pending, Show on whole graph) — these don't follow the click-dispatch logic because they're explicit operator intent.

The `SelectedNodeId` DP keeps its existing two-way binding so future features (highlight, scroll-to-node) can layer on top without rewiring.

---

## 4. Inspector ↔ workspace round-trip

```
Inspector opens for node n (state ∈ {Configured, Excluded})
    │
    ├── _workspace.Package.Scripts[*].TablesToProcess[*]  (find by (Schema, Name))
    │     → there is exactly one matching entry (state is Configured or Excluded
    │       per the matrix in v1; only Pending lacks an entry)
    │
    ├── Inspector binds:
    │     - StrategyChoice         ↔ entry.ExtractStrategy.GetType() (radio over four kinds)
    │     - Where                  ↔ entry.ExtractStrategy.Where
    │     - Excluded               ↔ entry.Excluded
    │
    ├── Operator edits → save-on-blur (or save-on-change for the radio + checkbox):
    │     1. Update entry.ExtractStrategy / Where / Excluded in-place
    │     2. await SaveWorkspaceAsync()             (existing MainWindowViewModel callback)
    │     3. Refresh node state via GraphViewModel.RecomputeStateForNode(n)
    │     4. Recompute edge styles for n's edges
    │
    └── Operator closes inspector → InspectedNode = null
```

Adding a Pending node to extract:

```
Inspector opens for Pending node n
    │
    ├── No TablesToProcess entry exists
    ├── Show "Add to extract" button instead of strategy editor
    ├── Click → workspace.Package.Scripts[currentScript].TablesToProcess.Add(
    │            new TableToExtract(n.Name, FKDependencyExtractStrategy(), SqlBuildStrategy())
    │            { Schema = n.Schema })
    ├── SaveWorkspaceAsync()
    └── n's state recomputes to Configured; inspector morphs into the standard editor
```

"Recompute graph" button on the inspector → `await GraphViewModel.RefreshCommand.ExecuteAsync(null)` — re-runs `IGraphBuilder.BuildAsync` against the updated workspace. Useful when adding to extract pulled new FK partners into reach.

---

## 5. Component diagram

```
+------------------------------------------------------+
|  ParameterizationExtractor.Desktop                   |
|                                                      |
|  Views/Graph/                                        |
|   ├── GraphView.xaml                                 |
|   ├── GraphViewModel  (Singleton — extended)         |
|   │     + FocusMode (enum)                           |
|   │     + ExpandedNodeIds : HashSet<string>          |
|   │     + VisibleNodes / VisibleEdges                |
|   │     + InspectedNode : GraphNodeViewModel?        |
|   │     + ExpandNodeCommand                          |
|   │     + ResetFocusCommand                          |
|   │     + RecomputeGraphCommand                      |
|   ├── NodeInspectorView.xaml      (NEW — Flyout)     |
|   └── NodeInspectorViewModel       (NEW)             |
|         + Selected (GraphNodeViewModel)              |
|         + StrategyChoice (enum)                      |
|         + Where (string)                             |
|         + Excluded (bool)                            |
|         + AddToExtractCommand                        |
|         + RecomputeGraphCommand                      |
|                                                      |
|  Controls/GraphHost/  (extended)                     |
|   └── GraphHostView                                  |
|         + binds VisibleNodes / VisibleEdges          |
|         + raises right-click events with picked node |
+------------------------------------------------------+
```

No new engine work. No new project references.

---

## 6. State model — `GraphViewModel` (extended)

| Member | Type | Source |
|---|---|---|
| _existing v1 members_ | … | … |
| `FocusMode` | `enum FocusMode { SeedPlusOneHop, SeedPlusTwoHops, WholeSubgraph }` | toolbar radio; default `SeedPlusOneHop` |
| `ExpandedNodeIds` | `HashSet<string>` | clicks on Pending nodes; reset by `ResetFocusCommand` / `FocusMode` change |
| `VisibleNodes` | `ObservableCollection<GraphNodeViewModel>` | computed |
| `VisibleEdges` | `ObservableCollection<GraphEdgeViewModel>` | computed |
| `InspectedNode` | `GraphNodeViewModel?` | clicks on Configured / Excluded nodes; null when inspector closed |
| `ExpandNodeCommand` | `IRelayCommand<string>` | adds nodeId to ExpandedNodeIds + recomputes |
| `ResetFocusCommand` | `IRelayCommand` | clears ExpandedNodeIds + sets FocusMode = SeedPlusOneHop |
| `RecomputeGraphCommand` | `IAsyncRelayCommand` | re-runs `RefreshCommand` |

`AllNodes` / `AllEdges` (the v1 collections, renamed) hold the full mapped VMs; `VisibleNodes` / `VisibleEdges` are the rendered subset.

---

## 7. State model — `NodeInspectorViewModel`

| Member | Type | Notes |
|---|---|---|
| `Selected` | `GraphNodeViewModel` | the node the inspector is open for |
| `Source` | `TableToExtract?` | the engine entry; null when Selected is Pending |
| `StrategyChoice` | `enum StrategyKind { FKDependency, OnlyChildren, OnlyParent, OnlyOneTable }` | radio in the view |
| `Where` | `string` | binds to `Source.ExtractStrategy.Where` |
| `Excluded` | `bool` | binds to `Source.Excluded` |
| `IsAddable` | computed `bool` | `Source is null` (i.e. Selected is Pending) |
| `AddToExtractCommand` | `IRelayCommand` | creates the `TableToExtract`; flips `Source` non-null |
| `RecomputeGraphCommand` | `IAsyncRelayCommand` | delegates to `GraphViewModel.RefreshCommand` |
| `Save` | callback | invoked on every property change; mirrors seed-tab save-on-blur |

The inspector edits the engine model **in-place** (matching the seed-tab pattern). On Save, `MainWindowViewModel.SaveWorkspaceAsync` persists.

---

## 8. Threading & cancellation

- Focus filter recomputes are synchronous (O(edges); fast for any reasonable schema).
- Click-to-expand is synchronous.
- `RecomputeGraphCommand` re-runs `GraphViewModel.RefreshCommand` which is `[RelayCommand(IncludeCancelCommand = true)]` — already cancellable from v1.
- Inspector save chains the existing `SaveWorkspaceAsync` callback; mirrors seed-tab. No new async surface.
- `IUiDispatcher` is the only marshalling seam — VM mutations from the engine refresh continue to go through `_ui.InvokeAsync`.

---

## 9. Risks & open questions

- **Layout stability across expansions** — AGL re-lays out the whole `DrawingGraph` each time `Viewer.Graph` is set. Adding a single node will reshuffle everyone's positions. **Mitigation:** accept v1 reshuffles; add positional caching if it bothers operators.
- **Inspector + multi-script workspaces** — inspector edits one script at a time. Decision: anchor inspector to the seed-tab's `SelectedScript`. The seed → graph anchor sync (step 07) keeps these aligned.
- **Add-to-extract + Schema** — Pending nodes have a discovered schema. The new `TableToExtract` entry uses that schema directly (matches ADR-011's "explicit Schema is unambiguous"). No bare-name fallback for newly-added entries.
- **Right-click on empty canvas** — does nothing (no global graph context menu). Right-click ON a node opens the contextual menu.
- **`Show on whole graph`** action — switches `FocusMode = WholeSubgraph` then opens the inspector for the chosen node. Useful when the operator knows a table is reachable but it's beyond the focus radius.
- **Concurrent inspector edits** — only one inspector pane at a time; opening on a different node closes the previous one (cancelling pending saves cleanly).
- **Engine edge case: an excluded table that's a seed root** — Exclude doesn't make sense for a seed (you'd extract nothing). Add a guard: don't render the Excluded toggle on a seed node; show a tooltip explaining why.

---

## 10. Security & isolation

No new trust boundaries. Inspector edits the workspace JSON in process; persistence goes through the existing `IWorkspaceStore` (DPAPI-encrypted password stays untouched). No new credentials, no new network surface.

---

## 11. What this feature does NOT build

- Edge-click toggling Follow/Stop (`desktop-graph-edge-click` v2 per ADR-008).
- Per-node `BuildDirectivesEditor` (INSERT/UPDATE checkboxes) — defer.
- `FKEdgeList` per-edge UI inside the inspector — defer to a follow-up.
- Auto-pan / "go to next pending" navigation.
- Persistent layout / focus-radius preference per workspace.
- Self-referential FK rendering (carried-over v1 limitation).
