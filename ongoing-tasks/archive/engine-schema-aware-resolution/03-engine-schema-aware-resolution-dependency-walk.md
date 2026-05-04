# 03 — engine-schema-aware-resolution — Dependency walking + FK resolution use (Schema, Name) tuples

## Goal

`DependencyBuilder` resolves FK targets via `(Schema, Name)` tuples instead of bare names. Operator-facing config entries (`TableToExtract` from XML/JSON) flow through `MSSQLSourceSchema.ResolveTable` (from step 02) so bare names continue to work when unambiguous, while qualified entries (`Schema="audit"`) match exactly. **No SQL-emission changes yet** — step 04. Step 03 is the wiring that lets a cross-schema graph traverse correctly.

## Track

`engine`.

## What Exists

- `MSSQLSourceSchema.ResolveTable` (from step 02).
- [`Logic/MSSQL/DependencyBuilder.cs`](../../ParameterizationExtractor.Logic/MSSQL/DependencyBuilder.cs) — walks the FK graph from seed records.
- [`Logic/Model/PRelation.cs`](../../ParameterizationExtractor.Logic/Model/PRelation.cs) (or wherever the FK relation model lives) — has source/target `PTable` references.
- All existing characterisation tests — they cover same-schema graphs and must continue to pass.

## What to Build

### `Logic/MSSQL/DependencyBuilder.cs` — switch lookups to `(Schema, Name)` tuples

- Audit every place that does `Tables.FirstOrDefault(t => t.Name == ...)` (or any bare-name comparison) — replace with `_schema.ResolveTable(configSchema, configName)`.
- Audit every place that compares `PTable` instances against a config-supplied name — same replacement.
- The `_schema.ResolveTable` call may throw `AmbiguousTableException` (from step 02) — let it propagate with the exception's natural message; do NOT swallow.
- FK targets: if the FK row from `sys.foreign_keys` has both source and target schema (it does — populated in step 02), resolve via `(targetSchema, targetName)` directly without the bare-name policy (FK targets are always concrete).

### Audit grep — touchpoints

```bash
grep -nE 'Tables\.(First|Single|Find|Where|Any).*Name' ParameterizationExtractor.Logic/MSSQL/
```

Identify each call site; logically classify:
- "Looking up an operator-config-supplied name" → `ResolveTable(configSchema, configName)`.
- "Looking up a discovered FK target by metadata-supplied name" → direct `(Schema, Name)` tuple comparison (concrete).
- "Listing all tables" — unchanged.

Document touchpoint count + classification in Notes.

### Tests

Per `prompts/execution.md` — CODE: TDD cycle.

The cross-schema *characterisation* scenario lands in step 05; this step's tests are tighter:

New file `Tests/EngineSchemaAwareResolution/DependencyBuilderResolveTests.cs`:

1. `Prepare_BareNameConfig_OneMatchInDiscovered_ResolvesAndWalks` — operator's config has `<TableToExtract Name="Patient" />`; engine walks correctly. (Regression for today's behaviour.)
2. `Prepare_QualifiedConfig_MatchesExactSchema` — `<TableToExtract Schema="dbo" Name="Patient" />` matches the same way.
3. `Prepare_QualifiedConfig_WrongSchema_Throws` — `<TableToExtract Schema="nonexistent" Name="Patient" />` → `TableNotFoundException` (or whatever `DependencyBuilder` throws today for a missing table — match its existing pattern).

These run against the real test DB.

A pure-unit test for the AmbiguousTableException propagation path is hard without two same-named tables in the test DB, so it's covered transitively in step 02 (the helper test) and via the cross-schema scenario in step 05 (which deliberately introduces a second schema).

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-02 152 + new 3 = **155 tests**, all green.
- All existing 5 characterisation scenarios × 2 loaders = 10 cases continue to pass — bare-name resolution still works for the test DB's same-schema setup.

## Acceptance Criteria

- [ ] All `Tables.FirstOrDefault(t => t.Name == ...)` (or equivalent) bare-name lookups in `DependencyBuilder.cs` route through `_schema.ResolveTable`.
- [ ] Audit grep results documented in Notes (touchpoint count + classification).
- [ ] FK target resolution uses `(Schema, Name)` tuples on both sides.
- [ ] All 3 new tests pass.
- [ ] All existing 152 tests still pass — including the 10 characterisation cases (bare-name back-compat).
- [ ] No SQL-emission changes (step 04 territory).
- [ ] `dotnet build` 0 errors; `dotnet test` 155/155 pass.

## References

- ADR: [`adr/011-engine-schema-awareness.md`](../../adr/011-engine-schema-awareness.md).
- Methodology: [`docs/methodology/dotnet-cli.md`](../../docs/methodology/dotnet-cli.md).
- Depends on: [02 — metadata + lookup helper](./02-engine-schema-aware-resolution-metadata-and-lookup.md).
