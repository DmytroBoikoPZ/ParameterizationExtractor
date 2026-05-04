# SQL Buldozer — Architecture Overview

> System-level topology. Read at session start. Updated when the topology changes.
> Feature-level architecture lives in `ongoing-tasks/{feature}/feature-architecture.md`, not here.

---

## 1. System Context

SQL Buldozer is a developer-side tool. A single operator runs it locally (or on a dev/CI machine), pointed at a SQL Server database and an extraction map (`ExtractConfig.xml` for the CLI today; a `.bws` JSON workspace for the Desktop UI in development). The tool walks foreign-key relations from a seed query, collects dependent rows, and emits parameterised `INSERT`/`UPDATE` SQL scripts on disk. There is no service surface, no inbound network, no users beyond the operator.

The system has **two operator-facing entry points** that drive the same engine (`Logic`):

```
                              args + ExtractConfig.xml + appsettings.json
                    ┌─────────────────────────────────►  ┌──────────────────┐
                    │                                    │  SQL Buldozer    │
                    │   ◄──── stdout/stderr (Serilog) ── │  CLI             │
┌──────────────────┐│        *.sql output files          └─────┬────────────┘
│  Developer /     ││                                          │
│  CI runner       │┤                                          │
│  (operator)      ││                                          │ T-SQL queries
└──────────────────┘│        .bws workspace + UI clicks        │ (Microsoft.Data.SqlClient)
                    │                                          │
                    │                                    ┌─────┴────────────┐
                    └─────────────────────────────────►  │  SQL Buldozer    │
                                                         │  Desktop (WPF)   │
                                                         └─────┬────────────┘
                                                               │
                                                               ▼
                                                       ┌──────────────────┐
                                                       │  Source SQL      │
                                                       │  Server DB       │
                                                       │  (read-only)     │
                                                       └──────────────────┘
```

The Desktop is in early development — the `desktop-skeleton` feature established the project, the Generic Host composition, and the empty MainWindow. UI features are scaffolded incrementally on top.

---

## 2. Components

| Component | Stack | Purpose | Path |
|-----------|-------|---------|------|
| `ParameterizationExtractor` (CLI) | .NET / C# (Exe) | CLI entry point, host, DI/logging composition, app lifecycle | `ParameterizationExtractor/` |
| `ParameterizationExtractor.Desktop` | WPF on `net10.0-windows` / C# (WinExe) | Operator-facing UI shell — owns `IWorkspaceStore` for `.bws` JSON workspace files (ADR-009); renders the M3 tabbed shell (Overview / Seed / Graph / Extras / Run) when a workspace is loaded, the M1 Welcome view otherwise; File menu (New / Open / Recent / Close / Exit); per-user recent files at `%APPDATA%\SqlBuldozer\recent-files.json`; New-workspace + Edit-connection dialogs talk to SQL Server through `IConnectionTester` (Logic-side); Seed tab (M4) for multi-script editing + bounded 200-row preview, first feature to use `IUiDispatcher` for marshalling preview rows back to the UI thread + `IDatabaseExplorer` (Logic) for table listing + bounded SELECT preview; Graph tab (M5) for read-only FK-subgraph view with per-node state badges (`✓` configured / `◯` pending / `✗` excluded), driven by `IGraphBuilder` (Logic) and rendered via `Controls/GraphHost/` — an AGL (`AutomaticGraphLayout`, ADR-012) wrapper; SQL passwords stored DPAPI-encrypted (ADR-010); Overview / Seed / Graph tabs are implemented, Extras / Run remain placeholders until their owning features land | `ParameterizationExtractor.Desktop/` |
| `ParameterizationExtractor.Common` | C# (netstandard2.0) | Shared abstractions; dependency-light | `ParameterizationExtractor.Common/` |
| `ParameterizationExtractor.Logic` | C# (netstandard2.0) | Core engine — graph traversal, extraction strategies, SQL generation via T4 | `ParameterizationExtractor.Logic/` |
| `ParameterizationExtractor.DSL` | F# (netstandard2.0, FParsec) | Parser for the extraction DSL; AST | `ParameterizationExtractor.DSL/` |
| `ParameterizationExtractor.DSL.Connector` | C# (netstandard2.0) | Translates F# DSL AST → Logic engine config | `ParameterizationExtractor.DSL.Connector/` |
| `Tests` | C# (`net10.0-windows`, NUnit) | Cross-module test project | `Tests/` |

References (per `<ProjectReference>` in each csproj/fsproj):

- `CLI` → `Common`, `Logic`, `DSL`, `DSL.Connector`
- `Desktop` → `Common`, `Logic`
- `DSL.Connector` → `DSL`, `Logic`
- `DSL` → `Logic`
- `Logic` → `Common`
- `Common` → (nothing)

