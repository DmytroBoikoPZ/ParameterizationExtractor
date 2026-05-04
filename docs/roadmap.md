# SQL Buldozer — Roadmap

> Living checklist of features. One sentence per item. Detailed scope lives in the corresponding `ongoing-tasks/{feature}-checklist.md` once a feature is scaffolded.
>
> Sequence is suggestive, not binding — pick whichever item best fits the current session.

---

## Proposed next

- [ ] **engine-pre-post-scripts** — Add `PreScripts` + `PostScripts` collections (likely on `Package` or `GlobalExtractConfiguration`) — ordered list of `ExtractionScript(Order, Name, Body)` records. JSON round-trip via `JsonPackageReader`/`JsonGlobalConfigReader`. T4 template (`Templates/DefaultTemplate.tt`) emits PreScripts before any `INSERT` and PostScripts after the last. New characterisation scenario `pre-post-scripts` with golden. Pre-req for `desktop-extras-scripts`.
- [ ] **desktop-extras-scripts** — UI for the script half of M7. `ScriptList` + `ScriptEditor` controls; Up/Down/Edit/Remove per script; slots into `ExtrasView`'s reserved Auto row below the standalone-tables list. Closes M7. Depends on `engine-pre-post-scripts`.
- [ ] **desktop-graph-host-camera** — Bundles the Fit / +/- zoom toolbar buttons (deferred from `desktop-graph-viz` step 08) with the AGL-highlight on `SelectedNodeId` + canvas auto-fit on visible-set change (silently dropped from `desktop-graph-tab` step 02). Closes the operator-friendliness gap most likely to bite during real graph use. AGL exposes `GraphCanvas.LayoutTransform` / `ContentBoundsToZoom` — design surface is one-shot `FitRequested` trigger vs two-way `Zoom: double` DP.

## Desktop UI track

In rough sequence; each later feature assumes the earlier ones landed.
- [ ] **desktop-extras-scripts** — UI for the script half of M7 (slots into `ExtrasView`). Depends on `engine-pre-post-scripts`.
- [ ] **desktop-dry-run-and-execute** — Run-tab UI wiring `Logic.PackageProcessor`; three radio modes (dry-run / save .sql / execute against target); requires engine `CancellationToken` support (see backlog) (mockup [M8](./design/desktop-ui/08-run.md)).

## Engine / CLI

- [ ] **engine-pre-post-scripts** — (see Proposed Next).
- [ ] **engine-cancellation-token** — `PackageProcessor.RunAsync` takes a `CancellationToken`; precondition for the desktop's "Cancel" button on the Run tab.
- [ ] **engine-t4-excluded-guard** — Belt-and-braces: thread `EmissionExcluded` through `PRecord` (mirrors `EmissionSchema`) and add `<# if (table.EmissionExcluded) continue; #>` in `Templates/DefaultTemplate.tt`. Today the protection lives in `DependencyBuilder` (skips excluded tables before the T4 sees them); golden `excluded-table.sql` pins the behaviour. Tiny — ~15 min. Drop unless a walker regression slips past.
- [ ] **engine-uow-factory-per-call** — Refactor `IUnitOfWorkFactory` to accept a connection string per-call so `MSSqlGraphBuilder` can drop its duplicated `FkSql` const and share `IObjectMetaDataProvider`'s FK metadata query. Medium-large engine refactor; probably not worth it for one duplicated SQL string — listed for completeness.
- [ ] **cli-generic-host-migration** — Migrate the CLI's custom `AppBuilder` to `Microsoft.Extensions.Hosting`, matching the desktop and shedding the wrapper ([ADR-006](../adr/006-msdi-container.md) named this as a candidate).
- [ ] **cli-accepts-bws** — Optional CLI extension to read `.bws` workspaces directly via `--package`; today the CLI handles only `.xml` and `.bc`.
- [ ] **adr-audit-002-003-004** — Read each of the bootstrap-era ADRs (module layering, FParsec DSL, T4 SQL generation) and verify the code matches; ADR-006's open follow-up.

## Cleanup / housekeeping

- [ ] **workspace-format-typo-aliases** — `[JsonPropertyName]` aliases so new workspaces can write `throwExceptionIfNotExists` / `uniqueColumns` while old files still load (deferred from `desktop-workspace-format`).
- [ ] **workspace-format-jsonschema** — JSON Schema document for `.bws` so editors validate hand-edited workspaces.
- [ ] **recipe-stale-facts-fix** — Update `docs/methodology/dotnet-cli.md` § Testing (NUnit version) and § Project shape (framework targets) to current reality.
- [ ] **bootstrap-residue-cleanup** — Delete `ongoing-tasks/_bootstrap/` (warned by `verify-bootstrap.ps1`) and `git rm` the stray pre-collapse `ongoing-tasks/msdi-migration-checklist.md` in the repo root.
- [ ] **mainwindow-vm-ctor-injection** — Refactor `MainWindow` to take its VM via constructor instead of `App.xaml.cs` setting `DataContext` externally; closes the recipe ↔ implementation drift flagged in `desktop-skeleton`'s audit.

## Far future

- [ ] **workspace-versioning-v2** — Schema migration story when a backward-incompatible field forces `$version: 2`.
- [ ] **desktop-cross-platform** — Port the desktop to Avalonia for Mac/Linux operators (no current demand; reconsider only if the user base widens).
- [ ] **engine-update-emission** — Emit `UPDATE` SQL alongside `INSERT` for tables marked accordingly (engine has the strategy hook today; T4 template path needs work).
- [ ] **desktop-graph-edge-click** — v2 of `desktop-graph-viz`: clicking a graph edge directly toggles Follow/Stop, complementing the inspector. Constraint already locked in ADR-008.

