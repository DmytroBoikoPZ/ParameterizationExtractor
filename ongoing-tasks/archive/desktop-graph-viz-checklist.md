# Desktop Graph Viz

> **Status (2026-05-03): shipped, superseded by `desktop-graph-tab`.** The 10 steps below all completed and tests are green, but smoke testing exposed that "render the whole reachable subgraph" was the wrong v1 lens — for any real DB it's an unreadable wall of nodes. The reframed `desktop-graph-tab` ships **focus mode + click-to-explore + slide-in node inspector (M6)** as the operational Graph-tab experience. The ADR-012 library pick, `IGraphBuilder` engine seam, `Excluded` flag (model + wiring), `Controls/GraphHost/` AGL wrapper, and `NodeState`/`EdgeStyle` mapping are all reusable foundations consumed by `desktop-graph-tab` — that work is not wasted, just re-clothed.

## Goal

Replace the Graph-tab placeholder with a read-only FK-graph view (mockup [M5](../docs/design/desktop-ui/05-graph.md)) that shows tables reachable from the seed via FK edges, with per-node state badges (`✓` configured / `◯` pending / `✗` excluded) and per-edge styling (Follow / Stop / Pending). Closes ADR-008's deferred follow-up: pick the graph visualisation library (constraint already locked: must support edge-pick events for v2). First feature to land the `IGraphBuilder` engine seam, the `Controls/GraphHost/` AGL wrapper, and the `Excluded` flag on `TableToExtract` (engine model + wiring).

**v1 is complete:** node-state rendering covers all three states (`✓`, `◯`, `✗`); the engine actually skips excluded tables in the FK walk (not cosmetic). v1 does **not** include the slide-in inspector pane (M6 → `desktop-graph-tab`) or edge-click toggling (v2 → `desktop-graph-edge-click`).

## Scope

