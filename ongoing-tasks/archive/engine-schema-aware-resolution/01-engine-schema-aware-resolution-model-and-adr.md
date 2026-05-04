# 01 — engine-schema-aware-resolution — ADR-011 + Schema field on engine model + serialization

## Goal

Add `Schema` (string, default `""`) to `PTable` and `TableToExtract` (and any other model class that names a table). XML and JSON serialization handle the new field with full round-trip backward compat: legacy fixtures continue to deserialise byte-identically. Equality / hashing become `(Schema, Name)` tuple-based. **No engine behaviour changes yet** — those land in steps 02-04. ADR-011 records the semantics.

## Track

`engine` (.NET / C#).

## What Exists

- [`adr/readme.md`](../../adr/readme.md) — index. Latest is ADR-010. Number this `011`.
- [`ParameterizationExtractor.Logic/Model/PTable.cs`](../../ParameterizationExtractor.Logic/Model/PTable.cs) — current model. Read first to identify what needs modifying.
- [`ParameterizationExtractor.Logic/Model/TableToExtract.cs`](../../ParameterizationExtractor.Logic/Model/TableToExtract.cs) — config model.
- [`Tests/EngineJsonTests/`](../../Tests/EngineJsonTests/) — pattern exemplar for JSON serialization tests.
- Existing characterisation goldens — must continue to match (verified in step 05).

## What to Build

### `adr/011-engine-schema-awareness.md`

Sections per `adr/_template.md`:
- **Status:** Accepted.
- **Context:** Bare-name resolution today; cross-schema FK reference scenario was deferred from `characterisation-tests`; `desktop-seed-tab` needs `Schema.TableName` for its picker. Forces this to land before any UI feature surfaces schema-prefixed tables.
- **Decision:** the bullet list from feature-architecture § 2 (empty Schema = "use SQL default-schema fallback"; `MSSQLSourceSchema` always populates discovered Schema; bare-name lookup uses 1-or-throw policy; emission `Qualify(t)` reads the *config* `TableToExtract`'s Schema, not the resolved `PTable`'s).
- **Consequences:** easier (cross-schema works; UI can show schemas); harder (additional model surface; emission decision matrix; risk of operator confusion when ambiguity throws); open follow-ups (CLI schema flag, schema-rename detection, fixture-rewrite tool).

### `adr/readme.md`

Add the `011` row to the index table.

### `Logic/Model/PTable.cs` — add `Schema` field

- New property: `public string Schema { get; set; } = string.Empty;` (or via constructor parameter, matching the existing constructor pattern — read first).
- `Equals(object?)` and `GetHashCode()` updated to include `Schema` with `OrdinalIgnoreCase` semantics.
- If `PTable` already has `[XmlAttribute]` on properties, add `[XmlAttribute("schema")]` + `[DefaultValue("")]` so empty Schema isn't emitted on round-trip.
- If `PTable` is consumed by JSON (via `JsonOptions.Default`), the camelCase `schema` property is automatic; just verify with the round-trip test.

### `Logic/Model/TableToExtract.cs` — add `Schema` field

Same shape as `PTable.Schema`. **This is the operator-facing config slot**. Existing fixtures omit it → empty.

### Other model types that name a table

Audit grep — find all classes with a `Name` property that names a table:

```bash
grep -nE 'class.*Table|TableName|public string Name' ParameterizationExtractor.Logic/Model/
```

Likely candidates: `PColumn` (column name, not table — skip), `PRelation` (FK, has `Source`/`Target` pointing at `PTable` — already covered transitively). Confirm during impl. **Do not** add `Schema` to types that don't name a table.

### Tests

Per `prompts/execution.md` — CONTRACT tasks (data shape) need a serialization test. CODE for the equality logic needs TDD.

New file `Tests/EngineModelTests/PTableSchemaTests.cs` (folder name matches `EngineJsonTests/` precedent):

1. `Default_Schema_IsEmptyString` — fresh `PTable` has `Schema == ""`.
2. `Equality_TupleSemantics_SchemaAndNameMustMatch` — `new PTable { Schema = "dbo", Name = "X" }` equals `new PTable { Schema = "DBO", Name = "x" }` (case-insensitive) but does NOT equal `new PTable { Schema = "audit", Name = "X" }`.
3. `GetHashCode_TupleSemantics_MatchesEquality` — equal objects produce equal hash codes.

New file `Tests/EngineModelTests/TableToExtractSerializationTests.cs`:

4. `Xml_LegacyFixture_DeserializesWithEmptySchema` — feed a `<TableToExtract Name="Patient" />` XML snippet; assert `Schema == ""`.
5. `Xml_RoundTrip_EmptySchema_DoesNotEmitAttribute` — serialize an empty-Schema `TableToExtract`; assert XML has no `schema=""` attribute.
6. `Xml_RoundTrip_NonEmptySchema_EmitsSchemaAttribute`.
7. `Json_LegacyFixture_DeserializesWithEmptySchema` — feed `{"name":"Patient"}` JSON; assert `Schema == ""` (or `null` normalised to `""` by setter; impl decides).
8. `Json_RoundTrip_EmptySchema_DoesNotEmitProperty` — serialize empty-Schema; assert JSON has no `schema` field.
9. `Json_RoundTrip_NonEmptySchema_EmitsSchemaProperty`.

The "doesn't emit" tests are the backward-compat lock — every existing fixture continues to round-trip byte-identical.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test "SQL Buldozer.sln"` — pre-existing 135 + new 9 = **144 tests**, all green.
- All existing characterisation tests continue to pass unchanged (the goldens have empty Schema in their config; behaviour is unchanged this step).

## Acceptance Criteria

- [ ] `adr/011-engine-schema-awareness.md` exists with the four standard sections; referenced from `adr/readme.md`.
- [ ] `PTable.Schema` and `TableToExtract.Schema` exist with default `""`.
- [ ] `PTable.Equals`/`GetHashCode` use `(Schema, Name)` tuple, OrdinalIgnoreCase.
- [ ] XML serialization: missing `schema` attribute → `Schema = ""`; empty Schema does NOT emit the attribute on write.
- [ ] JSON serialization: missing `schema` property → `Schema = ""`; empty Schema does NOT emit the property on write.
- [ ] All 9 new tests pass.
- [ ] All existing 135 tests still pass — the model change is additive only.
- [ ] No other model type that names a table was modified without explicit grep evidence (logged in Notes).
- [ ] `dotnet build` 0 errors; `dotnet test` 144/144 pass.

## References

- ADR: this step authors `adr/011-engine-schema-awareness.md`. References `004-t4-sql-generation.md` (T4 emission consequence) + `009-workspace-format.md` (`.bws` schema field implication).
- Methodology: [`docs/methodology/dotnet-cli.md`](../../docs/methodology/dotnet-cli.md).
- Pattern exemplars: `Tests/EngineJsonTests/`, `Logic/Configs/Json/JsonOptions.cs`.
- Depends on: nothing (first step).
