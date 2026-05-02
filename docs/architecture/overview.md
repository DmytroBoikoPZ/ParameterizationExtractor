# SQL Buldozer — Architecture Overview

> System-level topology. Read at session start. Updated when the topology changes.
> Feature-level architecture lives in `ongoing-tasks/{feature}/feature-architecture.md`, not here.

---

## 1. System Context

SQL Buldozer is a developer-side CLI tool. A single operator runs it locally (or on a dev/CI machine), pointed at a SQL Server database and an `ExtractConfig.xml` map. It reads rows, walks foreign-key relations to collect dependent rows, and emits parameterised `INSERT`/`UPDATE` SQL scripts on disk. There is no service surface, no inbound network, no users beyond the operator.

```
┌──────────────────┐       args + ExtractConfig.xml + appsettings.json
│  Developer /     │ ─────────────────────────────────►  ┌──────────────────┐
│  CI runner       │                                     │  SQL Buldozer    │
│  (operator)      │ ◄─── stdout/stderr (Serilog) ─────  │  CLI (.NET 6)    │
└──────────────────┘       *.sql output files            └─────┬────────────┘
                                                               │ T-SQL queries
                                                               │ (System.Data.SqlClient)
                                                               ▼
                                                       ┌──────────────────┐
                                                       │  Source SQL      │
                                                       │  Server DB       │
                                                       │  (read-only)     │
                                                       └──────────────────┘
```

---

## 2. Components

| Component | Stack | Purpose | Path |
|-----------|-------|---------|------|
| `ParameterizationExtractor` (CLI) | .NET 6 / C# (Exe) | CLI entry point, host, DI/logging composition, app lifecycle | `ParameterizationExtractor/` |
| `ParameterizationExtractor.Common` | C# (netstandard2.0) | Shared abstractions; dependency-light | `ParameterizationExtractor.Common/` |
| `ParameterizationExtractor.Logic` | C# (netstandard2.0) | Core engine — graph traversal, extraction strategies, SQL generation via T4 | `ParameterizationExtractor.Logic/` |
| `ParameterizationExtractor.DSL` | F# (netstandard2.0, FParsec) | Parser for the extraction DSL; AST | `ParameterizationExtractor.DSL/` |
| `ParameterizationExtractor.DSL.Connector` | C# (netstandard2.0) | Translates F# DSL AST → Logic engine config | `ParameterizationExtractor.DSL.Connector/` |
| `Tests` | C# (.NET 6, NUnit) | Cross-module test project | `Tests/` |

References (per `<ProjectReference>` in each csproj/fsproj):

- `CLI` → `Common`, `Logic`, `DSL`, `DSL.Connector`
- `DSL.Connector` → `DSL`, `Logic`
- `DSL` → `Logic`
- `Logic` → `Common`
- `Common` → (nothing)

Back-edges are forbidden (see the .NET layering tripwire in `CLAUDE.md`).

---

## 3. Data Flow

### 3.1 Inbound

The operator launches the CLI with arguments parsed by `FluentCommandLineParser`. Configuration is layered: `appsettings.json` (logging defaults + named connection strings) + environment variables. The actual extraction map is `ExtractConfig.xml` — *this file is what the CLI executes*: the list of tables, per-table `ExtractStrategy` (FKDependency / OnlyChildren / OnlyParent / OnlyOneTable), `Where` filters, and SQL build directives. `ClearingPackage.xml` shipped in the repo is a worked example of an `ExtractConfig`-shaped file. Optionally an extraction DSL file is also accepted and parsed by the F# parser.

### 3.2 Processing

