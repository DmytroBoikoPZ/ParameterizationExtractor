# Characterisation Tests

Pinned-output regression net for the SQL Buldozer engine. Scenarios run end-to-end against the dev SQL Server (`budzdorov_Core`); each emits SQL that is normalised and compared against a committed golden file.

## Layout

| Path | Purpose |
|---|---|
| `CharacterisationTests.cs` | Main test fixture. `[TestCaseSource]`-driven over `Scenarios.All`. |
| `Scenarios.cs` | Registry of scenarios — one row per `(name, factory)`. Disabling a scenario = remove a row. |
| `Goldens/` | Pinned SQL output, one file per scenario. See [Goldens/_README.md](./Goldens/_README.md) for the regen workflow. |
| `Harness/` | `TestDbConfig` (config loader), `TestUnitOfWorkFactory` (test-only `IUnitOfWorkFactory`), `CharacterisationRunner` (engine wiring), `SqlNormalizer` (CRLF→LF, trailing whitespace, header timestamp masking), `GoldenStore` (read/write/mode). |
| `ConnectivityTests.cs` | One-test smoke: opens a connection, runs `SELECT 1`. Independent of the harness. |
| `CharacterisationRunnerSmokeTests.cs` | One-test smoke: end-to-end harness exercise on scenario 1. Survives even if all goldens are regenerated. |
| `SchemaSurvey.md` | Step-02 deliverable: per-scenario tables, seed IDs, FK shape, hazards. |

## Conceptual model

- **`SchemaSurvey.md` is the spec.** What each scenario seeds, walks, and why.
- **`Scenarios.cs` is the code form of the spec.** Adding/removing a scenario = one-line edit in `All`.
- **`Goldens/*.sql` are the pinned outputs.** Updated only via record mode (see `Goldens/_README.md`).
- **`CharacterisationTests.Scenario_Matches_Golden(scenario)` is the only test that compares.** Every scenario in `All` becomes one `[TestCase]` automatically.

## Where to read more

- Feature scope, steps, decisions: [`ongoing-tasks/characterisation-tests-checklist.md`](../../ongoing-tasks/characterisation-tests-checklist.md)
- Feature architecture (data flow, components): [`ongoing-tasks/characterisation-tests/feature-architecture.md`](../../ongoing-tasks/characterisation-tests/feature-architecture.md)
- Per-scenario seed/walk/hazard detail: [`SchemaSurvey.md`](./SchemaSurvey.md)
- Regenerate a golden: [`Goldens/_README.md`](./Goldens/_README.md)
