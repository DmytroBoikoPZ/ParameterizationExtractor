# 02 — Characterisation Tests — Test DB config + schema survey

## Goal

Wire the `Tests/` project to the existing dev SQL Server (`budzdorov_Core` on `129.212.168.210,1433`) and produce a short survey of which real tables the step-01 scenarios will touch. No fixture creation, no synthetic data — the test target is a real, disposable dev DB.

## Track

cross-cutting (test infrastructure)

## What Exists

- **Test DB:** SQL Server 2025 on Ubuntu 24.04 Linux at `129.212.168.210,1433`, database `budzdorov_Core`. Real ClinicV2 schema. Disposable — connection string committed in test config is intentional, per the same convention as the CLI's `appsettings.json`.
- **Schema scale:** ~370 tables across 17 schemas (`dbo`, `clinic`, `cdc`, `templates`, `fin`, `eln`, `security`, `hospital`, `medicine`, `ui_extensions`, `document`, `organization`, `factors`, `import`, `cache`, `history`, `estore`).
- **The walker reads `sys.foreign_keys` exclusively.** Confirmed by reading `MSSQLSourceSchema.Init` (calls `ObjectMetaDataProvider.GetDependentTables`, which queries `sys.foreign_keys` + `sys.foreign_key_columns`) and `DependencyBuilder.GetRelatedTables` (only consumes `_schema.DependentTables`; no config-declared relations). The DBA is adding the FKs the scenario set needs before step 01 finishes.
- **Existing CLI config pattern:** `ParameterizationExtractor/appsettings.json` is committed with real connection strings under `ConnectionStrings.*`. Test config mirrors this pattern.

## What to Build

- `Tests/appsettings.test.json` (committed) with at minimum:
  ```json
  {
    "ConnectionStrings": {
      "Source": "Data Source=129.212.168.210,1433;Initial Catalog=budzdorov_Core;Application Name=BuldozerCharacterisationTests;User Id=sa;Password=Bz!Dev2026Sql;TrustServerCertificate=True"
    }
  }
  ```
  Copy-to-output-directory in `Tests.csproj` so the file ends up in `bin/.../net*.0/`.
- `Tests/CharacterisationTests/SchemaSurvey.md` — short markdown produced by reading the real schema, documenting:
  - Which tables each step-01 scenario will touch, and why those tables (size manageable, FK-shape coverage, `Where`-filter shape).
  - For each scenario's seed query: a `WHERE` clause that produces a small, deterministic seed set (e.g., `WHERE Id IN (...specific IDs...)` — not `TOP 5`, which is non-deterministic without `ORDER BY`).
- A short prose note in the checklist `## Notes` confirming the FKs needed by the step-01 scenario list exist in `budzdorov_Core`, with a `SELECT * FROM sys.foreign_keys` snapshot attached. (Re-run after any DB rebuild.)

## Acceptance Criteria

- [ ] `Tests/appsettings.test.json` exists, is copied to test output, and is consumable via `Microsoft.Extensions.Configuration` from inside an NUnit `[OneTimeSetUp]`.
- [ ] A trivial connectivity NUnit test (`SELECT 1`) using the configured connection string passes against the dev DB.
- [ ] `SchemaSurvey.md` lists, per scenario from step 01, the exact tables involved and their seed-row WHERE clauses.
- [ ] FKs required by the scenario set exist in `budzdorov_Core` (verified via `sys.foreign_keys` snapshot in `## Notes`).
- [ ] No synthetic fixture SQL files committed. No schema setup/teardown logic — the DB is treated as read-only by the test harness.

## References

- Related ADRs: `004-t4-sql-generation.md` (output surface being characterised).
- Related methodology: `docs/methodology/dotnet-cli.md` § Testing.
- Depends on: `01-characterisation-tests-survey-scenarios.md`.
