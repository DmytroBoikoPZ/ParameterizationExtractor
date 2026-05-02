# 05 — Characterisation Tests — Wire into NUnit suite

## Goal

Expose every scenario as an NUnit test under the existing `Tests/` project so that `dotnet test "SQL Buldozer.sln"` runs the characterisation suite alongside any existing tests.

## Track

cross-cutting

## What Exists

- Harness, fixtures, goldens from steps 02-04.
- Existing `Tests/Tests.csproj` (NUnit 3.12.0).

## What to Build

- `Tests/CharacterisationTests/CharacterisationTests.cs` — one `[TestCaseSource]`-driven test fixture iterating the scenario list, calling the harness, asserting `actual == golden`.
- A scenario-source provider that reads scenario names from a single committed registry (e.g. `Tests/CharacterisationTests/Scenarios.cs` with a `static IEnumerable<TestCaseData>`).
- `Tests/CharacterisationTests/_README.md` — pointer to feature-architecture and the regen workflow from step 04.

## Acceptance Criteria

- [ ] `dotnet test "SQL Buldozer.sln"` exits 0 with the characterisation tests included.
- [ ] Disabling a scenario is a one-line change in the registry, not a code edit per test method.
- [ ] An intentional one-character change to a golden file makes exactly one test fail with a clear actual-vs-expected diff.
- [ ] No existing test breaks because of this step.

## References

- Related ADRs: —
- Related methodology: `docs/methodology/dotnet-cli.md` § Testing.
- Depends on: `04-characterisation-tests-goldens.md`.