Back-edges are forbidden (see the .NET layering tripwire in `CLAUDE.md`). The Desktop intentionally does **not** reference `DSL` or `DSL.Connector` — the F# DSL is frozen (ADR-005); the Desktop's authoring surface is the workspace JSON, not the DSL.

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
        │   - open SqlConnection (Microsoft.Data.SqlClient)
        │   - walk graph from seed records, resolving tables by (Schema, Name) tuple (ADR-011)
        │   - build hierarchical in-memory structure of dependent records
        │   - render INSERT/UPDATE T-SQL via T4 template (Templates/DefaultTemplate.tt)
        │     emits [Schema].[Table] when operator config has Schema; bare [Table] otherwise
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
| Workspace files (`*.bws`) | Local filesystem (JSON) | Desktop-authored extraction definitions: connection metadata + embedded `Package` / `GlobalExtractConfiguration` (ADR-009). SQL passwords are DPAPI-encrypted at rest when "Store credentials" is checked (ADR-010). | `Desktop` (`IWorkspaceStore` reads + writes) |
| Recent files (`recent-files.json`) | Local filesystem (JSON) at `%APPDATA%\SqlBuldozer\` | Per-user list of recently-opened workspace paths (max 5, de-duplicated by canonical path) | `Desktop` (`IRecentFilesStore` reads + writes) |

No queues, no HTTP, no message bus, no cache.

---

## 5. Cross-cutting concerns

- **Authentication / Authorization:** N/A — local-machine tool. SQL Server connection supports both Windows auth (`Integrated Security=True`, the shipped default) and SQL auth (override the connection string via `appsettings.json` or env vars).
- **Logging / Observability:** Serilog (Console + File sinks) configured via the `Serilog` section of `appsettings.json`. No metrics, no tracing.
- **Configuration:** `Microsoft.Extensions.Configuration` (JSON + env), bound in `Program.cs`. The shipped `appsettings.json` is real, committed config — Serilog defaults plus named connection strings under `ConnectionStrings.*` (defaults point at non-prod dev SQL Server instances). CLI args override.
- **DI container:** `Microsoft.Extensions.DependencyInjection` across both entry points. The CLI uses a thin custom wrapper (`IAppBuilder` over `ServiceCollection`) — composition root `AppBootstrap.CreateAppBuilder(args)` in `ParameterizationExtractor/AppBootstrap.cs`. The Desktop uses `Microsoft.Extensions.Hosting`'s Generic Host (`Host.CreateApplicationBuilder()`) — composition root in `App.xaml.cs` `OnStartup`, factored through `DesktopHost.CreateApplicationBuilder()` for testability. Singletons on the Desktop side now include `IWorkspaceStore`, `IDialogService` (MahApps `MetroDialogManager` + `Microsoft.Win32` pickers), `IRecentFilesStore` (JSON-backed), `IConnectionTester` (engine-side, Logic project), `IDatabaseExplorer` (engine-side, Logic project — table list + bounded SELECT preview), `IGraphBuilder` (engine-side, Logic project — FK-reachable subgraph), `IUiDispatcher` (Desktop, wraps `Application.Current.Dispatcher`), and `IPasswordProtector` (DPAPI). Both register parts via `services.AddSingleton<...>()` / `AddTransient<...>()`; both use constructor injection only (no attribute-based registration). See ADR-006 (CLI) and ADR-007 (Desktop). ADR-001 originally recorded MEF as the container; that was incorrect — superseded by ADR-006.
- **Secret management:** CLI connection strings live in `appsettings.json` or env vars (no vault / secret store; committed defaults are non-prod dev DBs by design). Workspace-side SQL passwords are DPAPI-encrypted under `DataProtectionScope.CurrentUser` (ADR-010); plaintext is held in memory only for the lifetime of the loaded workspace.
- **Error handling:** Explicit process exit codes via `ExitCode` enum in `Program.cs`. Fatal errors logged before exit.

---

## 6. Trust boundaries

The only boundary that exists: CLI process ↔ SQL Server. Connection is configured by the operator. The tool is run inside whatever trust zone the operator already has DB access from — there is no privilege escalation or impersonation.

---

## 7. Active ADRs

See [`adr/readme.md`](../../adr/readme.md) for the full index. The architectural decisions that most directly shape this overview:

- `002-module-layering.md` — Module reference graph is one-way; back-edges forbidden.
- `003-fsharp-fparsec-dsl.md` — Extraction DSL is implemented in F# / FParsec.
- `004-t4-sql-generation.md` — SQL `INSERT`/`UPDATE` text is generated via the T4 template `Templates/DefaultTemplate.tt`.
- `005-freeze-fsharp-dsl.md` — F# DSL is frozen; no new feature work.
- `006-msdi-container.md` — DI container is `Microsoft.Extensions.DependencyInjection` (supersedes ADR-001's incorrect MEF claim).
- `007-desktop-wpf-stack.md` — Desktop UI stack: WPF on .NET 10 with CommunityToolkit.Mvvm and Generic Host.
- `008-desktop-ui-controls.md` — Desktop UI controls: MahApps.Metro chrome, AvalonEdit code editor, no docking lib initially.
- `009-workspace-format.md` — Workspace format: JSON via `System.Text.Json`, `$kind` polymorphic discriminator, `.bws` wrapper. Engine accepts both XML and JSON for `Package` / `GlobalExtractConfiguration`.
- `010-desktop-password-at-rest.md` — Workspace SQL passwords are DPAPI-encrypted under `DataProtectionScope.CurrentUser`, with an opt-out checkbox.
- `011-engine-schema-awareness.md` — Engine resolves tables by `(Schema, Name)` tuples; empty Schema in operator config triggers the bare-name lookup with the one-or-throw policy. T4 emits `[Schema].[Table]` when Schema non-empty; bare otherwise.
- `012-graph-visualisation-library.md` — Graph viz library is `AutomaticGraphLayout` 1.1.12 (community Msagl fork). Edge-pick events satisfied (precondition for v2 `desktop-graph-edge-click`). AGL types confined to `Controls/GraphHost/` (recipe-level tripwire mirrors AvalonEdit isolation).

---

## 8. Where to add detail

- **Feature-level architecture** — `ongoing-tasks/{feature}/feature-architecture.md`
- **Per-stack recipes** — `docs/methodology/dotnet-cli.md`, `docs/methodology/fsharp-dsl.md`, `docs/methodology/wpf-desktop.md`
- **Specific decisions** — `adr/NNN-*.md`
- **Hard rules / tripwires** — `CLAUDE.md`
