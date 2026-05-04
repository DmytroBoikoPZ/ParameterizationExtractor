# 07 — Extras tab

> Captures: M7 from the mockup pass. Status: **implemented (standalone-tables half; scripts half deferred to `engine-pre-post-scripts` + `desktop-extras-scripts`)** (2026-05-03).
>
> Where the user adds standalone tables (extracted outside the FK graph) and pre/post extraction SQL scripts. Covers point #4 of the user concept ("adding additional scripts, and tables").

## Mockup

```
┌─ Extras ────────────────────────────────────────────────────────────────────┐
│ ── Standalone tables (extracted outside the FK graph) ──────────────────── │
│ ┌────────────────┬───────────────┬──────────────────────────┬──────────┐    │
│ │ Table          │ Strategy      │ Where                    │          │    │
│ ├────────────────┼───────────────┼──────────────────────────┼──────────┤    │
│ │ LookupCountry  │ OnlyOneTable  │ (none)                   │ [ Edit ] │    │
│ │ LookupCurrency │ OnlyOneTable  │ IsActive = 1             │ [ Edit ] │    │
│ └────────────────┴───────────────┴──────────────────────────┴──────────┘    │
│ [ + Add table... ]                                                          │
│                                                                             │
│ ── Pre-extraction scripts ──────────────────────────────────────────────── │
│ ┌──────────────────────────────────────────────────────────────────────┐    │
│ │ 01 · disable-triggers.sql                                            │    │
│ │   EXEC sp_msforeachtable 'ALTER TABLE ? DISABLE TRIGGER ALL';        │    │
│ └──────────────────────────────────────────────────────────────────────┘    │
│ [ + Add script... ]   [ ↑ ]   [ ↓ ]   [ Edit ]   [ Remove ]                │
│                                                                             │
│ ── Post-extraction scripts ─────────────────────────────────────────────── │
│ (none)                                                                      │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Implications

- **Standalone tables** are extracted independently of the FK graph — they don't participate in the walk from the seed. Useful for lookup tables, reference data, or tables that share no FK path with the seed.
- **Standalone tables = a *view filter*, not an engine concept.** The Extras tab lists `TablesToProcess` entries whose `(Schema, Name)` is NOT in `GraphViewModel.AllNodes` (the FK reachable set from the seed). There is no `Standalone` flag on `TableToExtract`. A table can migrate between Extras and Graph as the FK reachable set changes — this matches operator intent.
- **Default strategy on Add is `OnlyOneTable`** — by far the common standalone choice. The operator can change it via the per-row inline editor (`StrategyPicker` + `WhereFilterEditor` + Excluded checkbox).
- **Graceful fallback** while the graph hasn't hydrated yet: ALL `TablesToProcess` entries are shown as standalone (better than confusing emptiness). Once the graph loads, the list narrows.
- Standalone tables can have any of the same strategies (`FKDependency`, `OnlyChildren`, `OnlyParent`, `OnlyOneTable`) — but `OnlyOneTable` is by far the most common in this position.
- **Pre-extraction scripts** run once before the walk; **post-extraction scripts** run once after. Both are emitted into the output `.sql` file in their declared order.
- Order matters and is editable (`↑` `↓`). Stored as an explicit ordered array in the workspace JSON.
- Scripts are **inline SQL text**, not file paths. Workspace stays self-contained.
- Open question (see [03 — Main shell](./03-shell.md)): the tab strip in M3 shows "Scripts" as its own tab separate from "Extras" — if we keep that, this screen shrinks to just the Standalone-tables half.

## Components used

- [TablePicker](./components.md#tablepicker) — used inside the "Add table…" flyout. Same component as [04 — Seed](./04-seed.md).
- [StrategyPicker](./components.md#strategypicker) — used inside the per-row Edit flyout. Same as [06 — Node inspector](./06-node-inspector.md).
- [WhereFilterEditor](./components.md#wherefiltereditor) — same.
- [ScriptList](./components.md#scriptlist) — ordered list of script entries with name + body preview.
- [ScriptEditor](./components.md#scripteditor) — multi-line SQL editor opened by Edit/Add. Wraps [SqlEditor](./components.md#sqleditor) in multi-line mode.

## Open questions specific to this screen

- **Scripts on their own tab?** See open question above and on [03 — Main shell](./03-shell.md).
- **Script naming.** Auto-numbered (`01`, `02`) or user-named? Both?
- **External script files.** Allow `[ Add from file… ]` that imports the file content into the workspace JSON, or also support storing-by-path for scripts that live alongside the workspace?
- **Per-script "skip on dry-run" flag.** Some pre-scripts (e.g. `DISABLE TRIGGER ALL`) shouldn't run in dry-run mode against the source. Today the engine emits them unconditionally.
- **Variables in scripts.** Dollar-substitution from workspace metadata (e.g. `$SeedId`)? Probably out of scope for v1.

## What ships in v1 (`desktop-extras-tab`)

- Standalone-tables list with per-row inline expander (Strategy / Where / Excluded edit) — uses extracted `Controls/StrategyPicker/` + `Controls/WhereFilterEditor/`.
- "+ Add table…" button → `IDialogService.ShowAddStandaloneTableDialogAsync` (new method, returns `TableRef?`); WPF impl is `Dialogs/AddStandaloneTable/`.
- Per-row Remove button.
- Anchor follows `Seed.SelectedScript` via the same `MainWindowViewModel.PropertyChanged` route as the Graph tab.
- Save-on-change debounced 500 ms (mirrors `NodeInspectorViewModel`).
- Empty hint when all of the current script's `TablesToProcess` are FK-reachable.

## What's deferred

- **Pre-extraction scripts** — engine model + JSON + T4 emission tracked as `engine-pre-post-scripts`; UI tracked as `desktop-extras-scripts`. The Extras tab's outer Grid is laid out so the script section can be slotted in below the standalone-tables list.
- **External script files / "Add from file…"** — out of scope.
- **Per-script "skip on dry-run" flag** — out of scope.
- **Variable substitution in scripts** — out of scope.
- **Multi-script bulk operations** — out of scope; Extras edits the anchored script's `TablesToProcess` only.
