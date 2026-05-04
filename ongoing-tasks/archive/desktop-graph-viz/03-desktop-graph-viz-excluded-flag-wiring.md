# 03 — desktop-graph-viz — Engine wiring: `DependencyBuilder` honours `Excluded` + characterisation scenario

## Goal

Make `Excluded == true` actually skip the table during extraction. `DependencyBuilder` checks the flag at the FK-walk boundary; the T4 template does not emit excluded tables. New characterisation scenario `excluded-table` pins the behaviour with a recorded golden — the byte-identical preservation across the rest of the goldens proves the wiring is purely additive for the default case.

## Track

`engine` (.NET / C#).

## What Exists

- [`Logic/MSSQL/DependencyBuilder.cs`](../../ParameterizationExtractor.Logic/MSSQL/DependencyBuilder.cs) — FK walk; `ConfigHelper.GetTableToExtract(...)` lookup pattern from `engine-schema-aware-resolution` is the place to read the new flag.
- [`Logic/Templates/DefaultTemplate.tt`](../../ParameterizationExtractor.Logic/Templates/DefaultTemplate.tt) — emission. May not need changes if `DependencyBuilder` doesn't enqueue the table at all.
- [`Tests/CharacterisationTests/`](../../Tests/CharacterisationTests/) — fixture infrastructure; `Scenarios.cs` registers scenario builders; `Json/` holds workspace files; `Goldens/` holds recorded SQL.
- `engine-schema-aware-resolution`'s `cross-schema` scenario — pattern exemplar for "engine model change + new characterisation scenario + recorded golden".

## What to Build

### `Logic/MSSQL/DependencyBuilder.cs` — gate the walk

When the candidate table maps to a `TableToExtract` entry with `Excluded == true`, skip enqueuing it:

```csharp
var template = ConfigHelper.GetTableToExtract(tableMetaData.Schema ?? string.Empty, tableName, source);
if (template?.Excluded == true)
{
    // Honour Excluded: do not walk into / out of this table; do not emit.
    continue; // (or equivalent control flow at the discovery point)
}
```

Apply at every place the walker decides whether to recurse into a discovered table or include the table in the emission set. Audit the file for the canonical "should we walk this table" branch — there should be one or two such branches per direction (children / parents). Add the guard, do not duplicate it elsewhere.

### `Logic/Templates/DefaultTemplate.tt` — defensive belt-and-braces

If `DependencyBuilder` correctly skips the table, T4 never sees it; still, add a defensive guard at the per-table emission loop:

```
<# if (table.Excluded) continue; #>
```

(Verify `PRecord` exposes `Excluded` via `EmissionExcluded` set in `DependencyBuilder` — mirror the `EmissionSchema` pattern from engine-schema-aware-resolution. Or simpler: read directly from the `TableToExtract` template at emission time.)

### Characterisation scenario `excluded-table`

`Tests/CharacterisationTests/Scenarios.cs` — add:

```csharp
private static ISourceForScript BuildExcludedTable()
{
    var s = new SourceForScript { ScriptName = "excluded-table" };
    // Use existing test-DB tables. Pick a small parent–child pair, e.g.:
    // - root: TherapyPrograms with a tight Where filter
    // - children include TherapyProgramItems
    // - mark TherapyProgramItems as Excluded → it should NOT appear in the emitted SQL
    s.RootRecords.Add(new RecordsToExtract("TherapyPrograms", "Id = 904"));
    s.TablesToProcess.Add(new TableToExtract("TherapyPrograms", new OnlyChildrenExtractStrategy(), new SqlBuildStrategy()));
    s.TablesToProcess.Add(new TableToExtract("TherapyProgramItems", new FKDependencyExtractStrategy(), new SqlBuildStrategy()) { Excluded = true });
    return s;
}
```

`Tests/CharacterisationTests/Json/excluded-table.json` — equivalent workspace JSON; the `excluded` field on the second table is `true`.

### Record the golden

Run with `BULDOZER_GOLDEN_MODE=record BULDOZER_GOLDEN_SCENARIO=excluded-table` to write `Tests/CharacterisationTests/Goldens/excluded-table.sql`. Inspect it: confirm `TherapyProgramItems` does NOT appear in any `INSERT` / `Deleter` block. Commit the golden.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-02 N + 2 = N + 2 (one new characterisation scenario × two loaders, Programmatic + JSON). All existing characterisation goldens preserved byte-identically.
- The new golden proves the excluded table is absent.

## Acceptance Criteria

- [ ] `DependencyBuilder` reads `TableToExtract.Excluded` and skips the table during the FK walk (single canonical guard, not scattered).
- [ ] T4 template's emission loop has a defensive guard so the recorded golden never accidentally emits an excluded table.
- [ ] `Scenarios.cs` registers `excluded-table` scenario; `Json/excluded-table.json` exists.
- [ ] `Goldens/excluded-table.sql` recorded; manual inspection confirms `TherapyProgramItems` (or whichever excluded table) is absent.
- [ ] All existing characterisation goldens pass byte-identically — pure additive for the default case.
- [ ] `dotnet test` green.

## References

- Pattern exemplars: `engine-schema-aware-resolution` step 03 (DependencyBuilder rewrite + new scenario + recorded golden), `engine-schema-aware-resolution` step 05 (characterisation scenario shape).
- Methodology: [`prompts/execution.md`](../../prompts/execution.md) (TDD for CODE; characterisation tests as "the only safety net").
- Depends on: [02 — `Excluded` flag on model](./02-desktop-graph-viz-excluded-flag-model.md).
