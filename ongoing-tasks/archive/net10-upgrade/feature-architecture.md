# .NET 10 Upgrade — Architecture Overview

> Internal modernisation. No production data flow change. Architecture here is the framework/dependency matrix and the surfaces most likely to break.

---

## 1. Pipeline / Integration

There is no runtime pipeline change. The upgrade touches only:

- `*.csproj` / `*.fsproj` `TargetFramework` and `<PackageReference>` versions.
- Source files where a bumped API changed shape (e.g., `IConfiguration` extension surface, logging configuration).

```
csproj/fsproj      ─┐
PackageReference    │──► dotnet restore ──► dotnet build ──► dotnet test (characterisation suite)
source adjustments ─┘
```

| Stage | What happens | Where |
|-------|--------------|-------|
| 1. Bump | Edit framework + package versions | `*.csproj`, `*.fsproj` |
| 2. Restore | Resolve transitives | `obj/project.assets.json` |
| 3. Compile | Surface breaking API changes | source files |
| 4. Test | Characterisation suite proves no observable change | `Tests/CharacterisationTests/` |

---

## 2. Component Diagram

Per-project target matrix (current → target):

| Project | Current TargetFramework | Target TargetFramework | Notes |
|---|---|---|---|
| `ParameterizationExtractor` (CLI exe) | `net6.0` | `net10.0` | Hard bump |
| `Tests` | `net6.0` | `net10.0` | Hard bump |
| `ParameterizationExtractor.Common` | `netstandard2.0` | `net10.0` (proposed) | Evaluate; nothing external consumes it |
| `ParameterizationExtractor.Logic` | `netstandard2.0` | `net10.0` (proposed) | Evaluate; same |
| `ParameterizationExtractor.DSL.Connector` | `netstandard2.0` | `net10.0` (proposed) | Evaluate; same |
| `ParameterizationExtractor.DSL` (F#) | `netstandard2.0` | `net10.0` (proposed) | Frozen per ADR-005; still must build |

The proposed netstandard → net10 bumps are conditional. If a meaningful reason to keep netstandard2.0 emerges (an external NuGet consumer exists that we did not know about), keep it. Default is to simplify.

---

## 3. Data Flow

### 3.1 Inbound / Ingestion

N/A — no runtime data flow change.

### 3.2 Processing / Event Handling

The breaking-change surfaces to watch:

- `Microsoft.Extensions.Configuration` — `AddJsonFile` / `AddEnvironmentVariables` overload shapes changed across major versions; usually source-compatible.
- `Microsoft.Extensions.DependencyInjection` — present transitively via Logging only (the app's DI is MEF). Should not surface.
- `Microsoft.Extensions.Logging` — provider registration and the `ILoggerFactory.AddSerilog` extension moved between packages historically.
- `Serilog.Settings.Configuration` — `ReadFrom.Configuration(IConfiguration)` API has been stable; `LoggerConfiguration` constructor unchanged.
- `FluentCommandLineParser.NETStandard` — package may need replacement; check for a current maintained equivalent.
- `System.Composition.AttributedModel` / `System.Composition.Runtime` — explicitly out of scope for this feature; MEF stays for now.

### 3.3 Query / Serving

N/A.

---

## 4. Data Stores Summary

N/A — same SQL Server target, same `appsettings.json` shape.

---

## 5. Extension Points

If a package (`FluentCommandLineParser.NETStandard` is the prime suspect) is unmaintained on .NET 10, the replacement choice is **architectural** and requires human approval before swapping.

---

## 6. Security & Isolation

- No new runtime surface area; no new endpoints, no new I/O.
- `System.Data.SqlClient` is being kept — separate decision needed for the migration to `Microsoft.Data.SqlClient` (it has different default behaviour for encryption / cert validation in newer versions).
