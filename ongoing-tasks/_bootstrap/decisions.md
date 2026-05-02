# Bootstrap Decisions

> Phase 2 (Interview) output. Mode: **brownfield**. Approved by human on 2026-05-02.
> Apply phase reads this file verbatim. Edit directly to change anything.

## Project identity

- **name:** SQL Buldozer
- **one-liner:** A CLI tool that extracts data from a relational database and emits parameterised `INSERT`/`UPDATE` SQL scripts, for developers preparing parameterisation scripts.
- **paragraph:** SQL Buldozer is a .NET 6 CLI tool that walks a relational database as a graph — tables are nodes, foreign keys are edges — starting from a set of seed records and a per-table extraction strategy. It collects the dependent rows in memory as a hierarchical structure, then emits parameterised `INSERT`/`UPDATE` SQL scripts (parents before children) suitable for replaying the extracted slice into another environment. A small F# DSL (FParsec) describes the extraction config; a configurable C# core does the walking and SQL generation. The intent is to automate the manual chore of preparing parameterisation scripts during development.

## Stacks

> Framing: **one CLI app composed of modules** — not separate services. Layout convention: one project per module under the repo root, named `ParameterizationExtractor[.Submodule]`, all built through the single `SQL Buldozer.sln`. C# `RootNamespace` is `Quipu.ParameterizationExtractor[.Submodule]`. Non-CLI projects target `netstandard2.0`; CLI and Tests target `net6.0`.

| Stack | Path | Purpose |
|---|---|---|
| .NET 6 / C# (CLI exe) | `ParameterizationExtractor/` | CLI entry point, host, DI/logging composition, app lifecycle |
| .NET / C# (netstandard2.0 lib) | `ParameterizationExtractor.Common/` | Shared abstractions; dependency-light |
| .NET / C# (netstandard2.0 lib) | `ParameterizationExtractor.Logic/` | Core engine — graph traversal, extraction strategies, SQL generation via T4 |
| F# (netstandard2.0 lib, FParsec) | `ParameterizationExtractor.DSL/` | Parser for the extraction DSL; AST |
| .NET / C# (netstandard2.0 lib) | `ParameterizationExtractor.DSL.Connector/` | Translates F# DSL AST → Logic engine config |
| .NET 6 / C# (NUnit) | `Tests/` | Cross-module test project |

Build: `dotnet build "SQL Buldozer.sln"` · Test: `dotnet test "SQL Buldozer.sln"`

## Tripwires

### .NET / C#

- [accepted] No `Thread.Sleep`, `.Result`, `.Wait()`, `async void` outside event handlers.
- [accepted] No hardcoded connection strings, server names, file paths, or secrets. Pull from `appsettings.json` + `Microsoft.Extensions.Configuration` + environment variables.
- [accepted] No `Console.WriteLine` outside CLI startup or operator-facing CLI output. Diagnostics go through `ILogger<T>` (Serilog).
- [accepted] No string interpolation into log messages — use named placeholders (`logger.LogInformation("Extracting {Table}", t)`).
- [accepted] No `IConfiguration["..."]` lookups in business code. Configuration is bound to POCOs in `Program.cs` only.
- [accepted] **Module layering is one-way.** Allowed reference graph: `CLI` → all four; `DSL.Connector` → `DSL` + `Logic`; `DSL` → `Logic`; `Logic` → `Common`; `Common` → nothing project-internal. Never add a back-edge.
- [accepted] Raw ADO.NET / `System.Data.SqlClient` types stay in `ParameterizationExtractor.Logic`. Other modules never open a `SqlConnection` or build SQL strings.
- [accepted] SQL text generation lives in the T4 template (`Templates/DefaultTemplate.tt`) + Logic SQL builder. No new ad-hoc string-concat paths for emitting `INSERT`/`UPDATE`.
- [accepted] `ParameterizationExtractor.Common` stays dependency-light. Only `Microsoft.Extensions.Logging.Abstractions`. New third-party deps need an ADR.
- [accepted] No new `TODO`/`FIXME` comments. (Existing legacy `//todo` lines in `Program.cs` are tech debt; do not add more.)

### F#

