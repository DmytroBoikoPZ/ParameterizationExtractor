# 05 — engine-schema-aware-resolution — Cross-schema characterisation scenario

## Goal

Land the cross-schema characterisation scenario deferred from the bootstrap-era `characterisation-tests` feature. A new scenario seeds from a `dbo` table that has an FK target in a non-default schema (e.g. `audit.ChangeLog`); engine walks across the boundary and emits schema-qualified SQL. Records a golden. Re-runs the existing 10 cases to confirm zero drift.

## Track

`engine` + `tests`.

## What Exists

- All preceding steps (model, metadata, lookup, dependency walk, T4 emission).
- [`Tests/CharacterisationTests/Scenarios.cs`](../../Tests/CharacterisationTests/Scenarios.cs) — list of 5 scenarios; new one rides in here.
- [`Tests/CharacterisationTests/CharacterisationTests.cs`](../../Tests/CharacterisationTests/CharacterisationTests.cs) — fixture; `[OneTimeSetUp]` builds the engine.
- [`Tests/CharacterisationTests/GoldenStore.cs`](../../Tests/CharacterisationTests/Harness/GoldenStore.cs) — recording harness.
- Test DB: needs an `audit` (or chosen-name) schema with at least one table FK-targeted by a `dbo` table.

## What to Build

### Test DB schema setup (idempotent)

`[OneTimeSetUp]` in a new fixture (or in the existing `CharacterisationTests` `[OneTimeSetUp]` — pick whichever doesn't pollute the existing scenarios' setup state). Pseudocode:

```csharp
private static bool _crossSchemaAvailable;

[OneTimeSetUp]
public async Task EnsureAuditSchema()
{
    var connStr = TestDbConfig.ResolveSourceConnectionString();
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    try
    {
        await using (var cmd = new SqlCommand(
            "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'audit') EXEC('CREATE SCHEMA audit')", conn))
            await cmd.ExecuteNonQueryAsync();

        await using (var cmd = new SqlCommand(@"
            IF OBJECT_ID('audit.SchemaTestParent') IS NULL
                CREATE TABLE audit.SchemaTestParent (
                    Id int NOT NULL PRIMARY KEY,
                    Note nvarchar(100) NULL);
            IF OBJECT_ID('dbo.SchemaTestChild') IS NULL
                CREATE TABLE dbo.SchemaTestChild (
                    Id int NOT NULL PRIMARY KEY,
                    ParentId int NOT NULL CONSTRAINT FK_SchemaTestChild_Parent
                        REFERENCES audit.SchemaTestParent(Id));
            -- Idempotent seed data (one parent, one child)
            IF NOT EXISTS (SELECT 1 FROM audit.SchemaTestParent WHERE Id = 1)
                INSERT audit.SchemaTestParent(Id, Note) VALUES (1, 'cross-schema test root');
            IF NOT EXISTS (SELECT 1 FROM dbo.SchemaTestChild WHERE Id = 100)
                INSERT dbo.SchemaTestChild(Id, ParentId) VALUES (100, 1);
            ", conn))
            await cmd.ExecuteNonQueryAsync();

        _crossSchemaAvailable = true;
    }
    catch (SqlException ex) when (ex.Number is 2760 or 262 or 15247)
    {
        // 2760 = CREATE SCHEMA permission; 262 = CREATE TABLE permission; 15247 = no rights.
        _crossSchemaAvailable = false;
        TestContext.Out.WriteLine($"Cross-schema fixture skipped: {ex.Message}");
    }
}
```

The schema + tables are NOT torn down — repeated test runs reuse them. (Tear-down would race with concurrent runs and is unnecessary for a dev DB.)

### New scenario fixture

Two artefacts:

- `Tests/CharacterisationTests/CrossSchemaScenario.xml` — package config with `<TableToExtract Schema="audit" Name="SchemaTestParent" />` + `<TableToExtract Schema="dbo" Name="SchemaTestChild" />`. Strategy: `OnlyParent` from the child to walk up to the parent.
- `Tests/CharacterisationTests/Json/cross-schema.json` — JSON equivalent for the JSON loader.

Both reference the seed query: `SELECT * FROM dbo.SchemaTestChild WHERE Id = 100`.

### `Tests/CharacterisationTests/Scenarios.cs` — register the new scenario

Add a 6th scenario (`"cross-schema"`) with:
- `Programmatic` builder that constructs the equivalent `Package` in code (mirroring the existing scenarios).
- XML / JSON loaders pointed at the fixture files above.

The `AsTestCases` Cartesian product automatically picks it up: 6 scenarios × 2 loaders = 12 cases.

### Skip behaviour when test DB doesn't allow CREATE SCHEMA

The new scenario's test cases call `Assert.Ignore("Cross-schema schema setup failed; skipping")` if `_crossSchemaAvailable == false`. Documented in the test class XMLdoc.

### Golden recording

Per existing `GoldenStore` flow:

```
GOLDEN_MODE=record GOLDEN_SCENARIO=cross-schema dotnet test --filter "Scenario_Matches_Golden"
```

Records `Tests/CharacterisationTests/Goldens/cross-schema.sql`. Verify the recorded SQL contains `INSERT INTO [audit].[SchemaTestParent]` (qualified) AND `INSERT INTO [SchemaTestChild]` (bare — because the operator's child config can also be bare-named; mock decision: leave the child as bare-named in the config to verify mixed-mode emission works).

Actually — **decision**: make the `dbo.SchemaTestChild` config `Schema="dbo"` qualified to verify the qualified-emission path explicitly. Bare-name back-compat is already covered by the original 5 goldens. Cross-schema is exclusively new territory; favour explicitness.

### Tests

The new tests are the TestCaseSource expansion (6th scenario, both loaders). Per the existing `Scenario_Matches_Golden` test, no new test methods are needed — just the new scenario in `Scenarios.cs` + the new golden + the fixtures.

The skip behaviour is itself a test of sorts; document the operator-permission-required precondition in the test class.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-04 157 + new (12 - 0 baseline) = depends on whether existing test count counted these:
  - If `Scenarios.AsTestCases()` reports 10 cases today (5 × 2), the new scenario adds 2 cases → **159 tests**.
- All 10 existing characterisation cases match their goldens byte-identically (regression net).
- New cross-schema golden recorded once; subsequent runs match it.

## Acceptance Criteria

- [ ] Test-DB schema + tables created idempotently in `[OneTimeSetUp]`.
- [ ] `audit.SchemaTestParent` + `dbo.SchemaTestChild` exist with one FK between them and seed data.
- [ ] `Tests/CharacterisationTests/CrossSchemaScenario.xml` + `Json/cross-schema.json` exist.
- [ ] `Scenarios.cs` registers the 6th scenario with both loaders.
- [ ] `Goldens/cross-schema.sql` is recorded; contains `[audit].[SchemaTestParent]` qualified emission.
- [ ] Cross-schema test cases skip cleanly (`Assert.Ignore`) when the operator can't `CREATE SCHEMA` — verified by reading the test code (don't deliberately fail-test the skip path).
- [ ] All 10 existing characterisation cases still pass byte-identically (regression net).
- [ ] `dotnet build` 0 errors; `dotnet test` 159/159 (or 157 + 2; exact depends on baseline).

## References

- ADR: [`adr/011-engine-schema-awareness.md`](../../adr/011-engine-schema-awareness.md).
- Pattern: existing characterisation harness.
- Backlog reference: the "deferred cross-schema scenario" mentioned in `characterisation-tests`.
- Depends on: [04 — T4 emission](./04-engine-schema-aware-resolution-t4-emission.md).
