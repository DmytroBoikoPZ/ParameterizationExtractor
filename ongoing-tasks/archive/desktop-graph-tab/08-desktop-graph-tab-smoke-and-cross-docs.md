# 08 — desktop-graph-tab — Manual smoke + sample.bws update + cross-doc updates

## Goal

End-to-end smoke against `examples/sample.bws`; update sample if needed; sync docs (M5/M6 mockups → implemented; components.md statuses; recipe note about click-dispatch + inspector pane; roadmap).

## Track

`docs` + `desktop` (sample workspace).

## What to Build

### Manual smoke (mandatory — operator-driven)

Walk the full M5 + M6 flow against `examples/sample.bws`:

1. App opens; switch to Graph tab → seed (Patient) + 1 hop visible. Each Pending neighbour shows `+N` badge.
2. Click a Pending neighbour (e.g. `Visit`) → expands. New layer appears.
3. Click `Visit` again (now Configured if it was added; otherwise still Pending and just re-expands a no-op).
4. Click `Patient` (Configured) → inspector slides in. Strategy = `FKDependency`. Where = empty.
5. Change Where to `Id < 1000` → persists; re-open workspace → still there.
6. Toggle Excluded → node turns ✗ + edges dash. Untoggle.
7. Pick a Pending node → inspector shows Add-to-extract button. Click → entry created; node turns ✓; graph recomputes.
8. Click Reset focus → returns to seed + 1 hop.
9. Switch focus radio to "Whole subgraph" → all reachable nodes visible. Switch back to Seed + 1 hop → previous focus mode.
10. Right-click on a Configured node → context menu: Exclude / Reset to Pending / Show on whole graph. Click Exclude → flips state.
11. Switch to Seed tab; in a multi-script workspace, change `SelectedScript` → switch back to Graph tab; anchor follows.
12. Close + reopen workspace → all edits round-trip.

Log outcome with date in checklist Notes.

### `examples/sample.bws` update

The current sample has one script (`PatientClearing`) with `Patient` root + `LookupCountry` standalone. Sufficient for smoke.

If a multi-script demo is desired (to exercise step 07's anchor sync), add a second script — but this can also be deferred since multi-script is exercised by `desktop-seed-tab` already.

### Cross-doc updates

#### `docs/design/desktop-ui/05-graph.md`

- Status header → `**implemented (2026-XX-YY)**`.
- Mockup notes update: focus mode is the default; click-dispatch is wired; inspector pane is the click payoff.

#### `docs/design/desktop-ui/06-node-inspector.md`

- Status header → `**implemented**`.
- Confirm the v1 inspector covers Strategy / Where / Excluded; defer notes for `BuildDirectivesEditor` (INSERT/UPDATE checkboxes) and `FKEdgeList` (per-edge Follow/Stop) per scope.

#### `docs/design/desktop-ui/components.md`

Mark Implemented (with paths):

- `NodeInspector` — `Desktop/Views/Graph/NodeInspectorView.xaml(.cs)`; bound to `NodeInspectorViewModel`.
- `StrategyPicker` — inline radio group inside `NodeInspectorView.xaml`.
- `WhereFilterEditor` — `<editor:SqlEditorView IsSingleLine="True"/>` reused inside `NodeInspectorView.xaml`. Notes: thin wrapper deferred until a second consumer needs it.
- `GraphToolbar` — Focus radio + Reset focus + layout dropdown + Refresh + Cancel + status pill (extended in this feature).
- `GraphNode` — note that visual styling of in-canvas nodes is delegated to AGL; the `+N` badge is driven by VM state. Per-state visual polish (colours/shapes inside AGL) deferred.

Defer entries: `BuildDirectivesEditor`, `FKEdgeList` — flag as "deferred follow-up; engine model already supports them but inspector v1 doesn't expose them."

#### `docs/methodology/wpf-desktop.md`

- Add a small note in "Service abstractions" or "How to add a screen" about the **click-dispatch pattern** demonstrated by Graph tab: single click is contextual (different state → different action) and `SelectedNodeId` is the single seam between the host control and the dispatch logic. Useful precedent for future graph features.
- Reference `desktop-graph-tab` in the implementation-timing table where applicable.

#### `docs/roadmap.md`

- Move `desktop-graph-tab` to ✅ Completed:
  > ✅ desktop-graph-tab — Focus + click-to-explore graph view (M5 revised) with slide-in node inspector (M6); reframes `desktop-graph-viz` v1's survey lens. Operators walk the FK graph by clicking nodes; inspector edits Strategy / Where / Excluded per node; right-click context menu for cross-cutting actions; seed-tab `SelectedScript` drives the graph anchor for single-context view.
- Promote next item — likely `desktop-extras-tab` (M7 — Standalone-table editor + pre/post-script editor) or `engine-cancellation-token` from the Engine/CLI track.

#### `CLAUDE.md` audit

No new top-level tripwires. The recipe-level rules (AGL types confined to `Controls/GraphHost/`; VMs never see WPF types) apply unchanged. Verify SHA256 of CLAUDE.md ↔ `.github/copilot-instructions.md` unchanged; record in Notes.

### Verification

- `dotnet build` 0 errors; `dotnet test` green.
- Manual smoke walked end-to-end. Logged in Notes with date.

## Acceptance Criteria

- [ ] Manual smoke complete; checklist Notes has a dated entry with bullets per the 12-step walk.
- [ ] M5 + M6 mockup files marked implemented.
- [ ] `components.md` updated for `NodeInspector`, `StrategyPicker`, `WhereFilterEditor`, `GraphToolbar`, `GraphNode`.
- [ ] `wpf-desktop.md` mentions the click-dispatch pattern as a graph-feature precedent.
- [ ] `roadmap.md` — feature in ✅ Completed; next promoted.
- [ ] CLAUDE.md SHA unchanged.
- [ ] `dotnet build` / `dotnet test` green.

## References

- Touched docs: as listed.
- Pattern exemplar for cross-docs steps: `desktop-graph-viz` step 10, `desktop-seed-tab` step 07.
- Depends on: [01](./01-desktop-graph-tab-focus-filter.md), [02](./02-desktop-graph-tab-focus-toolbar.md), [03](./03-desktop-graph-tab-click-dispatch.md), [04](./04-desktop-graph-tab-inspector-vm.md), [05](./05-desktop-graph-tab-inspector-view.md), [06](./06-desktop-graph-tab-context-menu.md), [07](./07-desktop-graph-tab-seed-anchor-sync.md).