- **In scope:**
  - **Graph library pick** — `AutomaticGraphLayout` 1.1.12 (community-maintained Msagl fork; same API, more recent activity, edge-pick events supported per ADR-008 constraint). New ADR-012.
  - **Engine model: `Excluded` flag** on `TableToExtract` (`bool`, default `false`; XML/JSON-optional, JSON `WhenWritingDefault`). Pure-add metadata.
  - **Engine wiring** — `DependencyBuilder` skips excluded tables during FK walk; `Templates/DefaultTemplate.tt` does not emit them. New characterisation scenario `excluded-table` + golden so the behaviour is pinned.
  - **Engine seam — `IGraphBuilder`** in `ParameterizationExtractor.Logic/Schema/`. One method: `Task<ReachableGraph> BuildAsync(string connectionString, IReadOnlyList<TableRef> seedTables, CancellationToken ct)` returning `record ReachableGraph(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)` where each node is `record GraphNode(string Schema, string Name)` and each edge is `record GraphEdge(string FromSchema, string FromName, string ToSchema, string ToName, string ConstraintName)`. Impl: `MSSqlGraphBuilder` (uses existing `IObjectMetaDataProvider` for FK data — same query that powers `DependencyBuilder`). 10s connect timeout (mirrors `MSSQLConnectionTester`). Cancellation observed.
  - **Desktop `Controls/GraphHost/`** — `internal sealed partial class GraphHostView : UserControl` wrapping `AutomaticGraphLayout.Wpf.GraphViewer`. Three DPs: `Graph` (`ReachableGraph?`, two-way disabled), `Layout` (enum `GraphLayoutKind { Hierarchical, Layered, ForceDirected }`), `SelectedNodeId` (`string?`, two-way). AGL types confined inside this control. Recipe tripwire mirrors `SqlEditor`.
  - **`GraphViewModel`** (Singleton) — hydrates from workspace + connection string (via `WorkspaceConnectionStringBuilder` from `desktop-seed-tab`). Calls `IGraphBuilder.BuildAsync` with the seed tables collected from `Workspace.Package.Scripts[*].RootRecords[*]`. Computes per-node state by intersecting `ReachableGraph.Nodes` with `Workspace.Package.Scripts[*].TablesToProcess[*]`:
    - `Configured` — node appears in `TablesToProcess` (with any explicit ExtractStrategy)
    - `Pending` — node was reached via FK walk but absent from `TablesToProcess`
    - `Excluded` — node appears in `TablesToProcess` AND `TableToExtract.Excluded == true`
  - **`GraphNodeViewModel` + `GraphEdgeViewModel`** — visual state. Node: `Schema`, `Name`, `IsSeed`, `State` (enum `NodeState`), `StrategyChip` (e.g. `"FKDependency"`, `"OnlyChildren"`, `""` for pending). Edge: `FromNodeId`, `ToNodeId`, `Style` (enum `EdgeStyle { Follow, Stop, Pending }`).
  - **`GraphView`** — XAML hosting `<graph:GraphHostView/>` + toolbar (layout dropdown / Fit / +/- zoom / status pill `"17/372"`) + dock-right legend (matches mockup). Bindings to `GraphViewModel`.
  - **MainWindow wire-up** — `MainWindowViewModel` ctor gains 9th param `GraphViewModel`. `OnWorkspaceChanged` calls `Graph.Hydrate(value, plaintextForGraph)` (decrypts password identically to Seed). `MainWindow.xaml` Graph tab swaps placeholder → `<graph:GraphView DataContext="{Binding Graph}"/>`. Manual smoke walked end-to-end (logged in Notes).
  - **Tests** — engine: `MSSqlGraphBuilderTests` (reachability from a known seed, FK direction respected, isolated table excluded, malformed connection, cancellation), `DependencyBuilderTests` (Excluded skips emission), characterisation scenario `excluded-table`. Desktop: `GraphViewModelTests` (hydrate maps node states correctly per state matrix; refresh re-runs explorer; build failure surfaces as status). DI smoke for `IGraphBuilder` + `GraphViewModel`.
  - **Cross-doc updates** — ADR-012 added to `adr/readme.md`; `docs/architecture/overview.md` Desktop row mentions Graph tab + IGraphBuilder; `docs/methodology/wpf-desktop.md` gains `IGraphBuilder` impl-timing row + new "Graph host" section; `docs/design/desktop-ui/components.md` marks `GraphView` / `GraphNode` / `GraphLegend` / `GraphToolbar` Implemented; `docs/methodology/workspace-format.md` gains `tablesToProcess[].excluded` field; `docs/roadmap.md` moves feature to Completed and promotes `desktop-graph-tab`.

- **Out of scope:**
  - **Slide-in inspector pane (M6)** — strategy / Where / Follow-Stop edits per node land in `desktop-graph-tab`. v1 surfaces selection (`SelectedNodeId`) but does nothing with it.
  - **Edge-click toggling Follow/Stop** — v2 (`desktop-graph-edge-click`). The library choice already supports edge-pick events; v1 just doesn't wire them.
  - **Right-click context menu** — deferred.
  - **Self-referential FKs (table → itself) rendering** — engine returns the edge; v1 may render imperfectly. Documented as a known v1 limitation.
  - **Cycle visualisation** — engine breaks cycles by visit-tracking; UI v1 doesn't surface cycles specially.
  - **Per-workspace layout preference persistence** — layout is in-memory only; defaults to hierarchical.
  - **Auto-pan/zoom to "next pending" node** — deferred.
  - **Performance ceiling beyond ~100 nodes** — accept whatever AGL gives; tune in a future feature if it matters.
  - **Docking library** — still no, per ADR-008.

