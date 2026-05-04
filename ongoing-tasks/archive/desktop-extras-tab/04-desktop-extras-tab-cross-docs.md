# 04 — desktop-extras-tab — Cross-doc updates

## Goal

Sync docs for the standalone-tables half landing: M7 mockup status, components.md (`WhereFilterEditor` / `StrategyPicker` / `ScriptList` / `ScriptEditor`), recipe note about the filtered-collection-with-graph-dependency pattern, and the roadmap.

## Track

`docs`.

## What to Build

### `docs/design/desktop-ui/07-extras.md`

- Status header → `**implemented (standalone-tables half; scripts half deferred to engine-pre-post-scripts + desktop-extras-scripts)** (2026-XX-YY)`.
- Update Implications:
  - Add: "Standalone tables = `TablesToProcess` entries whose `(Schema, Name)` is not in the FK-reachable set from the seed (`GraphViewModel.AllNodes`). This is a **view filter**, not an engine concept — there is no `Standalone` flag on `TableToExtract`. A table can migrate between Extras and Graph as the FK reachable set changes."
  - Add: "Default strategy on Add is `OnlyOneTable` (matches the M7 mockup; by far the common standalone choice). Operator can change it via the per-row inline editor."
- Mark scripts half as deferred at the bottom: "Pre/post-extraction scripts deferred to `engine-pre-post-scripts` (engine model + JSON + T4 emission) and `desktop-extras-scripts` (UI). The Extras tab's layout has an explicit Auto row reserved for the script section."

### `docs/design/desktop-ui/components.md`

Mark Implemented (with paths):

- **`WhereFilterEditor`** — `Controls/WhereFilterEditor/WhereFilterEditorView.xaml(.cs)` since `desktop-extras-tab`. Single DP `Text` (string, two-way). Wraps `<editor:SqlEditorView IsSingleLine="True"/>`. Used by `NodeInspectorView` (refactored from inline) and `Views/Extras/ExtrasView` per-row inline editor.
- **`StrategyPicker`** — `Controls/StrategyPicker/StrategyPickerView.xaml(.cs)` since `desktop-extras-tab`. Single DP `StrategyChoice` (`StrategyKind`, two-way). The four-radio chooser with one-line tooltips. Used by `NodeInspectorView` and `Views/Extras/ExtrasView`.

Add new entry:
- **`ExtrasView`** — `Views/Extras/ExtrasView.xaml(.cs)`. Bound to `ExtrasViewModel` (Singleton). Hosts the standalone-tables `ItemsControl` + Add button + empty hint. Pre/post-script section deferred.

Defer entries (still flag as deferred):
- `ScriptList` — deferred to `desktop-extras-scripts`.
- `ScriptEditor` — deferred to `desktop-extras-scripts`.

### `docs/methodology/wpf-desktop.md`

Add a small note in "Service abstractions" or "How to add a screen" section about the **filtered-collection-with-cross-VM-dependency pattern** demonstrated by Extras: a Singleton VM (`ExtrasViewModel`) subscribes to another Singleton VM's `INotifyCollectionChanged` (`GraphViewModel.AllNodes`) and recomputes its visible set on each event. Useful precedent for future cross-tab filters or live-derived views.

Reference `desktop-extras-tab` in the implementation-timing table where applicable (or add a row for the cross-VM subscription pattern if a separate row makes sense).

### `docs/roadmap.md`

- Move `desktop-extras-tab` to ✅ Completed:
  > ✅ desktop-extras-tab — Standalone-tables half of M7. Operator edits `TablesToProcess` entries that aren't FK-reachable from the seed in their own dedicated tab; "+ Add table" via TablePicker dialog; per-row inline editor with `WhereFilterEditor` + `StrategyPicker`. Filter is computed from `GraphViewModel.AllNodes` (graceful fallback to "show all" when graph hasn't hydrated). `WhereFilterEditor` and `StrategyPicker` extracted from `NodeInspectorView` into proper UserControls. Pre/post-extraction scripts deferred to `engine-pre-post-scripts` + `desktop-extras-scripts`.
- Promote `engine-pre-post-scripts` to Proposed Next:
  > **engine-pre-post-scripts** — Add `Package.PreScripts` + `PostScripts` (or on `GlobalExtractConfiguration`) — ordered list of `ExtractionScript(Order, Name, Body)` records. JSON round-trip via `JsonPackageReader`/`JsonGlobalConfigReader`. T4 template (`Templates/DefaultTemplate.tt`) emits PreScripts before any INSERT and PostScripts after the last. New characterisation scenario `pre-post-scripts` with golden. Pre-req for `desktop-extras-scripts`.
- Add `desktop-extras-scripts` to the Desktop UI track (after `engine-pre-post-scripts`):
  > **desktop-extras-scripts** — UI for the script half of M7. New `ScriptList` + `ScriptEditor` controls; Up/Down/Edit/Remove per script; slots into `ExtrasView`'s reserved Auto row below the standalone-tables list. Closes M7.

### `CLAUDE.md` audit

No new top-level tripwires. The recipe-level rules (no logic in `.xaml.cs` beyond `InitializeComponent`; reusable controls under `Controls/`; AvalonEdit confined to `SqlEditor`) apply unchanged. Verify SHA256 of `CLAUDE.md` ↔ `.github/copilot-instructions.md` unchanged; record in Notes.

### Verification

- `dotnet build` 0 errors (sanity); `dotnet test` green (sanity).
- Spot-check rendered docs.

## Acceptance Criteria

- [ ] `docs/design/desktop-ui/07-extras.md` Status header set to "implemented (standalone-tables half…)"; Implications updated with filter-view bullet.
- [ ] `docs/design/desktop-ui/components.md` updated for `WhereFilterEditor`, `StrategyPicker`, `ExtrasView`. `ScriptList` / `ScriptEditor` flagged as deferred to `desktop-extras-scripts`.
- [ ] `docs/methodology/wpf-desktop.md` mentions the filtered-collection-with-cross-VM-dependency pattern.
- [ ] `docs/roadmap.md` — feature in ✅ Completed; `engine-pre-post-scripts` promoted; `desktop-extras-scripts` added.
- [ ] CLAUDE.md ↔ copilot-instructions SHA unchanged.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Touched docs: as listed.
- Pattern exemplar for cross-docs steps: `desktop-graph-tab` step 08, `desktop-seed-tab-root-inference` step 04.
- Depends on: [01](./01-desktop-extras-tab-extract-controls.md), [02](./02-desktop-extras-tab-view-model.md), [03](./03-desktop-extras-tab-view.md).
