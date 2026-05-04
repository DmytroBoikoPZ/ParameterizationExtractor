# 10 — desktop-graph-viz — Cross-doc updates

## Goal

Sync docs for the landed `IGraphBuilder` engine seam, the `Excluded` flag, the `Controls/GraphHost/` AGL wrapper, and the Graph tab. Move feature to Completed; promote `desktop-graph-tab` next.

## Track

`docs`.

## What to Build

### `adr/readme.md`

- ADR-012 row added in step 01. Verify it's still present and correct.

### `docs/architecture/overview.md`

- § 2 Components — Desktop row Purpose extended:
  > "Graph tab (M5) for read-only FK-subgraph visualization with per-node state badges (`✓` / `◯` / `✗`); first feature to land `IGraphBuilder` (Logic-side) and `Controls/GraphHost/` (`AutomaticGraphLayout` wrapper)."
- § 5 Cross-cutting — DI line: append `IGraphBuilder` (Singleton, Logic-side) to the Desktop singletons list.
- § 7 Active ADRs — add ADR-012 line.

### `docs/methodology/wpf-desktop.md`

- New § "Graph host" between "Database explorer" and "Connection management":
  > The Desktop visualises the FK subgraph through `IGraphBuilder` (`Logic/Schema/`) — never touches `SqlConnection` directly. The render surface is `Controls/GraphHost/GraphHostView`, a thin UserControl wrapping `AutomaticGraphLayout.Wpf.GraphViewer` (ADR-012). **Tripwire: AGL types live only inside `Controls/GraphHost/`.** ViewModels see only `ReachableGraph` (engine record), `GraphLayoutKind` (enum), and `SelectedNodeId` (string?). The `Viewer.MouseDown` ↔ `SelectedNodeId` bridge is the only place AGL types are visible — confined to `GraphHostView.xaml.cs` (mirrors the AvalonEdit isolation in `SqlEditor`).
- Implementation timing table — add row:
  > `| IGraphBuilder | desktop-graph-viz | landed (engine-side) | First FK-subgraph builder consumer in the Graph tab. Lives in Logic/Schema/ so the Desktop never touches SqlConnection directly. |`
- § How to add a screen pointer extended:
  > **Reference implementations:** ... `Views/Graph/` + `Controls/GraphHost/` (read-only graph view + third-party graph-library wrapper; `desktop-graph-viz`).

### `docs/design/desktop-ui/components.md`

Mark **Implemented** on these entries (with file paths):

- `GraphView` — `Desktop/Views/Graph/GraphView.xaml(.cs)`; bound to `GraphViewModel`.
- `GraphNode` (DataTemplate inside `GraphHostView` — note: rendering is delegated to AGL; `GraphNodeViewModel` carries the visual state for legend/tooltip). v1 limitation: AGL drives node rendering; per-node visual styling is applied via converters where AGL surfaces support it. Refine in `desktop-graph-tab` if the inspector needs richer per-node visuals.
- `GraphLegend` — inline in `GraphView.xaml`; static legend matching mockup.
- `GraphToolbar` — inline in `GraphView.xaml`; layout dropdown + Fit/+/- (some controls may be deferred to a follow-up — log in Notes).

### `docs/methodology/workspace-format.md`

- Add row to `tablesToProcess` table:
  > `| scripts[].tablesToProcess[].excluded | <TableToExtract Excluded=""> | bool | no — when true, engine skips this table during the FK walk and emits no SQL. Defaults to false. Added by desktop-graph-viz. |`

### `docs/roadmap.md`

- Move `desktop-graph-viz` to ✅ Completed:
  > ✅ desktop-graph-viz — FK-subgraph view (M5) with per-node state badges (`✓` / `◯` / `✗`) and per-edge styling (Follow / Stop / Pending); `IGraphBuilder` engine seam; `Controls/GraphHost/` (`AutomaticGraphLayout` wrapper, ADR-012); `Excluded` flag on `TableToExtract` (engine model + wiring); `excluded-table` characterisation scenario.
- Promote next: `desktop-graph-tab` (slide-in inspector pane M6) is the natural follow-up.

### `CLAUDE.md` audit

- New tripwire worth surfacing? The "AGL types confined to `Controls/GraphHost/`" rule mirrors the existing AvalonEdit rule. The recipe-level rule covers it; CLAUDE.md tripwires don't need a duplicate. Decision: **no new tripwire line.** Verify CLAUDE.md ↔ `.github/copilot-instructions.md` SHA256 unchanged; record in Notes.

### Verification

- `dotnet build` — 0 errors (sanity).
- `dotnet test` — green (sanity; no behavioural change from this step).
- Spot-check that all referenced docs exist and the new sections render in markdown viewers.

## Acceptance Criteria

- [ ] `adr/readme.md` index has ADR-012.
- [ ] `docs/architecture/overview.md` § 2 + § 5 + § 7 updated.
- [ ] `docs/methodology/wpf-desktop.md` has new "Graph host" section + impl-timing table row + reference-impls list extended.
- [ ] `docs/design/desktop-ui/components.md` — `GraphView`, `GraphNode`, `GraphLegend`, `GraphToolbar` marked Implemented.
- [ ] `docs/methodology/workspace-format.md` — `tablesToProcess[].excluded` field documented.
- [ ] `docs/roadmap.md` — feature in ✅ Completed; `desktop-graph-tab` promoted to Proposed next.
- [ ] CLAUDE.md / copilot-instructions.md SHA256 unchanged (or updated symmetrically + recorded).
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Touched docs: as listed above.
- Pattern exemplars: prior cross-docs steps (engine-schema-aware-resolution step 06; desktop-seed-tab step 07).
- Depends on: [09 — wire-up + smoke](./09-desktop-graph-viz-wire-up-and-smoke.md).
