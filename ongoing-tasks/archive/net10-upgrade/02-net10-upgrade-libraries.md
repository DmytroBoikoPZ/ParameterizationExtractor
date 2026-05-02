# 02 — .NET 10 Upgrade — Library projects

## Goal

Decide whether to keep the four library projects on `netstandard2.0` or bump them to `net10.0`. Apply the decision.

## Track

dotnet (libraries)

## What Exists

- `ParameterizationExtractor.Common.csproj` — `netstandard2.0`
- `ParameterizationExtractor.Logic.csproj` — `netstandard2.0`
- `ParameterizationExtractor.DSL.Connector.csproj` — `netstandard2.0`
- `ParameterizationExtractor.DSL.fsproj` — `netstandard2.0` (frozen per ADR-005)
- All four projects are consumed only inside this solution; no external NuGet packages depend on them.

## What to Build

- A short proposal in the checklist's `## Notes`: keep `netstandard2.0` (and why) or bump to `net10.0` (and why). Default recommendation is bump-all — there's no external consumer, and it removes a layer of source-compatibility friction.
- *Requires human approval before edit.*
- After approval: edit each `.csproj` / `.fsproj` and bump (or leave). `dotnet restore` + `dotnet build`.

## Acceptance Criteria

- [ ] Decision recorded with rationale, dated, in `## Notes`.
- [ ] Selected target framework applied uniformly across the four library projects (or, if mixed, the rationale per project recorded).
- [ ] `dotnet build "SQL Buldozer.sln"` succeeds.
- [ ] Characterisation suite still green.

## References

- Related ADRs: `005-freeze-fsharp-dsl.md` — DSL stays buildable but no feature work.
- Related methodology: `docs/methodology/dotnet-cli.md`, `docs/methodology/fsharp-dsl.md`.
- Depends on: `01-net10-upgrade-bump-targets.md`.
