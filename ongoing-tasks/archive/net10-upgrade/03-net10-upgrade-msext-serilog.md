# 03 — .NET 10 Upgrade — Microsoft.Extensions.* + Serilog

## Goal

Bump every `Microsoft.Extensions.*` package from `3.1.0` to a version aligned with the target framework picked in steps 01-02. Bump Serilog and its sinks to current. No new packages added.

## Track

dotnet (dependencies)

## What Exists

Current `Microsoft.Extensions.*` references in `ParameterizationExtractor.csproj`:

- `Microsoft.Extensions.Configuration` 3.1.0
- `Microsoft.Extensions.Configuration.EnvironmentVariables` 3.1.0
- `Microsoft.Extensions.Configuration.FileExtensions` 3.1.0
- `Microsoft.Extensions.Configuration.Json` 3.1.0
- `Microsoft.Extensions.DependencyInjection` 3.1.0
- `Microsoft.Extensions.Logging` 3.1.0
- `Microsoft.Extensions.Logging.Abstractions` 3.1.0
- `Microsoft.Extensions.Logging.Configuration` 3.1.0
- `Microsoft.Extensions.Logging.Console` 3.1.0
- `Microsoft.Extensions.Logging.Debug` 3.1.0

Library projects also reference `Microsoft.Extensions.Logging.Abstractions` 3.1.0.

Current Serilog references:

- `Serilog` 2.9.0
- `Serilog.Extensions.Logging` 3.0.1
- `Serilog.Settings.Configuration` 3.1.0
- `Serilog.Sinks.Console` 3.1.1
- `Serilog.Sinks.File` 4.1.0

## What to Build

- Bump every `Microsoft.Extensions.*` PackageReference to a version matching the target framework. Do this across every `.csproj` and `.fsproj` that has one.
- Bump every Serilog package to current.
- `dotnet restore` + `dotnet build "SQL Buldozer.sln"`.
- Capture compile errors / warnings in `## Notes`. Likely friction points: `IConfiguration` extension method overloads, `LoggerConfiguration.ReadFrom.Configuration` signature.

## Acceptance Criteria

- [ ] Every `Microsoft.Extensions.*` reference is on the same major version, matching the target framework.
- [ ] Every Serilog reference is on a current version.
- [ ] `dotnet build` succeeds.
- [ ] Characterisation suite still green.
- [ ] No new packages introduced as part of fixing breaks (any new dependency is a separate, approved decision).

## References

- Related ADRs: —
- Related methodology: `docs/methodology/dotnet-cli.md`.
- Depends on: `02-net10-upgrade-libraries.md`.
