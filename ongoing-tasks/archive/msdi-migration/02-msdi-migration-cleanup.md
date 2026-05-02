# 02 — MS.DI Migration — Cleanup unused MEF references

## Goal

Remove the dead-code MEF artefacts that step 01 surfaced: two unused `System.Composition.*` PackageReferences and two commented-out `[Export]` lines. No behaviour change. After this step the codebase has zero residual MEF surface — the only thing left to fix is the docs (step 03).

## Track

CLEANUP (per `prompts/execution.md` task classification — file deletion / dead-code removal; "ensure existing tests still pass; write new test only if a coverage gap is found").

## What Exists

- `ParameterizationExtractor/ParameterizationExtractor.csproj`:
  - `<PackageReference Include="System.Composition.AttributedModel" Version="7.0.0" />`
  - `<PackageReference Include="System.Composition.Runtime" Version="7.0.0" />`
  - Both transitively unused. No source file imports `System.Composition.*` (verified via grep in step 01).
- `ParameterizationExtractor/FromFileExecutor.cs:13` — `//[Export(typeof(IExecutor))]` (commented out).
- `ParameterizationExtractor/DSLExecutor.cs:16` — `//[Export(typeof(IExecutor))]` (commented out).

## What to Build

- Remove the two `<PackageReference>` lines from `ParameterizationExtractor.csproj`.
- Remove the two `//[Export(typeof(IExecutor))]` comment lines (they're confusing — they suggest a wiring that no longer exists; deleting them is more honest than leaving them as historical residue).
- `dotnet restore` + `dotnet build` + `dotnet test`.

## Acceptance Criteria

- [ ] `ParameterizationExtractor.csproj` no longer references `System.Composition.AttributedModel` or `System.Composition.Runtime`.
- [ ] No `//[Export...]` comment remains in `FromFileExecutor.cs` or `DSLExecutor.cs`.
- [ ] `grep "System.Composition"` over `*.cs` and `*.csproj` returns 0 matches.
- [ ] `dotnet build "SQL Buldozer.sln"` — 0 errors.
- [ ] `dotnet test "SQL Buldozer.sln"` — all 33 tests pass; characterisation goldens unchanged.

## References

- Related ADRs: `001-mef-di-container.md` (the policy that turns out to have been wrong from the start; superseded by ADR-006 in step 03).
- Related methodology: `docs/methodology/dotnet-cli.md` § Composition (currently describes MEF — fixed in step 03).
- Depends on: `01-msdi-migration-inventory.md`.
