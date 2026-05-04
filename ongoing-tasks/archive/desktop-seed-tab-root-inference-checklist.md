# Desktop Seed Tab — Root Inference

## Goal

Make the Seed tab's "what should I do?" obvious by **inferring the root table from the SQL** the operator types. The root combobox stops being a separate input the operator has to remember; it becomes a passive confirmation that updates as the operator writes the seed query. Mismatch (the operator's picked root doesn't appear in the SQL) surfaces as a subtle inline hint, not a blocker.

This is a small, focused UX patch — sized as a half-day follow-up to `desktop-seed-tab`. It does NOT touch the engine model or the graph features.

## Scope

- **In scope:**
  - **`Logic.Helpers.SeedQueryParser`** — regex / lightweight parser that extracts the first `FROM <schema>.<table>` (or bare `FROM <table>`) from a SQL string. Returns `record SeedRoot(string Schema, string Name)?` (null when no `FROM` is found or the SQL is empty). Lives in `Logic` because the parser is engine-side reusable (not WPF-specific).
  - **`ScriptEditorViewModel.SeedQueryChanged`** — when the SQL changes, run `SeedQueryParser.TryExtract` against the new value. If a root is found:
    - If `RootSchema` / `RootTable` are empty → set them from the parser (auto-populate).
    - If `RootSchema` / `RootTable` are non-empty AND match the parsed root (case-insensitive) → no-op.
    - If non-empty AND DON'T match → set `RootMismatchHint = "SQL targets {Schema.Name}; picked root is {RootSchema.RootTable}"` (operator-visible warning, no auto-overwrite).
  - **`SeedView.xaml`** — small inline hint banner under the Root picker, bound to `RootMismatchHint`. Shown only when non-empty. Friendly amber tone, dismissible (sets `RootMismatchHint = ""`).
  - **`SeedView.xaml`** — reorder + relabel the Root picker so it visually reads as "Root (auto-detected from query):" with a subtle hint icon. Make the SQL editor visually primary (the operator's main input).
  - **`ScriptEditorViewModel.RootMismatchHint`** — `[ObservableProperty] string _rootMismatchHint = string.Empty;`. Cleared when the operator picks a new root from the picker (overrides the hint) OR edits the SQL to match.
  - **Tests** — `SeedQueryParserTests` (parses bare `FROM Patient` / qualified `FROM dbo.Patient` / square-bracketed `FROM [dbo].[Patient]` / leading whitespace / trailing semicolon / multi-line / no FROM / commented-out FROM); `ScriptEditorViewModelTests` extension (auto-populate when root empty; no-op when matching; mismatch hint when different; hint clears on picker pick).

- **Out of scope:**
  - **Multi-table queries with JOINs** — only the first `FROM` is parsed. Operator can override via the picker.
  - **CTEs (`WITH … SELECT … FROM …`)** — accepted; parser walks past the WITH block. Documented as known limitation if the regex misses edge cases.
  - **Subquery `FROM`** — only the outermost is taken.
  - **Auto-fix of the picker on mismatch** — the hint surfaces; the operator decides.
  - **Engine-side inference** — engine still consumes `RootSchema` + `RootTable` from `RecordsToExtract`. The parser is UI-only.

- **Dependencies:**
  - [`desktop-seed-tab`](./archive/desktop-seed-tab-checklist.md) — provides `ScriptEditorViewModel`, `SeedView`.

## Architecture

See [feature-architecture.md](./desktop-seed-tab-root-inference/feature-architecture.md) for the parser contract, the VM trigger flow, and the mismatch-hint state machine.

## Steps

- [x] [01 — `SeedQueryParser` (Logic helper) + tests](./desktop-seed-tab-root-inference/01-desktop-seed-tab-root-inference-parser.md)
- [x] [02 — `ScriptEditorViewModel` auto-populate + mismatch hint + tests](./desktop-seed-tab-root-inference/02-desktop-seed-tab-root-inference-vm-wiring.md)
- [x] [03 — `SeedView` reordering + inline hint banner + manual smoke](./desktop-seed-tab-root-inference/03-desktop-seed-tab-root-inference-view.md)
- [x] [04 — Cross-doc updates (M4 mockup + recipe note)](./desktop-seed-tab-root-inference/04-desktop-seed-tab-root-inference-cross-docs.md)

## Notes

Free-form log of decisions, blockers, links to PRs. Dated entries preferred (YYYY-MM-DD).

- 2026-05-03 — Feature kicked off after operator UX feedback during `desktop-graph-viz` smoke testing: "I have a dropbox and I have a SQL editor — if I run the query I see records, but I don't know I need to filter table in root combobox". The disconnect is: operators write SQL first; the picker should follow.
- 2026-05-03 — All 4 steps executed end-to-end:
  - **Step 01** — `Logic/Helpers/SeedQueryParser.cs` token scanner (no regex; handles line/block comments, single-quote string literals with `''` escape, bracketed identifiers, qualified `schema.table`, bare table, temp `#table`, CTE name as documented limitation, subquery-after-FROM returns null). 18 tests added (`Tests/EngineHelperTests/SeedQueryParserTests.cs`), all green.
  - **Step 02** — `ScriptEditorViewModel` extended with `RootMismatchHint` observable, `InferRootFromSeedQuery` helper, two suppression flags (`_suppressSaveDuringAutoPopulate` for save coalescing, `_suppressMismatchOnNextRootChange` for hint logic), and `DismissRootMismatchHintCommand`. Picker is auto-populated only when empty; manual override is sticky and surfaces a mismatch hint instead of overwriting. 7 new tests pass; pre-existing 13 unchanged.
  - **Step 03** — `SeedView.xaml` relabelled "Root (auto-detected from query):", added inline hint banner under the Root row (dismissible with ✕), narrative hint "Type a SELECT — the root table is detected automatically." above the SQL editor (now `MinHeight="240"` to be visually primary). New `Converters/StringNonEmptyToVisibilityConverter.cs` registered as `StringNonEmptyToVis` in `App.xaml`. No code-behind changes.
  - **Step 04** — `docs/design/desktop-ui/04-seed.md` mockup + Implications updated with auto-detect bullet; `docs/methodology/wpf-desktop.md` "Code editor (AvalonEdit)" gained a bullet on the inferred-state pattern referencing `SeedQueryParser`; `docs/roadmap.md` moves `desktop-seed-tab-root-inference` to ✅ Completed and removes it from Proposed Next + Desktop UI track.
- 2026-05-03 — `dotnet build` 0 errors. `dotnet test` shows 4 pre-existing failures (`LoadsSampleBws_…`, `OpenWorkspaceAsync_*`, `CloseCommand_*`) caused by `examples/sample.bws` carrying a real DPAPI blob from earlier UI smoke testing — flagged at session start as needing `git checkout examples/sample.bws`. NOT introduced by this feature; all 246 unit tests not gated on sample.bws are green (including the 7 new VM tests + 18 new parser tests).
- 2026-05-03 — CLAUDE.md ↔ `.github/copilot-instructions.md` SHA256 parity verified: `F5364FAFBAE549B96D490C77CC57FE5AB943F0EEBCDE501E0825D0EB73D69CB9`.
- 2026-05-03 — Manual smoke deferred to operator (UI changes, not unit-tested per recipe). The view-side wiring is XAML-only and traceable to the bound VM properties already covered by tests.
