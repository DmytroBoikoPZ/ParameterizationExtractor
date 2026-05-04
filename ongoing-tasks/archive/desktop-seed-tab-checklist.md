# Desktop Seed Tab

## Goal

Replace the placeholder text on the M3 shell's Seed tab with a real authoring surface (mockup [M4](../docs/design/desktop-ui/04-seed.md)) that lets the operator manage **multiple scripts**, each with a `Schema.TableName` root, an editable seed query (AvalonEdit), and a bounded preview of the result rows. First feature to ship `IUiDispatcher` (recipe pre-spec → implemented), the foundational `Controls/SqlEditor/` AvalonEdit wrapper, the `Controls/TablePicker/` reusable combobox, and the engine-side `IDatabaseExplorer` seam (table listing + bounded preview).

Depends on `engine-schema-aware-resolution` (must execute first): the table picker shows `Schema.TableName` and `IDatabaseExplorer.ListTablesAsync` returns schema-aware metadata.

## Scope

- **In scope:**
  - **Engine seam — `IDatabaseExplorer`** in `ParameterizationExtractor.Logic/Schema/`. Two methods: `Task<IReadOnlyList<TableRef>> ListTablesAsync(string connectionString, CancellationToken ct)` returning `record TableRef(string Schema, string Name)`; and `Task<PreviewResult> PreviewQueryAsync(string connectionString, string sql, int maxRows, CancellationToken ct)` returning `record PreviewResult(IReadOnlyList<string> ColumnNames, IReadOnlyList<string?[]> Rows, bool Truncated)` (everything stringified by the engine — no ADO.NET types leak). Impl: `MSSqlDatabaseExplorer`. 10s connection timeout (mirrors `MSSQLConnectionTester`); cancellation token observed and propagates.
  - **`IUiDispatcher`** + WPF impl + `FakeUiDispatcher` per recipe pre-spec. `Services/Threading/{IUiDispatcher.cs, UiDispatcher.cs}`; the WPF impl wraps `Application.Current.Dispatcher` with the canonical comment. Singleton.
  - **`Controls/SqlEditor/{SqlEditorView.xaml(.cs)}`** — AvalonEdit wrapper per [recipe](../docs/methodology/wpf-desktop.md) and [components map](../docs/design/desktop-ui/components.md#sqleditor). Two dependency properties: `Text` (string, two-way) and `IsReadOnly` (bool). Hides every `ICSharpCode.AvalonEdit.*` type. Loads the SQL syntax-highlighting definition once in `App.xaml.cs` `OnStartup` (registered with `HighlightingManager`); references it by name from the control. Single-line vs multi-line behaviour exposed via a `IsSingleLine` DP (multi-line is the default — single-line is for `WhereFilterEditor` later).
  - **`Controls/TablePicker/{TablePickerView.xaml(.cs), TablePickerViewModel.cs}`** — reusable combobox per [components map](../docs/design/desktop-ui/components.md#tablepicker). Bound to an `ObservableCollection<TableRef>`; selected item is `TableRef?`; display formatted as `"{Schema}.{Name}"`. Searchable / filterable via the built-in WPF `ComboBox.IsEditable` + `IsTextSearchEnabled`. Surfaces a `RefreshCommand` that re-runs `IDatabaseExplorer.ListTablesAsync`.
  - **`Controls/RowsPreviewGrid/`** — thin wrapper over `DataGrid` that materialises `PreviewResult` columns dynamically. Auto-generated columns are off; we generate them from `PreviewResult.ColumnNames`. Read-only. Truncation indicator banner ("Showing 200 of N+ rows; refine the query to see more") when `PreviewResult.Truncated == true`.
  - **`Views/Seed/{SeedView.xaml(.cs), SeedViewModel.cs, ScriptEditorViewModel.cs}`** — third canonical View↔VM pair. Layout (matches M4 mockup, extended for multi-script):
    - **Left rail**: `ListBox` of `Scripts` (`ObservableCollection<ScriptEditorViewModel>`); each item shows `ScriptName`. Toolbar above: `[ + Add ]` `[ - Remove ]` `[ Rename ]` (the latter via inline edit on double-click; or a small modal — pick simplest at impl time).
    - **Right pane** (visible when `SelectedScript != null`): `TablePicker` for root + `SqlEditor` (multi-line) for seed query + `[ Run preview ]` / `[ Cancel ]` button + `RowsPreviewGrid`.
    - Empty-state when no scripts: "No scripts yet. Click + Add to create one."
  - **`SeedViewModel`** (Singleton, lifetime per recipe table for tab VMs):
    - `ObservableCollection<ScriptEditorViewModel> Scripts`.
    - `[ObservableProperty] ScriptEditorViewModel? _selectedScript;`.
    - `AddScriptCommand` (creates a fresh `Script` named `"NewScript{N}"` with empty root + empty query); `RemoveScriptCommand` (gated on selection); `RenameScriptCommand`.
    - Hydrated from `Workspace.Package.Scripts` whenever the host workspace changes; **preserved** across tab switches because it's Singleton.
  - **`ScriptEditorViewModel`** (Transient — one per `Script` in the package):
    - `[ObservableProperty] string _scriptName;` `_rootSchema;` `_rootTable;` `_seedQuery;`.
    - `[ObservableProperty] PreviewResult? _previewResult;` (nullable; empty until first run).
    - `[ObservableProperty] string _previewStatus;` (`""` / `"Loading…"` / `"42 rows"` / `"Failed: ..."` / `"Cancelled"`).
    - `[RelayCommand(IncludeCancelCommand = true)] private async Task RunPreviewAsync(CancellationToken ct)` — calls `IDatabaseExplorer.PreviewQueryAsync` on a `Task.Run` continuation, then `_ui.InvokeAsync` to assign `PreviewResult` back on the dispatcher thread (this is the `IUiDispatcher` debut).
    - `RootTable` setter writes through the `TablePicker`'s `TableRef?`; serialisation maps to `Schema` + `Name` separately.
  - **Save-on-blur model** — every `[ObservableProperty]` setter on `ScriptEditorViewModel` (except transient `PreviewResult` / `PreviewStatus`) triggers a debounced (500ms-ish) write back into `Workspace.Package.Scripts[i]` followed by `IWorkspaceStore.SaveAsync(Workspace, _currentPath)`. The debounce uses a `CancellationTokenSource` that's swapped per-keystroke; pure VM logic, no `Dispatcher.Invoke`. Save failures (read-only file, etc.) surface via `IDialogService.ShowMessageAsync` and the in-memory edit stays so the operator doesn't lose work. Decision: save-on-blur from any field on the selected script; no dirty-marker UI.
  - **Wire-up** — `MainWindowViewModel` ctor takes `SeedViewModel`; `MainWindow.xaml` Seed tab content swaps from `<TextBlock>` placeholder to `<seed:SeedView DataContext="{Binding Seed}" />`. When `Workspace` changes, `SeedViewModel.Hydrate(Workspace)` is called from `OnWorkspaceChanged`.
  - **Schema highlighting registration** — `App.xaml.cs` `OnStartup` registers AvalonEdit's built-in T-SQL highlighting definition once. SqlEditor references it by name `"TSQL"`.
  - **Tests** — engine: `MSSqlDatabaseExplorerTests` (list tables, preview rows, truncation, cancellation, malformed SQL). Desktop: `UiDispatcherTests` (DI smoke + fake-runs-inline guarantee), `SeedViewModelTests` (hydrate from workspace, add/remove scripts, selection, persistence on blur), `ScriptEditorViewModelTests` (preview success/failure/cancellation, status transitions, save-on-blur). DI smoke for the new services + reusable controls.
  - **Cross-doc updates** — `adr/readme.md` (no new ADR — no new constraint, only impls of recipe pre-specs); `docs/architecture/overview.md` (Desktop row mentions Seed tab + IUiDispatcher landed); `docs/methodology/wpf-desktop.md` (IUiDispatcher status → implemented; new § "Database explorer"; SqlEditor + TablePicker reference impls); `docs/design/desktop-ui/components.md` (SqlEditor / TablePicker / RowsPreviewGrid status → implemented); `docs/roadmap.md` (move feature to Completed; promote next item).

- **Out of scope:**
  - **Per-script TablesToProcess editing** — strategy graph + Where filters per node land in `desktop-graph-tab` and `desktop-graph-viz`. The Seed tab edits only `Script.ScriptName / RootSchema / RootTable / SeedQuery`. The strategy graph follows the engine's defaults until the graph tab lands.
  - **Standalone tables / pre-post scripts** — `desktop-extras-tab` territory.
  - **SQL syntax validation** — AvalonEdit highlights but doesn't validate. Operator's first signal is the preview's "Failed: ..." status.
  - **Connection re-auth prompt** when stored password is missing/decrypt-failed — surfaces a `IDialogService.ShowMessageAsync` directing the operator to Edit-connection. We do NOT pop the Edit-connection dialog automatically.
  - **`sys.databases` enumeration** in `IDatabaseExplorer` — we list tables from one DB (the workspace's `WorkspaceSource.Database`).
  - **Auto-refresh of table list** when the connection changes — operator clicks the refresh button on the table picker. (We could subscribe to `Workspace` changes; defer until needed.)
  - **Save-As / explicit Save command / dirty marker** — still future work. Save-on-blur + the Edit-connection save-back are the only save call sites.
  - **Multi-row column type rendering** — preview values are stringified by the engine. Type-aware rendering (dates, booleans, BLOBs) deferred.

- **Dependencies:**
  - [`engine-schema-aware-resolution`](./engine-schema-aware-resolution-checklist.md) — **must execute first.** `IDatabaseExplorer.ListTablesAsync` returns `(Schema, Name)` tuples; the Seed tab assumes the engine model carries `Schema`. Without this, the table picker can't render schema-prefixed entries and the engine can't resolve schema-qualified config.
  - [`desktop-shell`](./archive/desktop-shell-checklist.md) — Seed tab placeholder lives here.
  - [`desktop-connection-management`](./archive/desktop-connection-management-checklist.md) — `IConnectionTester` already proved the no-`SqlConnection`-outside-Logic discipline. `IDatabaseExplorer` follows the same pattern.
  - Recipe pre-spec for `IUiDispatcher` ([wpf-desktop.md § Service abstractions](../docs/methodology/wpf-desktop.md)).
  - Components map: [TablePicker](../docs/design/desktop-ui/components.md#tablepicker) + [SqlEditor](../docs/design/desktop-ui/components.md#sqleditor) + [RowsPreviewGrid](../docs/design/desktop-ui/components.md#rowspreviewgrid).
  - Mockup [M4 Seed](../docs/design/desktop-ui/04-seed.md).

## Architecture

See [feature-architecture.md](./desktop-seed-tab/feature-architecture.md) for the multi-script VM hierarchy, the dispatcher-marshalling pattern, the AvalonEdit isolation, the save-on-blur lifecycle, and the engine seam.

## Steps

- [x] [01 — Engine seam: IDatabaseExplorer + MSSqlDatabaseExplorer](./desktop-seed-tab/01-desktop-seed-tab-database-explorer.md)
- [x] [02 — IUiDispatcher + WPF impl + FakeUiDispatcher](./desktop-seed-tab/02-desktop-seed-tab-ui-dispatcher.md)
- [x] [03 — Controls/SqlEditor (AvalonEdit wrapper) + Controls/TablePicker + Controls/RowsPreviewGrid](./desktop-seed-tab/03-desktop-seed-tab-reusable-controls.md)
- [x] [04 — ScriptEditorViewModel (single-script editor with preview + save-on-blur)](./desktop-seed-tab/04-desktop-seed-tab-script-editor-vm.md)
- [x] [05 — SeedViewModel (multi-script container) + WorkspaceConnectionStringBuilder](./desktop-seed-tab/05-desktop-seed-tab-seed-vm-container.md)
- [x] [06 — SeedView + MainWindow wire-up + sample workspace + manual smoke](./desktop-seed-tab/06-desktop-seed-tab-view-and-wire-up.md)
- [x] [07 — Cross-doc updates](./desktop-seed-tab/07-desktop-seed-tab-cross-docs.md)

## Notes

Free-form log of decisions, blockers, links to PRs. Dated entries preferred (YYYY-MM-DD).

- 2026-05-03 — Step 04 model touchpoint: chose **option (a) augment** the engine model. M4 mockup shows the seed query as a full SELECT (including FROM/WHERE), so a free-form `Query` field is needed. Added `string Query` to `SourceForScript` (XML-ignored, JSON-optional via `[JsonIgnore(WhenWritingNull)]`). UI-only persistence in v1 — engine still drives the FK walk from `RootRecords[i].Schema/TableName/Where`. Future feature can wire `Query` into the seed path. Pure-add, back-compat preserved (defaults to null on legacy XML / DSL workspaces).
- 2026-05-03 — Step 04 root mapping: chose **option (b) VM-side translation** for `RootSchema`/`RootTable` ↔ `RecordsToExtract[0].Schema/TableName` (one-root-per-script v1; `WriteThroughToSource` ensures the list has at least one entry).
- 2026-05-03 — Step 05 deviation from step 04 spec: added `internal SourceForScript Source { get; }` getter to `ScriptEditorViewModel` so `SeedViewModel.RemoveSelectedScriptAsync` can identify the engine record by reference when removing it from `Workspace.Package.Scripts` (anticipated by step 05's "Important model touchpoint").
- 2026-05-03 — Step 06 picker shape: chose **single shared `Tables` collection on `SeedViewModel`** (refreshed once per workspace via `IDatabaseExplorer.ListTablesAsync` after `Hydrate`) over a per-script `TablePickerViewModel`. The DataTemplate uses an inline `<ComboBox>` bound to `DataContext.Tables` (relative-source up to the SeedView UserControl) + `SelectedItem={Binding RootRef}`. The `TablePickerView` from step 03 is reusable for other consumers (extras tab) but was not nested here — keeps WPF binding shape simple.
- 2026-05-03 — Step 06 manual smoke: deferred to operator. WPF UI cannot be exercised from a CI/headless environment. End-to-end behaviour (Hydrate → save-on-blur → engine call shape) verified through the 220 unit + integration tests (4 new hydration tests).
- 2026-05-03 — Step 07 CLAUDE.md ↔ copilot-instructions.md sync: SHA256 both `f0fe5df9b8fa4d99c25de32a13849e6814a07887098ac365a11b1513e8c52649` (untouched — no new tripwire needed; recipe-level updates were sufficient).
