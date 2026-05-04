# 02 — engine-schema-aware-resolution — MSSQLSourceSchema reads schema; bare-name lookup helper

## Goal

`MSSQLSourceSchema.GetMetaData` populates `PTable.Schema` from `sys.schemas` for every discovered table. Add a `ResolveTable(schema, name, discovered)` helper implementing the bare-name-matching policy from feature-architecture § 2. **No FK / dependency-walk changes yet** — step 03's job. Step 02 ensures discovered metadata is schema-aware and the resolution helper is correct in isolation.

## Track

`engine`.

## What Exists

- [`Logic/MSSQL/MSSQLSourceSchema.cs`](../../ParameterizationExtractor.Logic/MSSQL/MSSQLSourceSchema.cs) — `GetMetaData` reads from SQL Server.
- [`Logic/MSSQL/MetaDataInitializer.cs`](../../ParameterizationExtractor.Logic/MSSQL/MetaDataInitializer.cs) — owns the SQL queries.
- `PTable.Schema` (from step 01) — slot exists; needs to be populated.
- Test DB connection via `TestDbConfig.ResolveSourceConnectionString()`.

## What to Build

### `Logic/MSSQL/MetaDataInitializer.cs` — schema-aware metadata SQL

- Tables query: `SELECT s.name AS schema_name, t.name AS table_name, t.object_id, ... FROM sys.tables t INNER JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.is_ms_shipped = 0`. (The `is_ms_shipped = 0` filter mirrors what `MSSQLConnectionTester` does — keep operator-facing tables only.)
- Foreign keys query: similar join via `sys.schemas` for both source and target tables.
- Columns query: unchanged (columns belong to tables, not schemas).

Concrete SQL changes pinned via integration tests in this step.

### `Logic/MSSQL/MSSQLSourceSchema.cs` — populate `PTable.Schema`

- Wherever `PTable` instances are constructed from the metadata reader, set `Schema` from the new `schema_name` column.
- Add `public PTable? ResolveTable(string schema, string name)`:
  - If `schema` is non-empty: linear-search `Tables.FirstOrDefault(t => Equals(t.Schema, schema, IgnoreCase) && Equals(t.Name, name, IgnoreCase))`. Returns null on miss. **(Throw vs null** — pick null and let callers throw with their own context; `DependencyBuilder` already throws on missing tables today.)
  - If `schema` is empty:
    - `var matches = Tables.Where(t => Equals(t.Name, name, IgnoreCase)).ToList();`
    - If `matches.Count == 0` → return null.
    - If `matches.Count == 1` → return `matches[0]` (preserves today's behaviour).
    - If `matches.Count >= 2` → throw `AmbiguousTableException(name, matches.Select(m => m.Schema))`.

Optionally: extract the helper to `Logic/Helpers/TableResolver.cs` if `MSSQLSourceSchema` already feels heavy. **Decision: keep on `MSSQLSourceSchema`** for v1 — it's the natural home; refactor only if a second consumer emerges.

### `Logic/MSSQL/AmbiguousTableException.cs` — new exception type

- `public sealed class AmbiguousTableException : InvalidOperationException`
- Constructor: `(string name, IEnumerable<string> candidateSchemas)` — composes a clear message: `"Table 'X' exists in multiple schemas: 'audit', 'dbo'. Specify Schema in the config to disambiguate."`
- `public string TableName { get; }`, `public IReadOnlyList<string> CandidateSchemas { get; }`.

### Tests

Per `prompts/execution.md` — CODE: TDD cycle.

New file `Tests/EngineSchemaAwareResolution/MSSQLSourceSchemaResolveTests.cs` (folder mirrors `EngineConnectivityTests/` precedent):

**Pure-unit tests** (no DB, in-memory `PTable` lists):

1. `ResolveTable_QualifiedMatch_ReturnsMatchingTable`.
2. `ResolveTable_QualifiedNoMatch_ReturnsNull`.
3. `ResolveTable_BareName_OneMatch_ReturnsThatTable`.
4. `ResolveTable_BareName_NoMatch_ReturnsNull`.
5. `ResolveTable_BareName_TwoMatches_ThrowsAmbiguousTableException` — assert exception's `CandidateSchemas` lists both schemas.
6. `ResolveTable_CaseInsensitive_QualifiedMatch` — `("DBO","patient")` matches `("dbo","Patient")`.

**DB-backed integration tests** (against the test DB):

7. `GetMetaData_PopulatesSchemaForDiscoveredTables` — `await schema.Init(); schema.Tables.Should().OnlyContain(t => !string.IsNullOrEmpty(t.Schema));`
8. `GetMetaData_DiscoveredDboTables_HaveSchemaDbo` — assert known test-DB tables (e.g. `Patient`) have `Schema == "dbo"` (or whatever the test DB's default is — the test asserts on a specific known table, not on the general default).

Tests need the helper extractable / accessible. If `ResolveTable` is on `MSSQLSourceSchema` (the chosen design), the unit tests construct an `MSSQLSourceSchema` instance with a manually-populated `Tables` list; the integration tests use the real one.

If `MSSQLSourceSchema`'s constructor makes manual instantiation hard, add an `internal` testing constructor or factory; document in Notes.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-01 144 + new 8 = **152 tests**, all green.
- All existing characterisation tests continue to pass — discovered metadata now has Schema, but emission paths still bare-name (steps 03-04 not yet landed).

## Acceptance Criteria

- [ ] `MSSQLSourceSchema.Tables` after `Init()` against the test DB → every table has non-empty `Schema`.
- [ ] `MSSQLSourceSchema.ResolveTable(schema, name)` exists and implements the policy from feature-architecture § 2.
- [ ] `AmbiguousTableException` exists; the message names all candidate schemas.
- [ ] All 8 new tests pass (6 unit + 2 integration).
- [ ] All existing 144 tests still pass.
- [ ] Bare-name matching with one discovered match returns that table — preserves today's behaviour.
- [ ] No FK / dependency-walk changes yet (step 03's scope).
- [ ] `dotnet build` 0 errors; `dotnet test` 152/152 pass.

## References

- ADR: [`adr/011-engine-schema-awareness.md`](../../adr/011-engine-schema-awareness.md) (from step 01).
- Methodology: [`docs/methodology/dotnet-cli.md`](../../docs/methodology/dotnet-cli.md).
- Pattern exemplars: `Logic/MSSQL/MSSQLSourceSchema.cs` (existing structure), `Tests/EngineConnectivityTests/`.
- Depends on: [01 — model + ADR](./01-engine-schema-aware-resolution-model-and-adr.md).
