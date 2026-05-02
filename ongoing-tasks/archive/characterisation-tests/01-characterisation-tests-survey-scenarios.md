# 01 — Characterisation Tests — Survey extraction scenarios

## Goal

Produce a written list of distinct extraction scenarios that the test suite must cover, derived from a read of the existing config files (`ExtractConfig.xml`, `ClearingPackage.xml`), the four `ExtractStrategy` variants, and the real schema of `budzdorov_Core` (the test target — see step 02). Output is a markdown table in the checklist's `## Notes` section — not code.

## Track

cross-cutting (planning step, no production code touched)

## What Exists

- `ParameterizationExtractor/ExtractConfig.xml` — the shipped config.
- `ParameterizationExtractor/ClearingPackage.xml` — sample working config.
- `ParameterizationExtractor.Logic/` — engine entry points; pattern exemplar for "what gets called when running an extraction".
- README "Extraction Strategy" section enumerates the four strategies + `Where`.
- Test target: `budzdorov_Core` on `129.212.168.210,1433` — real ClinicV2 schema, ~370 tables. **The walker reads `sys.foreign_keys` exclusively** — confirmed by reading `MSSQLSourceSchema.Init` and `DependencyBuilder.GetRelatedTables`. The DBA is adding the FK constraints needed by the test scenarios before this step's acceptance; coordinate the scenario list with the FK additions.

## What to Build

- A scenario list, one row per scenario, with: name (kebab-case), strategy under test, real tables involved (schema-qualified), seed-row `WHERE` clause, expected FK chain the scenario exercises, one-line description, notes on what makes it interesting.
- Coverage check: every `ExtractStrategy` variant is touched by at least one scenario; at least one scenario uses a `Where` filter; at least one scenario exercises a multi-level FK chain (3+ tables deep).
- Coordinate with the DBA: the FKs needed by the chosen scenarios must exist in `budzdorov_Core` by the end of this step. Capture the list of FKs the scenario set depends on so they can be reproduced if the DB is rebuilt.
- Pick tables and seed predicates that produce **small, stable result sets** — narrow `WHERE` clauses on indexed columns, deterministic seed IDs, no `TOP n` without `ORDER BY`. Avoid `Prescription*` / `Service*` tables for seed bases (8M-39M rows each); use them only as downstream targets when the seed produces a tight slice.
- Target ~5-8 scenarios. Aim for the smallest set that pins the engine's observable behaviour.

## Acceptance Criteria

- [ ] Scenario list is appended to the checklist's `## Notes` section, dated.
- [ ] Each of `FKDependency` / `OnlyChildren` / `OnlyParent` / `OnlyOneTable` appears in at least one scenario.
- [ ] At least one scenario uses a `Where` clause.
- [ ] At least one scenario has a multi-level FK chain (3+ tables deep), with the FKs declared in `budzdorov_Core` (added by the DBA).
- [ ] Each scenario names real schema-qualified tables that exist in `budzdorov_Core` and a deterministic seed-row `WHERE` clause.
- [ ] List of FKs the scenario set depends on is captured in `## Notes` (so the DB can be rebuilt if needed).
- [ ] Reviewer (human) signs off on the list before step 02 starts.

## References

- Related ADRs: none directly; `004-t4-sql-generation.md` documents the SQL emission surface that's being pinned.
- Related methodology: `docs/methodology/dotnet-cli.md` § Testing.
- Depends on: —