```
operator args + ExtractConfig.xml + (optional) DSL text
        │
        ▼
ParameterizationExtractor (CLI)
        │   - parse args (FluentCommandLineParser)
        │   - bind appsettings + env to POCOs
        │   - if DSL provided: text → DSL (F#, FParsec) → AST → DSL.Connector → engine config
        │
        ▼
ParameterizationExtractor.Logic
        │   - open SqlConnection (System.Data.SqlClient)
        │   - walk graph from seed records, applying per-table ExtractStrategy
        │   - build hierarchical in-memory structure of dependent records
        │   - render INSERT/UPDATE T-SQL via T4 template (Templates/DefaultTemplate.tt)
        │     parents-before-children
        ▼
.sql files on disk (+ Serilog logs to console + file)
```

### 3.3 Outbound

Generated `.sql` files written to a configured output directory; structured logs to console + file (Serilog Console + File sinks).

---

## 4. Data Stores

| Store | Technology | Purpose | Owner |
|-------|------------|---------|-------|
| Source database | Microsoft SQL Server | Read-only graph being extracted from | `Logic` (only module that opens connections) |
| Output filesystem | Local filesystem | Generated `.sql` scripts; Serilog file sink | `CLI` writes; `Logic` produces strings |

No queues, no HTTP, no message bus, no cache.

---

## 5. Cross-cutting concerns

- **Authentication / Authorization:** N/A — local-machine tool. SQL Server connection supports both Windows auth (`Integrated Security=True`, the shipped default) and SQL auth (override the connection string via `appsettings.json` or env vars).
- **Logging / Observability:** Serilog (Console + File sinks) configured via the `Serilog` section of `appsettings.json`. No metrics, no tracing.
- **Configuration:** `Microsoft.Extensions.Configuration` (JSON + env), bound in `Program.cs`. The shipped `appsettings.json` is real, committed config — Serilog defaults plus named connection strings under `ConnectionStrings.*` (defaults point at non-prod dev SQL Server instances). CLI args override.
- **DI container:** `Microsoft.Extensions.DependencyInjection`. Composition root is `AppBootstrap.CreateAppBuilder(args)`, which returns an `IAppBuilder` — a thin custom wrapper around `new ServiceCollection()` (see `ParameterizationExtractor/Common/SqlBuldozerApp.cs`). Module-level extension methods `.AddMSSQL()` and `.AddExecutor()` register parts via `services.AddSingleton<...>()` / `AddTransient<...>()`. `IServiceProvider` is built via `ServiceCollection.BuildServiceProvider()` in `AppBuilder.Build()`. Constructor injection only — no attribute-based registration. (ADR-001 originally recorded MEF as the container; that was incorrect. See ADR-006.)
- **Secret management:** Connection strings live in `appsettings.json` or env vars; no vault / secret store. The committed defaults are non-prod dev DBs by design.
- **Error handling:** Explicit process exit codes via `ExitCode` enum in `Program.cs`. Fatal errors logged before exit.

---

## 6. Trust boundaries

The only boundary that exists: CLI process ↔ SQL Server. Connection is configured by the operator. The tool is run inside whatever trust zone the operator already has DB access from — there is no privilege escalation or impersonation.

---

## 7. Active ADRs

See [`adr/readme.md`](../../adr/readme.md) for the full index. The architectural decisions that most directly shape this overview:

- `006-msdi-container.md` — DI container is `Microsoft.Extensions.DependencyInjection` (supersedes ADR-001's incorrect MEF claim).
- `002-module-layering.md` — Module reference graph is one-way; back-edges forbidden.
- `003-fsharp-fparsec-dsl.md` — Extraction DSL is implemented in F# / FParsec.
- `004-t4-sql-generation.md` — SQL `INSERT`/`UPDATE` text is generated via the T4 template `Templates/DefaultTemplate.tt`.
- `005-freeze-fsharp-dsl.md` — F# DSL is frozen; no new feature work.

---

## 8. Where to add detail

- **Feature-level architecture** — `ongoing-tasks/{feature}/feature-architecture.md`
- **Per-stack recipes** — `docs/methodology/dotnet-cli.md` and `docs/methodology/fsharp-dsl.md`
- **Specific decisions** — `adr/NNN-*.md`
- **Hard rules / tripwires** — `CLAUDE.md`