## Completed

- ✅ desktop-extras-tab — Standalone-tables half of M7 implemented. Operator edits `TablesToProcess` entries that aren't FK-reachable from the seed in their own dedicated tab; "+ Add table…" via a `MetroWindow`-based picker dialog; per-row inline expander hosts the extracted reusable controls `Controls/StrategyPicker/` + `Controls/WhereFilterEditor/` (refactored out of `NodeInspectorView`'s inlined radio group + single-line `SqlEditor`). Filter is computed from `GraphViewModel.AllNodes` with `INotifyCollectionChanged` subscription for re-filter on graph hydrate; graceful fallback to "show all" when graph hasn't loaded. Anchor follows `Seed.SelectedScript`. Pre/post-extraction scripts deferred to `engine-pre-post-scripts` + `desktop-extras-scripts`.
- ✅ desktop-graph-tab — Focus + click-to-explore graph view (M5 revised) with slide-in node inspector (M6); reframes `desktop-graph-viz` v1's survey lens. Operators walk the FK graph by clicking nodes (Pending → expand neighbours; Configured/Excluded → open inspector); inspector edits Strategy / Where / Excluded per node; right-click context menu for Exclude / Include / Reset / Show on whole graph; seed-tab `SelectedScript` drives the graph anchor for single-context view. Implementation deviations: same-pane `Border` overlay (not MahApps `<Flyout>`) since the host is a `UserControl`; `BuildDirectivesEditor` and `FKEdgeList` deferred — engine model already supports them but inspector v1 doesn't expose them.
- ✅ desktop-seed-tab-root-inference — Seed tab auto-detects the root table from the SQL editor's first `FROM` clause via `Logic/Helpers/SeedQueryParser`; auto-populates the picker on empty state and surfaces a non-blocking mismatch hint banner when the operator's pick disagrees with the SQL. UX patch closing the operator-feedback gap surfaced during `desktop-graph-viz` smoke.
- ✅ characterisation-tests — Pinned engine output via 5 golden SQL scenarios (regression net for everything below).
- ✅ net10-upgrade — Migrated all projects from .NET 6 / netstandard2.0 to .NET 10; bumped Microsoft.Extensions.* to 10.0.0; NUnit 3 → 4.
- ✅ replace-stale-deps — `System.Data.SqlClient` → `Microsoft.Data.SqlClient` 6.0.2; `FluentCommandLineParser` → `CommandLineParser` 2.9.1.
- ✅ msdi-migration — Discovered the codebase already used MS.DI; collapsed the migration plan into ADR retirement and dead-package cleanup.
- ✅ desktop-skeleton — New `ParameterizationExtractor.Desktop` project (WPF + Generic Host + MahApps.Metro) with empty `MetroWindow`; ADRs 007 / 008 + recipe `wpf-desktop.md`.
- ✅ desktop-workspace-format — Engine learns JSON for `Package` + `GlobalExtractConfiguration`; `IWorkspaceStore` reads/writes `.bws`; ADR-009; characterisation parity proven by 10 cases over 5 goldens.
- ✅ desktop-shell — M3 tabbed shell (Overview / Seed / Graph / Extras / Run) with Overview tab bound to `IWorkspaceStore`; placeholder text in the other 4 tabs; workspace path passed as the first command-line arg.
- ✅ desktop-startup-and-open-workspace — M1 Welcome view on empty state; first impl of `IDialogService` (MahApps + Win32) and `IRecentFilesStore` (JSON @ `%APPDATA%\SqlBuldozer\`, capped at 5); File menu (Open / Recent / Close / Exit); New-workspace deferred to `desktop-connection-management`.
- ✅ desktop-connection-management — DPAPI password-at-rest (ADR-010), `IConnectionTester` engine seam, reusable `ConnectionEditor`, M2 New-workspace dialog, Edit-connection flow with save-back, Welcome's `[ New ]` button live, ConnectionIndicator clickable.
- ✅ engine-schema-aware-resolution — `Schema` field on table-naming model (default empty for back-compat); `MSSQLSourceSchema.ResolveTable` implements bare-name + qualified resolution per ADR-011; T4 emits `[Schema].[Table]` when non-empty; cross-schema characterisation scenario landed (closes the deferred follow-up from `characterisation-tests`).
- ✅ desktop-seed-tab — Multi-script editor (M4) with `Schema.TableName` root picker, AvalonEdit seed-query editor, bounded 200-row preview with cancel; first impl of `IUiDispatcher` and `IDatabaseExplorer`; foundational reusable controls (`SqlEditor` / `TablePicker` / `RowsPreviewGrid`); save-on-blur (debounced); `WorkspaceConnectionStringBuilder` shared with the connection editor; engine `SourceForScript.Query` field added (UI-only persistence v1).
- ✅ desktop-graph-viz — *(shipped, superseded by `desktop-graph-tab`)* — FK-subgraph view (M5) with per-node state badges (`✓` configured / `◯` pending / `✗` excluded) and per-edge styling (Follow / Stop / Pending); `IGraphBuilder` engine seam; `Controls/GraphHost/` AGL wrapper (ADR-012, `AutomaticGraphLayout` 1.1.12); `Excluded` flag on `TableToExtract` (engine model + wiring honoured by `DependencyBuilder`); `excluded-table` characterisation scenario. Smoke testing showed survey-mode rendering had near-zero operational value — the reframed `desktop-graph-tab` ships focus mode + click-to-explore + the M6 inspector pane on top of these reusable foundations.
