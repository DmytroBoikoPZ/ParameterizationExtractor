# 01 — .NET 10 Upgrade — Bump CLI + Tests TargetFramework

## Goal

Move the two `net6.0` projects (`ParameterizationExtractor`, `Tests`) to `net10.0`. Do **not** touch package versions yet — that's step 03/04. The point of this step is to surface framework-level breaks in isolation from package-level breaks.

## Track

dotnet (CLI + tests)

## What Exists

- `ParameterizationExtractor/ParameterizationExtractor.csproj` — `<TargetFramework>net6.0</TargetFramework>`
- `Tests/Tests.csproj` — `<TargetFramework>net6.0</TargetFramework>`
- All other projects target `netstandard2.0`.

## What to Build

- Edit `ParameterizationExtractor.csproj`: `net6.0` → `net10.0`.
- Edit `Tests.csproj`: `net6.0` → `net10.0`.
- `dotnet restore` + `dotnet build "SQL Buldozer.sln"`.
- Record the build output (errors + warnings) in the checklist `## Notes`. Any compile errors here are framework-level — likely small, often zero.

## Acceptance Criteria

- [ ] Both csproj files target `net10.0`.
- [ ] `dotnet build "SQL Buldozer.sln"` succeeds. Warnings allowed; errors not.
- [ ] Characterisation suite still green via `dotnet test "SQL Buldozer.sln"`.
- [ ] No package versions changed in this step.

## References

- Related ADRs: —
- Related methodology: `docs/methodology/dotnet-cli.md`.
- Depends on: `characterisation-tests-checklist.md` (must be green).