- **Dependencies:**
  - [`desktop-seed-tab`](./archive/desktop-seed-tab-checklist.md) — provides `WorkspaceConnectionStringBuilder`, `IUiDispatcher` discipline, the recipe-impl pattern.
  - [`engine-schema-aware-resolution`](./archive/engine-schema-aware-resolution-checklist.md) — `(Schema, Name)` tuples used throughout `GraphNode`, `GraphEdge`, and `TableRef`.
  - [`desktop-shell`](./archive/desktop-shell-checklist.md) — Graph-tab placeholder lives here.
  - ADR-008's locked constraint: chosen library **must** support edge-pick events.
  - Mockup [M5 Graph](../docs/design/desktop-ui/05-graph.md).
  - Components map: [GraphView](../docs/design/desktop-ui/components.md#graphview) / [GraphNode](../docs/design/desktop-ui/components.md#graphnode) / [GraphLegend](../docs/design/desktop-ui/components.md#graphlegend) / [GraphToolbar](../docs/design/desktop-ui/components.md#graphtoolbar).

## Architecture

See [feature-architecture.md](./desktop-graph-viz/feature-architecture.md) for the engine seam, the AGL isolation pattern, the node-state computation matrix, and the workspace → builder → renderer pipeline.

## Steps

- [x] [01 — ADR-012 + AutomaticGraphLayout NuGet](./desktop-graph-viz/01-desktop-graph-viz-adr-and-nuget.md)
- [x] [02 — Engine model: `Excluded` flag on `TableToExtract`](./desktop-graph-viz/02-desktop-graph-viz-excluded-flag-model.md)
- [x] [03 — Engine wiring: `DependencyBuilder` honours `Excluded` + characterisation scenario](./desktop-graph-viz/03-desktop-graph-viz-excluded-flag-wiring.md)
- [x] [04 — Engine seam: `IGraphBuilder` + `MSSqlGraphBuilder` + reachable-graph model](./desktop-graph-viz/04-desktop-graph-viz-graph-builder.md)
- [x] [05 — Desktop `Controls/GraphHost/` (AGL wrapper)](./desktop-graph-viz/05-desktop-graph-viz-graph-host-control.md)
- [x] [06 — `GraphViewModel` (Singleton, hydrate + state computation)](./desktop-graph-viz/06-desktop-graph-viz-graph-view-model.md)
- [x] [07 — `GraphNodeViewModel` + `GraphEdgeViewModel`](./desktop-graph-viz/07-desktop-graph-viz-node-and-edge-vms.md)
- [x] [08 — `GraphView` XAML + toolbar + legend + DataTemplates](./desktop-graph-viz/08-desktop-graph-viz-graph-view.md)
- [x] [09 — MainWindow wire-up + sample.bws update + manual smoke](./desktop-graph-viz/09-desktop-graph-viz-wire-up-and-smoke.md)
- [x] [10 — Cross-doc updates](./desktop-graph-viz/10-desktop-graph-viz-cross-docs.md)

## Notes

Free-form log of decisions, blockers, links to PRs. Dated entries preferred (YYYY-MM-DD).

- 2026-05-03 — Step 07 lookup deviation: `GraphViewModel.MapAndApply` falls back to bare-name lookup (empty config Schema matches any discovered Schema) per ADR-011's "empty Schema = bare-name resolution" policy. Without this, an operator's `TableToExtract("Patient", ...)` (no Schema specified) wouldn't match the discovered `("dbo", "Patient")` node and the table would render as Pending. The fallback restores expected behaviour for the common single-schema case.
- 2026-05-03 — Step 09 manual smoke: deferred to operator. WPF UI cannot be exercised from a CI/headless environment. End-to-end behaviour (workspace → connection-string → IGraphBuilder → state matrix → status) verified through 265 unit + integration tests (4 new graph-hydration tests).
- 2026-05-03 — Step 09 sample.bws note: existing `PatientClearing` script in `examples/sample.bws` already has `Patient` root + `LookupCountry` standalone, so the Graph tab will render the FK subgraph reachable from `Patient` against the test DB on first open. No edit required.
- 2026-05-03 — Step 10 CLAUDE.md ↔ copilot-instructions.md SHA256 sync: both `f0fe5df9b8fa4d99c25de32a13849e6814a07887098ac365a11b1513e8c52649` (untouched — recipe-level "AGL types confined to `Controls/GraphHost/`" rule mirrors the existing AvalonEdit tripwire; no new top-level tripwire required).
- 2026-05-03 — Step 03 deviation (audit follow-up): T4 template defensive guard NOT added. Reason: `DependencyBuilder` skips excluded tables before they reach `processedTables`, so the T4 emission loop never sees them. The `excluded-table` characterisation golden pins the behaviour (verified `grep -c TherapyProgramItems Goldens/excluded-table.sql == 0`). Adding the belt-and-braces guard would require threading `EmissionExcluded` through `PRecord` (mirrors `EmissionSchema`) and adding a `<# if (table.EmissionExcluded) continue; #>` line in `Templates/DefaultTemplate.tt`. Deferred — the trigger condition (walker regression that emits excluded tables) hasn't materialised, and the golden + 7 round-trip tests already protect against silent emission. Revisit if a future regression slips past the walker check.
- 2026-05-03 — Step 04 deviation (audit follow-up): `MSSqlGraphBuilder` does NOT call `IObjectMetaDataProvider` for FK data — it duplicates the SQL query as a private const `FkSql` byte-identical to `ObjectMetaDataProvider.sqlFKs`. Reason: `IObjectMetaDataProvider` depends on `IUnitOfWorkFactory`, which is configured at DI startup with a single connection string; the graph builder takes a per-call `connectionString` parameter (different workspace = different connection). Refactoring `IUnitOfWorkFactory` to accept a connection string per-use would ripple through `DependencyBuilder` + the rest of the engine — out of scope for `desktop-graph-viz`. The duplication is small (one const SQL string) and the two queries cannot drift silently because the builder's integration tests exercise the same FK data.
- 2026-05-03 — Step 08 deviation (audit follow-up): Toolbar Fit / +/- zoom buttons are NOT present (not even as `IsEnabled="False"` placeholders) — only Layout dropdown, Refresh, Cancel, and the status pill shipped. Reason: AGL exposes camera transforms (`GraphViewer.GraphCanvas.LayoutTransform` / `ContentBoundsToZoom`), but wiring them through DPs on `GraphHostView` adds a non-trivial design surface (one-shot `FitRequested` trigger vs two-way `Zoom: double`) that wasn't worth burning step-08 scope on. Documented in `docs/design/desktop-ui/components.md#GraphToolbar` with a "deferred to a follow-up feature" note. Revisit when an operator reports the missing controls hurt usability — `desktop-graph-tab` is the natural home (the inspector pane will exercise the same selection-driven camera flows).
- 2026-05-03 — **Superseded by `desktop-graph-tab`.** Operator smoke testing exposed that survey-mode rendering ("show the whole reachable subgraph") has near-zero operational value — for 328 tables × 490 FKs it's a wall of unreadable nodes. The reframed `desktop-graph-tab` ships focus mode + click-to-explore + the M6 inspector pane as the actionable experience. Operator quote: *"looks like epic, but has zero value now. I would expect that user is positioning to the root table and it is zoomed closely so user can start walking… click on some dependency table and it shows 1 level of tables around and etc."* Two diagnostic logs added during smoke (in `MSSQLConnectionTester.TestAsync` and `GraphViewModel.Hydrate`) are kept as standing operational logs — they helped surface a separate `PasswordBoxBindingBehavior` defaultValue bug (`""` → `null`) and a UX bug in `ConnectionEditorViewModel.Populate` (forced `StoreCredentials = false` when the workspace had no prior stored password, contradicting ADR-010's opt-out model). Both fixes shipped and are live in the Desktop. The v1 graph rendering itself works correctly; it's just the wrong rendering scope.
