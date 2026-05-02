# 03 — Characterisation Tests — Test harness

## Goal

Build the NUnit-side harness that, given a scenario name, runs the engine against the fixture DB and returns the generated SQL as a normalised string ready to compare against a golden file.

## Track

cross-cutting (test infrastructure)

## What Exists

- `ParameterizationExtractor.Logic` — engine; entry points the CLI uses today.
- `ParameterizationExtractor` (CLI) `Program.cs` and bootstrap — pattern exemplar for *how* the engine is invoked. The harness should call the same composition, not re-implement it.
- `Tests/appsettings.test.json` and `SchemaSurvey.md` from step 02.

## What to Build

- `Tests/CharacterisationTests/Harness/CharacterisationRunner.cs` — single class with one method, e.g. `string RunScenario(string scenarioName, ExtractConfigInput config)`. Returns the engine's emitted SQL after normalisation (line endings → `\n`, trailing whitespace stripped, GUID/timestamp tokens replaced with stable placeholders if any appear).
- `Tests/CharacterisationTests/Harness/SqlNormalizer.cs` — pure function; testable in isolation.
- `Tests/CharacterisationTests/Harness/TestDbConfig.cs` — loads `Tests/appsettings.test.json` via `Microsoft.Extensions.Configuration`, exposes `ConnectionStrings:Source`. No schema setup, no teardown — the dev DB is treated as a read-only constant.
- *Proposal-only — requires human approval before implementation:* whether the harness invokes the engine via the CLI's existing composition (MEF in current code) or via a smaller programmatic seam. This decision gates the implementation; do not build until accepted.

## Acceptance Criteria

- [ ] `CharacterisationRunner` runs at least one scenario end-to-end against `budzdorov_Core` and returns a non-empty SQL string.
- [ ] `SqlNormalizer` has its own NUnit tests (round-trip + idempotence + a couple of fixed strings).
- [ ] `TestDbConfig` resolves the connection string from `Tests/appsettings.test.json`; harness fails fast with a clear error if the file or `ConnectionStrings:Source` is missing.
- [ ] No new production code in `Logic` / `CLI` — harness only consumes existing surface.
- [ ] Harness must not mutate the dev DB (no `INSERT`/`UPDATE`/`DELETE`/DDL). A round-trip diff via `sys.dm_db_index_usage_stats` or equivalent is sufficient evidence.
- [ ] Decision recorded (in checklist `## Notes`) on the engine-invocation seam before any implementation lands.

## References

- Related ADRs: `001-mef-di-container.md` (current DI; relevant to invocation seam choice).
- Related methodology: `docs/methodology/dotnet-cli.md`.
- Depends on: `02-characterisation-tests-build-fixture-db.md`.
