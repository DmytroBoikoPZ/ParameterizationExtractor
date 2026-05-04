# Desktop Graph Tab — Focus Mode + Node Inspector

## Goal

Replace `desktop-graph-viz` v1's "show whole reachable subgraph" survey lens with a **focus + click-to-explore** lens (revised [M5 mockup](../docs/design/desktop-ui/05-graph.md)) and ship the **slide-in node inspector** ([M6](../docs/design/desktop-ui/06-node-inspector.md)) as the same gesture's payoff. Clicking a node either expands its FK neighbours (Pending) or opens the inspector (Configured / Excluded) — the user *walks* the graph instead of staring at it.

The focus reframing makes the graph genuinely actionable. The inspector closes the loop on `Excluded`-flag editing (the engine already honours it, but v1 has no UI to set it). Together they turn `desktop-graph-viz` v1's groundwork into a usable feature.

## Scope

- **In scope:**
  - **Focus toolbar** on `GraphView` — radio group: `(•) Seed + 1 hop` (default) / `( ) Seed + 2 hops` / `( ) Whole reachable subgraph` (the v1 mode, retained as a debug/validation lens). New "Reset focus" button restores Seed + 1 hop.
  - **Visible-subgraph filter** in `GraphViewModel` — `ObservableCollection<GraphNodeViewModel> VisibleNodes` + `VisibleEdges`, computed from the loaded `ReachableGraph` plus a `HashSet<string> _expandedNodeIds`. The host renders only `VisibleNodes` / `VisibleEdges`.
  - **Click-to-expand on Pending nodes** — `ExpandNodeCommand(nodeId)` — adds the node + its immediate FK partners to `_expandedNodeIds`; recomputes `VisibleNodes`. The current `SelectedNodeId` DP wires through, BUT the click handler dispatches: Pending → Expand; Configured / Excluded → open inspector. (Single click → contextual action.)
  - **+N count badge** on each Pending node — number of FK partners NOT currently visible. Computed on render.
  - **Node inspector pane (M6)** — `Views/Graph/NodeInspectorView.xaml(.cs)` + `NodeInspectorViewModel`. Slide-in `<mah:Flyout>` from the right. Bound to the currently-selected `GraphNodeViewModel` + the engine's `TableToExtract` for that table.
    - Strategy picker — radio group over `FKDependency / OnlyChildren / OnlyParent / OnlyOneTable` (matches engine's four strategies); selecting the new strategy mutates `Workspace.Package.Scripts[*].TablesToProcess[i].ExtractStrategy` (last-script-wins, same convention as `GraphViewModel.MapAndApply`'s state computation).
    - `Where` editor — `<editor:SqlEditorView IsSingleLine="True"/>` bound to `ExtractStrategy.Where`. Reuses `SqlEditor` from `desktop-seed-tab`.
    - `Excluded` toggle — `<CheckBox>` bound to `TableToExtract.Excluded`. When flipped:
      - true → node state goes to Excluded; FK edges to/from re-style as Stop.
      - false → state recomputes (Configured if matched, Pending if no entry).
    - Add-to-extract button (visible when the selected node is Pending) — adds a `TableToExtract` entry to the workspace. Opens the inspector populated with engine defaults (FKDependency strategy, no Where).
    - "Recompute graph" button — re-runs `IGraphBuilder.BuildAsync` against the updated workspace. Useful when the operator added a new entry that should bring new FK partners into reach.
    - Save-on-blur / save-on-change for inspector edits, mirroring the seed-tab pattern. `MainWindowViewModel.SaveWorkspaceAsync` is the same callback (already exists).
  - **`GraphHostView` wiring extension** — `SelectedNodeId` now drives the host to AGL-highlight the picked node (currently the DP changes but nothing visible). On expand, the canvas auto-fits the new visible subgraph.
  - **Inspector dispatch in `GraphViewModel`** — new `[ObservableProperty] GraphNodeViewModel? _inspectedNode;` set when a Configured / Excluded node is clicked; bound to `<mah:Flyout IsOpen={Binding InspectedNode, Converter=NotNullToVis}>`.
  - **Right-click context menu** — minimal: "Exclude" / "Include" / "Reset to Pending" / "Show on whole graph". The fourth opens the inspector for an off-screen node.
  - **`SeedView` connection** — when the seed-tab's `SelectedScript` changes, the Graph tab's anchor (the seed) follows. Single-context view per architecture § Open questions.
  - **Tests** — `GraphViewModelTests` (focus filter: starts at +1 hop, expands on click, resets on Reset; +N count correctness; click-Configured opens inspector; click-Pending expands instead); `NodeInspectorViewModelTests` (strategy radio mutates engine model; Excluded toggle re-styles edges; add-to-extract creates `TableToExtract` entry; Where editor save-on-blur).
  - **Cross-doc updates** — M6 mockup status → implemented; components.md NodeInspector / StrategyPicker / WhereFilterEditor / FKEdgeList / BuildDirectivesEditor → implemented; recipe note about the click-dispatch pattern; roadmap moves `desktop-graph-tab` to Completed.

