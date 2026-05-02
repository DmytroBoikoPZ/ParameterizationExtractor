# 05 — MS.DI Migration — Composition root + remove MEF

## Goal

Replace `AppBootstrap`'s MEF-flavoured composition with a `HostBuilder` (or equivalent) `IServiceCollection` composition root. Remove `System.Composition.*` PackageReferences and any `[Export]` attribute leftovers. Result: clean MS.DI app with no MEF references.

## Track

dotnet (refactor)

## What Exists

- `AppBootstrap` (MEF-shaped) in the CLI project.
- `[Export]` attributes still in source (left from step 03 for incremental safety).
- `System.Composition.AttributedModel` and `System.Composition.Runtime` PackageReferences in `ParameterizationExtractor.csproj`.

## What to Build

- Replace `AppBootstrap.CreateAppBuilder` with a host-builder-based composition that calls the extension methods designed in step 02. Match the shape proposed and approved there.
- Delete every remaining `[Export]` / `[Shared]` attribute and any MEF-only types.
- Remove `System.Composition.*` PackageReferences from `ParameterizationExtractor.csproj`.
- Verify nothing else in the solution references `System.Composition` (e.g. transitive packages).
- Optional: if `AppBootstrap` becomes a thin wrapper, inline it into `Program.cs` and delete the file. Decision recorded in `## Notes`.

## Acceptance Criteria

- [ ] No `using System.Composition.*;` directive remains in the C# source.
- [ ] No `[Export]` / `[Import]` / `[ImportMany]` / `[Shared]` attribute remains.
- [ ] `System.Composition.AttributedModel` and `System.Composition.Runtime` are not in any project file.
- [ ] `dotnet build "SQL Buldozer.sln"` succeeds.
- [ ] Characterisation suite green.

## References

- Related ADRs: `001-mef-di-container.md`, `006-msdi-container.md`.
- Related methodology: `docs/methodology/dotnet-cli.md` § Composition.
- Depends on: `04-msdi-migration-imports.md`.