- [accepted] F# code lives only in `ParameterizationExtractor.DSL`. The C#↔F# bridge is `ParameterizationExtractor.DSL.Connector`.
- [accepted] The DSL is parsed with FParsec. No regex / `String.Split` substitutes for grammar parsing.
- [accepted] `AST.fs` is the canonical DSL representation. Do not duplicate AST types in C#; `DSL.Connector` consumes the F# AST directly.
- [accepted] F# compilation order is explicit and significant. Insert new `.fs` files into `<Compile Include>` in the right place; alphabetical doesn't work. Current order: `AST.fs` → `Mapper.fs` → `ParserResult.fs` → `InternalCommandParser.fs` → `Parser.fs`.
- [accepted] Parse failures cross the boundary as `ParserResult` values, not exceptions.
- [accepted] No `null` returns from F# functions consumed by C#. Use `Option<T>` (or `ParserResult`); the C# side unwraps explicitly.

## Recipe decisions

| Stub | Action | Project-specific notes |
|---|---|---|
| `docs/methodology/dotnet-service.md` | **REWRITE + RENAME** to `docs/methodology/dotnet-cli.md` | Drop service-shaped sections (controllers, MediatR, EF, `IHttpClientFactory`, health checks, OpenAPI, `<<HEALTH_PATH>>`). Keep / add: async hygiene; configuration via `Microsoft.Extensions.Configuration` + Serilog; argument parsing via `FluentCommandLineParser`; **DI is `System.Composition` (MEF), not `IServiceCollection`** — anything wired up uses `[Export]` / `[Import]` / `[ImportMany]` (composition root: `AppBootstrap.CreateAppBuilder(args)` in `Program.cs`); SQL generation via T4 template (`Templates/DefaultTemplate.tt`); module layering rule; NUnit test conventions; single `Tests/` project — no per-module test sub-projects. |
| `docs/methodology/fsharp-dsl.md` | **CREATE** | New short recipe covering: FParsec parser hygiene, file/AST organisation in `ParameterizationExtractor.DSL`, the explicit compile-order rule, `ParserResult`-based interop with `DSL.Connector`. |
| `docs/methodology/python-service.md` | **DELETE** | No Python in repo. |
| `docs/methodology/node-service.md` | **DELETE** | No Node in repo. |
| `docs/methodology/react-ui.md` | **DELETE** | No React in repo. |
| `docs/methodology/feature-development.md` | **KEEP-AS-IS** | Stack-agnostic. |
| `docs/methodology/task-workflow.md` | **KEEP-AS-IS** | Process doc, not stack-shaped. |

> Apply phase must also update the **Where to Read What** table in `CLAUDE.md` and `.github/copilot-instructions.md` to reflect renames/deletions and to point the F# stack tripwire link at the new `fsharp-dsl.md`.

## System architecture

> Reproduced here as the agreed sketch for `docs/architecture/overview.md`. Apply phase writes this content to that file (verbatim or near-verbatim).

### 1. What this is

SQL Buldozer is a developer-side CLI tool. A single operator runs it locally (or on a dev/CI machine), pointed at a SQL Server database and an `ExtractConfig.xml` map. It reads rows, walks foreign-key relations to collect dependent rows, and emits parameterised `INSERT`/`UPDATE` SQL scripts on disk. There is no service surface, no inbound network, no users beyond the operator.

### 2. Context

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

### 3. Components (modules)

| Component | Stack | Purpose | Path |
|---|---|---|---|
| `ParameterizationExtractor` (CLI) | .NET 6 / C# (Exe) | CLI entry point, host, DI/logging composition, app lifecycle | `ParameterizationExtractor/` |
| `ParameterizationExtractor.Common` | C# (netstandard2.0) | Shared abstractions; dependency-light | `ParameterizationExtractor.Common/` |
| `ParameterizationExtractor.Logic` | C# (netstandard2.0) | Core engine — graph traversal, extraction strategies, SQL generation via T4 | `ParameterizationExtractor.Logic/` |
| `ParameterizationExtractor.DSL` | F# (netstandard2.0, FParsec) | Parser for the extraction DSL; AST | `ParameterizationExtractor.DSL/` |
| `ParameterizationExtractor.DSL.Connector` | C# (netstandard2.0) | Translates F# DSL AST → Logic engine config | `ParameterizationExtractor.DSL.Connector/` |
| `Tests` | C# (.NET 6, NUnit) | Cross-module test project | `Tests/` |

### 4. Component diagram (one-way reference graph)

References (per `<ProjectReference>` in each csproj/fsproj):

- `CLI` → `Common`, `Logic`, `DSL`, `DSL.Connector`
- `DSL.Connector` → `DSL`, `Logic`
- `DSL` → `Logic`
- `Logic` → `Common`
- `Common` → (nothing)

Back-edges are forbidden (see the .NET layering tripwire).

### 5. Data flow