- **Out of scope:**
  - **Edge-click toggling Follow/Stop** — `desktop-graph-edge-click` v2 (per ADR-008 lock).
  - **Per-node `BuildDirectivesEditor`** (INSERT/UPDATE checkboxes + identity-insert) from M6 — defer to a smaller follow-up. v1 inspector covers Strategy / Where / Excluded which already cover most operator decisions.
  - **`FKEdgeList`** (per-edge Follow/Stop dropdowns) — engine model has `DependencyToExclude` (per-strategy edge skip list) but the UX is fiddly enough to warrant its own feature.
  - **Auto-pan to "next pending"** navigation — deferred.
  - **Persistent layout / focus-radius preference per workspace** — in-memory only; resets on reopen.
  - **Self-referential FK rendering** — known v1 limitation carried over.

- **Dependencies:**
  - [`desktop-graph-viz`](./archive/desktop-graph-viz-checklist.md) — provides `IGraphBuilder`, `Controls/GraphHost/`, `GraphViewModel` baseline, `Excluded` flag (model + wiring), `NodeState` / `EdgeStyle` mapping.
  - [`desktop-seed-tab`](./archive/desktop-seed-tab-checklist.md) — provides `SqlEditor` (reused by Where editor), `IUiDispatcher`, save-on-blur pattern.
  - Mockup: [M5 — Graph](../docs/design/desktop-ui/05-graph.md) (revised for focus mode), [M6 — Node inspector](../docs/design/desktop-ui/06-node-inspector.md).
  - Components: [NodeInspector](../docs/design/desktop-ui/components.md#nodeinspector), [StrategyPicker](../docs/design/desktop-ui/components.md#strategypicker), [WhereFilterEditor](../docs/design/desktop-ui/components.md#wherefiltereditor).
  - ADR-012 (graph viz library); ADR-011 (schema-aware tuples through the inspector's `TableToExtract` edits).

## Architecture

See [feature-architecture.md](./desktop-graph-tab/feature-architecture.md) for the focus filter algorithm, the click-dispatch decision tree, the inspector ↔ workspace round-trip, and the `Recompute graph` flow.

## Steps

- [x] [01 — Focus filter in `GraphViewModel` (VisibleNodes/VisibleEdges + ExpandedNodeIds + +N count)](./desktop-graph-tab/01-desktop-graph-tab-focus-filter.md)
- [x] [02 — Toolbar Focus radio + Reset focus button + GraphHostView wiring to render only visible subset](./desktop-graph-tab/02-desktop-graph-tab-focus-toolbar.md)
- [x] [03 — Click-dispatch: Pending → expand; Configured/Excluded → InspectedNode (no inspector pane yet)](./desktop-graph-tab/03-desktop-graph-tab-click-dispatch.md)
- [x] [04 — `NodeInspectorViewModel` (strategy / Where / Excluded / add-to-extract / recompute) + tests](./desktop-graph-tab/04-desktop-graph-tab-inspector-vm.md)
- [x] [05 — `NodeInspectorView` (M6) — slide-in Flyout with strategy radio / SqlEditor for Where / Excluded checkbox / buttons](./desktop-graph-tab/05-desktop-graph-tab-inspector-view.md)
- [x] [06 — Right-click context menu (Exclude / Include / Reset / Show on whole graph)](./desktop-graph-tab/06-desktop-graph-tab-context-menu.md)
- [x] [07 — Seed-tab → Graph-tab anchor sync (single-context view)](./desktop-graph-tab/07-desktop-graph-tab-seed-anchor-sync.md)
- [x] [08 — Manual smoke + sample.bws update + cross-doc updates](./desktop-graph-tab/08-desktop-graph-tab-smoke-and-cross-docs.md)

## Notes

Free-form log of decisions, blockers, links to PRs. Dated entries preferred (YYYY-MM-DD).

- 2026-05-03 — Feature kicked off after `desktop-graph-viz` v1 smoke testing exposed that the survey-mode rendering had no operational value: "looks like epic, but has zero value now. I would expect that user is positioning to the root table and it is zoomed closely so user can start walking… click on some dependency table and it shows 1 level of tables around and etc." The v1 graph view was a wall of nodes for any real DB; this feature ships the actionable explore experience and folds the M6 inspector pane in as the natural payoff for clicks (since the engine already honours `Excluded` but v1 has no UI to flip it).
- 2026-05-03 — Architectural choice: focus filter is **client-side** (`GraphViewModel` filters the loaded `ReachableGraph`). The engine seam `IGraphBuilder.BuildAsync` keeps returning the whole reachable graph in one call; loading 328 tables / 490 FKs is sub-second. No new engine method, no per-click round-trip. Simpler + faster for typical schema sizes.
- 2026-05-03 — Click-dispatch decision: single click is contextual — Pending → expand; Configured / Excluded → open inspector. Avoids modal switching ("are you in expand mode or inspect mode?"). Right-click handles the cross-cutting actions (Exclude / Include / Reset / Show on whole graph).
- 2026-05-03 — All 8 steps executed end-to-end:
  - **Step 01** — `FocusMode` enum + `GraphViewModel` extended with `FocusModeChoice`, `_expandedNodeIds`, `VisibleNodes`/`VisibleEdges`, `ExpandNodeCommand`, `ResetFocusCommand`, `RecomputeVisible`, `+N` badge via `GraphNodeViewModel.UnexpandedNeighbourCount`. Renamed `Nodes`/`Edges` → `AllNodes`/`AllEdges` (test/host-binding refs updated). 10 new tests pass; 21 pre-existing GraphViewModelTests pass after rename.
  - **Step 02** — `EnumMatchConverter` added + registered as `EnumMatchConverter` in `App.xaml`. Toolbar gained Focus radio (3 options) + Reset focus button. `GraphHostView` extended with `VisibleNodes`/`VisibleEdges` DPs (`Graph` DP retained as fallback per spec); `RenderFromVisible` builds the AGL `DrawingGraph` from VM collections and appends `+N` badge to node labels. Subscribes to `INotifyCollectionChanged` so AGL re-renders when the visible set changes.
  - **Step 03** — `[ObservableProperty] InspectedNode` + `CloseInspectorCommand` + `OnSelectedNodeIdChanged` dispatch (Pending → ExpandNodeCommand; Configured/Excluded → InspectedNode = node; null/empty → InspectedNode = null). 6 new tests pass.
  - **Step 04** — `StrategyKind` enum + `NodeInspectorViewModel` (Open/HydrateFromSource/save-on-change/AddToExtractAsync/RecomputeGraphAsync). Strategy radio mutates engine `ExtractStrategy` in-place, preserving Where. 9 new tests in `NodeInspectorViewModelTests`, all green.
  - **Step 05** — `NodeInspectorView.xaml(.cs)` (header + Strategy radio group + WhereFilterEditor via `SqlEditorView IsSingleLine="True"` + Excluded checkbox + Add-to-extract button + Recompute graph button). `NotNullToBoolConverter` + `NotNullToVisibilityConverter` added + registered. **Deviation:** uses a same-pane `Border` overlay (`HorizontalAlignment="Right"`, `Width=380`) inside `GraphView.xaml` rather than a `<mah:Flyout>`, because Flyouts must live inside `MetroWindow.Flyouts` and the Graph tab is a `UserControl`. Logged here per CLAUDE.md tripwire on architectural deviations. `MainWindowViewModel` now calls `Graph.Bind(SaveWorkspaceAsync)`; `GraphViewModel.OnInspectedNodeChanged` materialises a fresh `NodeInspectorViewModel` against the current workspace + script.
  - **Step 06** — `GraphHostView.RightClickedNodeId` DP added; `OnViewerMouseDown` uses `MsaglMouseEventArgs.RightButtonIsPressed` to dispatch left vs right click. `GraphViewModel` got 4 new commands (`ExcludeNode`/`IncludeNode`/`ResetNode`/`ShowOnWholeGraph`) + `RightClickedNodeId` observable. `GraphView.xaml` adds a `<ContextMenu>` resource on the host with menu items bound to the commands via `RelativeSource AncestorType=ContextMenu` + `PlacementTarget.DataContext` (item-level state gating deferred to v2 — items always shown). 6 new tests, all green.
  - **Step 07** — `GraphViewModel.AnchorScript` + `SetAnchor()`; `CollectSeeds` now uses `AnchorScript ?? FirstScript` instead of unioning all scripts. `MainWindowViewModel` subscribes to `Seed.PropertyChanged` for `SelectedScript` and calls `Graph.SetAnchor`. 4 new GraphViewModelTests + 1 new MainWindowViewModelGraphHydrationTest, all green.
  - **Step 08** — Mockup files M5/M6 marked implemented. `components.md` updated for `NodeInspector`, `StrategyPicker`, `WhereFilterEditor` (deferred-wrapper note), `GraphToolbar` (focus extension), `GraphNode` (+N badge), and `FKEdgeList`/`BuildDirectivesEditor` (deferred). `wpf-desktop.md` gained the click-dispatch pattern note + implementation-timing entry. `roadmap.md` moved `desktop-graph-tab` to ✅ Completed; `desktop-extras-tab` promoted to Proposed Next.
- 2026-05-03 — `dotnet build` 0 errors. `dotnet test` final tally: 282 unit tests pass, 4 pre-existing failures unchanged (all 4 are sample.bws drift from earlier UI smoke testing, flagged at session start as needing `git checkout examples/sample.bws`). New tests landed: 10 (step 01) + 6 (step 03) + 9 (step 04) + 6 (step 06) + 4 + 1 (step 07) = **36 new tests**, all green.
- 2026-05-03 — CLAUDE.md ↔ `.github/copilot-instructions.md` SHA256 parity verified: `F5364FAFBAE549B96D490C77CC57FE5AB943F0EEBCDE501E0825D0EB73D69CB9`.
- 2026-05-03 — Manual smoke deferred to operator. UI changes are XAML-only (per recipe, no unit tests for views) and the bound VM properties are covered by `GraphViewModelTests` + `NodeInspectorViewModelTests`. The 12-step smoke walk in step 08 is operator-driven; `examples/sample.bws` carries a single `PatientClearing` script which exercises steps 1-10 directly. Multi-script anchor sync (step 11) requires a second script — not added to the sample to keep portability; deferred per step 08's "If a multi-script demo is desired … this can also be deferred since multi-script is exercised by `desktop-seed-tab` already".
- 2026-05-03 — Audit fixes applied per `prompts/audit-checklist.md` review:
  - **Removed orphaned `NotNullToBoolConverter`** — the `<mah:Flyout IsOpen=…>` use case it was added for is gone (replaced by Border overlay using `NotNullToVis`); deleted the file + the `App.xaml` registration.
  - **`NodeInspectorViewModel._source` → `[NotifyCanExecuteChangedFor(nameof(AddToExtractCommand))]` + `[NotifyPropertyChangedFor(nameof(IsAddable))]`** — keeps `AddToExtractCommand.CanExecute` in sync with `IsAddable` automatically; removes the manual `OnSourceChanged` partial and the explicit `AddToExtractCommand.NotifyCanExecuteChanged()` call inside `AddToExtractAsync`.
  - **Inspector save-on-change is now debounced (500 ms default; `TimeSpan.Zero` in tests)** mirroring the `ScriptEditorViewModel` pattern. Cancels prior `_saveCts` on each edit; rapid Where-editor keystrokes coalesce into a single save. `NodeInspectorViewModelTests.Build()` accepts an optional debounce parameter and defaults to `TimeSpan.Zero` so the existing 9 assertions on `SaveCount==1` continue to pass.
  - 74/74 feature tests pass; build clean. Operator smoke (suggestion #4 — AGL re-layout reshuffle on per-expansion recompute) intentionally left for the operator pass.
