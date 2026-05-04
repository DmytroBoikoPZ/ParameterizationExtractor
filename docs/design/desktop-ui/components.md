# Reusable components — composition map

> **Status:** placeholder. Identifies UI bits that appear on more than one screen so we don't duplicate them in code, and to surface the View / UserControl inventory before the `desktop-skeleton` feature gets scaffolded.
>
> **Out of scope here** (deferred to ADRs and feature-architecture docs): concrete WPF type, MVVM ViewModel shape, binding contracts, DI lifetime. Those land when the desktop project is scaffolded — this page exists only to flag *which* components warrant UserControl status and *where* they appear.
>
> Conventions:
> - One section per candidate component.
> - "Used in" links the screens where it appears.
> - "Variations" captures different modes the same component supports (e.g. read-only vs editable).
> - "Notes" can be empty until we know more.
>
> **Decisions baked into this map** (see [readme — Decisions already locked](./readme.md#decisions-already-locked)):
> - No `WorkspaceTree` (left explorer dropped, L2).
> - No `EdgeInteraction` overlay (edges are display-only in v1, L3).
> - `TabHost` carries 5 tabs: Overview / Seed / Graph / Extras / Run. No separate Scripts tab.

---

## Composition principles (placeholder)

> Fill in once the WPF stack ADR is drafted. Topics to cover here:
> - When does a thing become a UserControl vs. inline composition in the parent View?
> - Convention for `View` ↔ `ViewModel` pairing (e.g. `XxxView.xaml` + `XxxViewModel.cs` next to each other in `Views/Xxx/`).
> - How shared components get their dependencies (DI vs. parent-passes-VM).
> - Where DataTemplate selectors live for variants (e.g. strategy variants).
> - Theming / styling conventions.

---

## Inventory

### WorkspaceShell
- **Used in:** [01 — Startup](./01-startup.md), [03 — Main shell](./03-shell.md).
- **Variations:** empty-state (no workspace) vs. workspace-loaded.
- **Notes:** owns the `Window` chrome — menu bar, toolbar, status bar, dirty-state title.

### WorkspaceWelcome
- **Used in:** [01 — Startup](./01-startup.md).
- **Variations:** —
- **Notes:** the centred "No workspace open" panel with New / Open / Recent.

### TabHost
- **Used in:** [03 — Main shell](./03-shell.md).
- **Variations:** —
- **Notes:** tab strip + active tab content host. Each tab content is one of: [OverviewView](#overviewview), [SeedView](#seedview), [GraphView](#graphview), [ExtrasView](#extrasview), [ScriptsView](#scriptsview), [RunView](#runview).

### OverviewView
- **Used in:** [03 — Main shell](./03-shell.md).
- **Variations:** —
- **Notes:** read-only summary tab. Tab-specific, not reused — listed for completeness.

### ConnectionIndicator
- **Used in:** [03 — Main shell](./03-shell.md) (toolbar pill).
- **Variations:** healthy / disconnected / connecting.
- **Notes:** clickable; opens [ConnectionEditor](#connectioneditor) in "edit current" mode.

### DirtyTitleBar
- **Used in:** [03 — Main shell](./03-shell.md).
- **Variations:** —
- **Notes:** prepends `*` when the workspace has unsaved changes. Likely a tiny attached behaviour, not a full UserControl.

### ConnectionEditor
- **Used in:** [02 — New workspace](./02-new-workspace.md), [08 — Run](./08-run.md) (target connection), [03 — Main shell](./03-shell.md) (via [ConnectionIndicator](#connectionindicator)).
- **Variations:** Windows auth (hides user/pwd) vs. SQL auth (shows user/pwd); editable vs. read-only.
- **Notes:** server / db / auth / user / pwd fields + "Test connection" action. Same control reused across screens.

### PathPicker
- **Used in:** [02 — New workspace](./02-new-workspace.md), [08 — Run](./08-run.md) ("Save to disk…").
- **Variations:** file vs. folder.
- **Notes:** textbox + browse button.

### TablePicker
- **Status:** Implemented at `ParameterizationExtractor.Desktop/Controls/TablePicker/` (View + ViewModel) since `desktop-seed-tab`.
- **Used in:** [04 — Seed](./04-seed.md) (root table), [07 — Extras](./07-extras.md) (add standalone table).
- **Variations:** —
- **Notes:** combobox of tables from the loaded source schema. Items are `TableRef(Schema, Name)` from `IDatabaseExplorer.ListTablesAsync`. The Seed tab does not nest the `TablePickerView` — it inlines a `<ComboBox>` bound to `SeedViewModel.Tables` to share one source-of-truth table list across all script editors. The control remains available for the Extras tab.

### SqlEditor
- **Status:** Implemented at `ParameterizationExtractor.Desktop/Controls/SqlEditor/SqlEditorView.xaml(.cs)` since `desktop-seed-tab`. Wraps AvalonEdit (6.3.1.120). Three DPs: `Text` (string, two-way), `IsReadOnly` (bool), `IsSingleLine` (bool).
- **Used in:** [04 — Seed](./04-seed.md) (seed query), [06 — Node inspector](./06-node-inspector.md) (where filter, single-line), [07 — Extras](./07-extras.md) (scripts, multi-line), [08 — Run](./08-run.md) (.sql preview, read-only).
- **Variations:** single-line vs. multi-line; editable vs. read-only.
- **Notes:** the foundational text-editor control. AvalonEdit types live ONLY inside `SqlEditorView.xaml.cs` (tripwire). T-SQL highlighting registered once in `App.xaml.cs` via `HighlightingManager.Instance.GetDefinition("TSQL")`. Wrapped by [WhereFilterEditor](#wherefiltereditor), [ScriptEditor](#scripteditor), [SqlOutputPreview](#sqloutputpreview) (none yet built — `desktop-graph-tab` and `desktop-extras-tab` respectively).

### WhereFilterEditor
- **Used in:** [06 — Node inspector](./06-node-inspector.md), [07 — Extras](./07-extras.md).
- **Variations:** —
- **Notes:** thin wrapper over [SqlEditor](#sqleditor) configured single-line, with light validation hints.

### ScriptEditor
- **Status:** Deferred to `desktop-extras-scripts`.
- **Used in:** [07 — Extras](./07-extras.md).
- **Variations:** —
- **Notes:** thin wrapper over [SqlEditor](#sqleditor) configured multi-line, with name field.

### SqlOutputPreview
- **Used in:** [08 — Run](./08-run.md).
- **Variations:** —
- **Notes:** read-only [SqlEditor](#sqleditor) bound to the engine's generated `.sql` output.

### RowsPreviewGrid
- **Status:** Implemented at `ParameterizationExtractor.Desktop/Controls/RowsPreviewGrid/RowsPreviewGridView.xaml(.cs)` since `desktop-seed-tab`. Single DP: `Result` (`PreviewResult?`); columns auto-generated from `Result.ColumnNames` on assignment.
- **Used in:** [04 — Seed](./04-seed.md).
- **Variations:** —
- **Notes:** read-only `DataGrid` for query results. Bounded row count (200 in the Seed tab). Truncation banner shown when `PreviewResult.Truncated == true`.

### GraphView
- **Status:** Implemented at `Desktop/Views/Graph/GraphView.xaml(.cs)` since `desktop-graph-viz`. Hosts the toolbar, the legend, and `<graphHost:GraphHostView/>` (the AGL wrapper). Bound to `GraphViewModel` (Singleton).
- **Used in:** [05 — Graph](./05-graph.md).
- **Variations:** —
- **Notes:** rendering is delegated to `Controls/GraphHost/GraphHostView` — a thin wrapper over `Microsoft.Msagl.WpfGraphControl.GraphViewer` per ADR-012. AGL types live ONLY inside `Controls/GraphHost/` (recipe tripwire, mirrors AvalonEdit isolation in `SqlEditor`).

### GraphNode
- **Status:** Implemented as `GraphNodeViewModel` (`Desktop/Views/Graph/`) since `desktop-graph-viz`. Extended in `desktop-graph-tab` with `UnexpandedNeighbourCount` (the `+N` badge driven by VM state; appended to the node label by `GraphHostView.RenderFromVisible`). AGL drives the in-canvas rendering; per-state visual polish (colours / shapes inside AGL) deferred.
- **Used in:** [05 — Graph](./05-graph.md).
- **Variations:** seed / configured / pending / excluded — surfaced via `NodeState` enum on the VM.
- **Notes:** `GraphEdgeViewModel` is the per-edge analogue with the `EdgeStyle` enum (Follow / Stop / Pending) computed from endpoint states.

### GraphLegend
- **Status:** Implemented inline in `Desktop/Views/Graph/GraphView.xaml` since `desktop-graph-viz`. Static content matching mockup M5.
- **Used in:** [05 — Graph](./05-graph.md).
- **Variations:** —
- **Notes:** small legend panel; static content.

### GraphToolbar
- **Status:** Implemented inline in `Desktop/Views/Graph/GraphView.xaml`. Extended in `desktop-graph-tab` with the **Focus** radio group (Seed + 1 hop / Seed + 2 hops / Whole subgraph) bound to `GraphViewModel.FocusModeChoice` via `EnumMatchConverter`, plus the **Reset focus** button. Layout dropdown bound to `GraphViewModel.LayoutChoice` + Refresh / Cancel buttons + status pill (`{configured}/{total}` format) carry over from `desktop-graph-viz`. Fit / +/- zoom buttons deferred.
- **Used in:** [05 — Graph](./05-graph.md).
- **Variations:** —
- **Notes:** focus radio, reset focus, layout dropdown, refresh, cancel, status. Fit / zoom deferred.

### NodeInspector
- **Status:** Implemented at `Desktop/Views/Graph/NodeInspectorView.xaml(.cs)` since `desktop-graph-tab`. Bound to `NodeInspectorViewModel` (constructed by `GraphViewModel` when `InspectedNode` is set; transient per inspected node). Implementation deviation: a same-pane `Border` overlay (HorizontalAlignment=Right) rather than a `<mah:Flyout>`, because Flyouts must live inside `MetroWindow.Flyouts` and the Graph tab is a `UserControl`. Visibility bound via the `NotNullToVis` converter on `GraphViewModel.InspectorVm`. Refactored in `desktop-extras-tab` to compose `<strategy:StrategyPickerView>` + `<whereEditor:WhereFilterEditorView>` instead of inlining the radio group / single-line `SqlEditor`.
- **Used in:** [06 — Node inspector](./06-node-inspector.md).
- **Variations:** Pending (shows Add-to-extract button) vs. Configured/Excluded (shows Strategy / Where / Excluded edit fields).
- **Notes:** Hosts [StrategyPicker](#strategypicker), [WhereFilterEditor](#wherefiltereditor), and the Excluded checkbox. [FKEdgeList](#fkedgelist) and [BuildDirectivesEditor](#builddirectiveseditor) deferred to a follow-up — engine model already supports them but inspector v1 doesn't expose them.

### StrategyPicker
- **Status:** Implemented at `Controls/StrategyPicker/StrategyPickerView.xaml(.cs)` since `desktop-extras-tab` (extracted from the inlined radio group inside `NodeInspectorView`). Two DPs: `StrategyChoice` (`StrategyKind`, two-way) + `GroupName` (string, auto-defaults to a per-instance unique GUID so multiple pickers in the same window don't share radio state). The `StrategyKind` enum lives at `Controls/StrategyPicker/StrategyKind.cs`. Bindings use the existing `EnumMatchConverter`. Each radio includes a one-line tooltip explaining the strategy.
- **Used in:** [06 — Node inspector](./06-node-inspector.md), [07 — Extras](./07-extras.md).
- **Variations:** —
- **Notes:** four-radio chooser for `FKDependency` / `OnlyChildren` / `OnlyParent` / `OnlyOneTable`. Mutates the engine's `TableToExtract.ExtractStrategy` in-place via the host VM's save-on-change wiring.

### WhereFilterEditor
- **Status:** Implemented at `Controls/WhereFilterEditor/WhereFilterEditorView.xaml(.cs)` since `desktop-extras-tab` (extracted from the inlined `<editor:SqlEditorView IsSingleLine="True"/>` inside `NodeInspectorView`). Single DP: `Text` (string, two-way). Wraps `SqlEditorView` in single-line mode.
- **Used in:** [06 — Node inspector](./06-node-inspector.md), [07 — Extras](./07-extras.md).
- **Variations:** —
- **Notes:** thin wrapper over [SqlEditor](#sqleditor) configured single-line. Light validation hints deferred — the engine surfaces SQL errors at preview/run time.

### FKEdgeList
- **Status:** Deferred. Engine model already supports per-strategy `DependencyToExclude` (the per-edge skip list), but inspector v1 doesn't expose it — the UX is fiddly enough to warrant its own feature.
- **Used in:** [06 — Node inspector](./06-node-inspector.md).
- **Variations:** —
- **Notes:** Parents / Children grouped list with Follow/Stop dropdowns per edge.

### BuildDirectivesEditor
- **Status:** Deferred. Engine emits `INSERT` today; the T4 template already has the strategy hook for `UPDATE`. Inspector v1 doesn't expose the toggles.
- **Used in:** [06 — Node inspector](./06-node-inspector.md).
- **Variations:** —
- **Notes:** INSERT/UPDATE checkboxes + identity-insert tri-state.

### ExtrasView
- **Status:** Implemented at `Views/Extras/ExtrasView.xaml(.cs)` since `desktop-extras-tab`. Bound to `ExtrasViewModel` (Singleton). Hosts the standalone-tables `ItemsControl` with per-row inline expander (`StrategyPickerView` + `WhereFilterEditorView` + Excluded checkbox), "+ Add table…" button, and the empty-hint `TextBlock`.
- **Used in:** [07 — Extras](./07-extras.md).
- **Variations:** —
- **Notes:** Per-row VM is `StandaloneTableViewModel` (Transient, one per row). Reserved Auto row at the bottom of the outer Grid for the script section landing in `desktop-extras-scripts`.

### ScriptList
- **Status:** Deferred to `desktop-extras-scripts`.
- **Used in:** [07 — Extras](./07-extras.md).
- **Variations:** pre / post.
- **Notes:** ordered list of script entries with name + body preview, with up/down/edit/remove actions.

### RunModePicker
- **Used in:** [08 — Run](./08-run.md).
- **Variations:** —
- **Notes:** three-radio mode chooser (dry-run / generate / execute).

### TargetConnectionPicker
- **Used in:** [08 — Run](./08-run.md).
- **Variations:** —
- **Notes:** link/popover that opens [ConnectionEditor](#connectioneditor) for the target.

### PerTableBreakdownGrid
- **Used in:** [08 — Run](./08-run.md).
- **Variations:** —
- **Notes:** per-table row/byte breakdown `DataGrid` with totals row.

### RunLogView
- **Used in:** [08 — Run](./08-run.md).
- **Variations:** —
- **Notes:** append-only log pane bound to a Serilog UI sink.

---

## TODO before this becomes a spec

- For each entry: stub the ViewModel inputs/outputs (placeholder properties, commands).
- Decide which entries are full UserControls vs. inline composition vs. just DataTemplates.
- Mark ones that need DataTemplate selectors (e.g. [GraphNode](#graphnode) variants).
- Cross-check with the workspace JSON schema once that lands — every editable view must map cleanly to a JSON node.
- Add a "Tests" column or section noting which components warrant a ViewModel-level test.
