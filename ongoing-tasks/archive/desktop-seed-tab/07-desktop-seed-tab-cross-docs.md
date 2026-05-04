# 05 — desktop-seed-tab — Cross-doc updates

## Goal

Sync docs for the landed `IUiDispatcher`, `IDatabaseExplorer`, `SqlEditor`/`TablePicker`/`RowsPreviewGrid` reusable controls, and the multi-script Seed tab. Move feature to Completed; promote next.

## Track

`docs`.

## What to Build

### `docs/architecture/overview.md`

- § 2 Components — Desktop row Purpose extended: "Seed tab (M4) for multi-script editing + bounded preview; first feature to use `IUiDispatcher` for marshalling preview rows back to the UI thread; `IDatabaseExplorer` (Logic) for table listing + bounded SELECT preview".
- § 5 — DI line: append `IUiDispatcher` (Singleton, Desktop) and `IDatabaseExplorer` (Singleton, Logic-side).

### `docs/methodology/wpf-desktop.md`

- § Service abstractions header note — change `partially landed` to: `**Status: landed.** All recipe pre-spec abstractions are implemented (`IDialogService` since `desktop-startup-and-open-workspace`; `IUiDispatcher` since `desktop-seed-tab`).`
- `IUiDispatcher` subsection — mark **implemented**:
  > Implemented as `Services/Threading/{IUiDispatcher.cs, UiDispatcher.cs}` since `desktop-seed-tab`. Test fake at `Tests/Desktop/Fakes/FakeUiDispatcher.cs` (runs everything inline). The WPF impl is safe to resolve in non-WPF test hosts (returns inline when `Application.Current` is null).
- Implementation timing table — update `IUiDispatcher` row to `desktop-seed-tab (landed)`. Add a row for `IDatabaseExplorer` (`desktop-seed-tab` / **landed** / "first table-list + preview-query consumer in the Seed tab").
- New § "Database explorer" between "Connection management" and "Dialogs":
  > The Desktop reads tables and runs preview SELECTs through `IDatabaseExplorer` (`Logic/Schema/`) — never touches `SqlConnection` directly (mirrors the `IConnectionTester` pattern). `MSSqlDatabaseExplorer.PreviewQueryAsync` enforces the row cap by reading `maxRows + 1` (truncation flag) and stringifies all cells via `Convert.ToString(value, InvariantCulture)` — type-aware rendering is deferred. Failures are wrapped in `DatabaseExplorerException`; cancellation propagates as `OperationCanceledException`.
- § How to add a screen pointer expanded further:
  > **Reference implementations:** `Views/Overview/` (read-only summary; `desktop-shell`), `Views/Welcome/` (services-driven empty-state; `desktop-startup-and-open-workspace`), `Controls/ConnectionEditor/` (reusable control hosted in dialogs; `desktop-connection-management`), `Views/Seed/` + `Controls/{SqlEditor, TablePicker, RowsPreviewGrid}/` (multi-script editor + foundational reusable controls; `desktop-seed-tab`).

### `docs/design/desktop-ui/components.md`

- Mark **Implemented** on `SqlEditor`, `TablePicker`, `RowsPreviewGrid` entries (small status badge or one-line note). Add the file path under each.
- Note on `WhereFilterEditor` / `ScriptEditor` (the future wrappers): "wraps `SqlEditor`; not yet built — `desktop-graph-tab` and `desktop-extras-tab` respectively".

### `docs/methodology/workspace-format.md`

- If the example workspace doesn't already include a populated `Package.Scripts` array, add one so future readers see the seed-tab-relevant shape.
- Cross-link to `desktop-seed-tab` archive once the feature is moved.

### `docs/roadmap.md`

- Move `desktop-seed-tab` to ✅ Completed:
  > ✅ desktop-seed-tab — Multi-script editor (M4) with `Schema.TableName` root picker, AvalonEdit seed-query editor, bounded 200-row preview with cancel; first impl of `IUiDispatcher` and `IDatabaseExplorer`; foundational `Controls/{SqlEditor, TablePicker, RowsPreviewGrid}/`; save-on-blur (debounced).
- Promote next item to Proposed next. Per the existing track ordering: `desktop-graph-viz` (graph library pick) is the natural next.

### `CLAUDE.md` audit

- New tripwires worth surfacing? Possibly:
  - "AvalonEdit types live only inside `Controls/SqlEditor/`. ViewModels never see `TextDocument`, `TextEditor`, or any `ICSharpCode.AvalonEdit.*` type." — but this is **already in the WPF tripwires section** of CLAUDE.md (verify).
  - "VMs marshal observable mutations from background continuations via `IUiDispatcher.InvokeAsync` — never `Dispatcher.Invoke`." — also already present.
- Decision: no new tripwire line. Recipe-level updates are sufficient.
- Recompute SHA256 of CLAUDE.md and `.github/copilot-instructions.md`; assert equal; record in Notes.

### Verification

- `dotnet build` 0 errors (sanity); `dotnet test` green (sanity).

## Acceptance Criteria

- [ ] `docs/architecture/overview.md` § 2 + § 5 updated.
- [ ] `docs/methodology/wpf-desktop.md` § Service abstractions header → "landed"; `IUiDispatcher` subsection marked implemented; impl-timing table has `IDatabaseExplorer` row; new § "Database explorer"; § How to add a screen lists four reference impls.
- [ ] `docs/design/desktop-ui/components.md` — `SqlEditor`, `TablePicker`, `RowsPreviewGrid` marked Implemented.
- [ ] `docs/methodology/workspace-format.md` example shows populated `Package.Scripts`.
- [ ] `docs/roadmap.md` — feature in ✅ Completed; next item promoted.
- [ ] CLAUDE.md / copilot-instructions.md SHA256 unchanged (or updated symmetrically + recorded).
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Touched docs: as listed above.
- Pattern exemplars: prior step-NN cross-docs.
- Depends on: [04 — Seed view + VMs](./04-desktop-seed-tab-view-and-vms.md).