**Ingress.** The operator launches the CLI with arguments parsed by `FluentCommandLineParser`. Configuration is layered: `appsettings.json` (logging defaults + named connection strings) + environment variables. The actual extraction map is `ExtractConfig.xml` — *this file is what the CLI executes*: the list of tables, per-table `ExtractStrategy` (FKDependency / OnlyChildren / OnlyParent / OnlyOneTable), `Where` filters, and SQL build directives. `ClearingPackage.xml` shipped in the repo is a worked example of an `ExtractConfig`-shaped file. Optionally an extraction DSL file is also accepted and parsed by the F# parser.

**Internal flow.**

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

**Egress.** Generated `.sql` files written to a configured output directory; structured logs to console + file (Serilog Console + File sinks).

### 6. Data stores / external services

| Store / service | Tech | Purpose | Owning module |
|---|---|---|---|
| Source database | Microsoft SQL Server | Read-only graph being extracted from | `Logic` (only module that opens connections) |
| Output filesystem | Local filesystem | Generated `.sql` scripts; Serilog file sink | `CLI` writes; `Logic` produces strings |

No queues, no HTTP, no message bus, no cache.

### 7. Cross-cutting concerns

- **Authentication / Authorization:** N/A — local-machine tool. SQL Server connection supports both Windows auth (`Integrated Security=True`, the shipped default) and SQL auth (override the connection string via `appsettings.json` or env vars).
- **Logging / Observability:** Serilog (Console + File sinks) configured via the `Serilog` section of `appsettings.json`. No metrics, no tracing.
- **Configuration:** `Microsoft.Extensions.Configuration` (JSON + env), bound in `Program.cs`. The shipped `appsettings.json` is real, committed config — Serilog defaults plus named connection strings under `ConnectionStrings.*` (defaults point at non-prod dev SQL Server instances). CLI args override.
- **DI container:** `System.Composition` (MEF). Composition root is `AppBootstrap.CreateAppBuilder(args)` in `Program.cs`, with module-level extension methods (`.AddMSSQL()`, `.AddExecutor()`) registering MEF parts. Anything advertised to the rest of the app uses `[Export]`; consumers receive parts via `[Import]` / `[ImportMany]`. `Microsoft.Extensions.DependencyInjection` is present transitively only because the Microsoft.Extensions.Logging stack drags it in — it is **not** the primary container.
- **Secret management:** Connection strings live in `appsettings.json` or env vars; no vault / secret store. The committed defaults are non-prod dev DBs by design.
- **Error handling:** Explicit process exit codes via `ExitCode` enum in `Program.cs`. Fatal errors logged before exit.

### 8. Network / trust boundaries

The only boundary that exists: CLI process ↔ SQL Server. Connection is configured by the operator. The tool is run inside whatever trust zone the operator already has DB access from — no privilege escalation or impersonation.

### 9. Per-stack recipes

- .NET / C# CLI module recipe — `docs/methodology/dotnet-cli.md` (created in Apply phase, replacing `dotnet-service.md`).
- F# DSL recipe — `docs/methodology/fsharp-dsl.md` (created in Apply phase).

## PR review

### Exceptions (path globs to skip)

- `**/bin/**`
- `**/obj/**`
- `**/Templates/DefaultTemplate.cs` *(T4-generated; sibling `.tt` is the source)*
- `ParameterizationExtractor/ClearingPackage.xml`, `ParameterizationExtractor/ExtractConfig.xml` *(operator-facing sample/working configs, not source code)*

### Notes for the reviewer

- This is a developer CLI tool, not a network service — do not flag missing auth, missing input validation, or missing rate limits on code paths.
- Connection strings committed to `appsettings.json` are intentional non-prod dev defaults — do not flag as a secrets leak.
- F# files in `ParameterizationExtractor.DSL` compile in a fixed order — do not suggest reordering `<Compile Include>` entries in the fsproj.
- `System.Composition` (MEF) is the DI container by design — do not suggest migrating to `Microsoft.Extensions.DependencyInjection`.

## Pending ADRs (titles only)

- DI container is `System.Composition` (MEF), not `Microsoft.Extensions.DependencyInjection`.
- Module layering is one-way: `CLI` → all; `DSL.Connector` → `DSL`+`Logic`; `DSL` → `Logic`; `Logic` → `Common`; `Common` → nothing. Back-edges forbidden.
- Extraction DSL is implemented in F# / FParsec.
- SQL `INSERT`/`UPDATE` text is generated via the T4 template `Templates/DefaultTemplate.tt`; no hand-written code paths emit these statements.

> INTERVIEW COMPLETE. decisions.md written. Awaiting human review before proceeding to Phase 3 — Apply.
