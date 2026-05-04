# SQL Buldozer — Coding Rules

> **MANDATORY: No architectural decisions without explicit human approval.** Present the idea, explain the tradeoffs, and WAIT for approval before writing any code. This includes new files, new interfaces, new data stores, new services, DI changes, and any structural changes. Never proceed autonomously on design decisions.

> **Auto-loaded every session.** This file is a routing table + tripwires. It tells the AI where to find details and what to never do. Keep it lean.

---

## Where to Read What

| Topic | File | When to read |
|-------|------|--------------|
| All operator prompts | `prompts/readme.md` | Looking for a prompt to use — index of every prompt the team uses |
| Adding a new stack post-bootstrap | `prompts/add-stack.md` | Introducing a language/framework not yet covered by tripwires or recipes |
| Session bootstrap | `prompts/session-start.md` | Start of every session |
| Execution protocol (TDD, task classification, reporting) | `prompts/execution.md` | Before executing any step |
| Task workflow (checklists, step files, lifecycle) | `docs/methodology/task-workflow.md` | Starting or refactoring a plan |
| Feature recipe (the "how") | `docs/methodology/feature-development.md` | Before implementing any feature |
| .NET CLI module recipe | `docs/methodology/dotnet-cli.md` | Working on any C# code in this repo |
| F# DSL recipe | `docs/methodology/fsharp-dsl.md` | Working on the F# parser in `ParameterizationExtractor.DSL` |
| WPF Desktop module recipe | `docs/methodology/wpf-desktop.md` | Working on any C# code in `ParameterizationExtractor.Desktop` |
| PR review (auto-review instructions) | `.ignix/review-instructions.md` | Customizing what the automated reviewer flags in this repo |
| Architectural constraints | `adr/readme.md` | Before choosing approach A over B |
| Feature architecture overview | `ongoing-tasks/{feature}/feature-architecture.md` | Before working on any step of a feature |
| Plan templates | `ongoing-tasks/_template/` | Creating a new initiative |
| Post-implementation audit | `prompts/audit-checklist.md` | When verifying a completed checklist |

---

## Repository Shape

SQL Buldozer is a .NET 6 CLI tool that walks a relational database as a graph — tables are nodes, foreign keys are edges — starting from a set of seed records and a per-table extraction strategy. It collects the dependent rows in memory as a hierarchical structure, then emits parameterised `INSERT`/`UPDATE` SQL scripts (parents before children) suitable for replaying the extracted slice into another environment. A small F# DSL (FParsec) describes the extraction config; a configurable C# core does the walking and SQL generation. The intent is to automate the manual chore of preparing parameterisation scripts during development.

### Modules of the SQL Buldozer app

> One app composed of modules — not separate services. Two operator-facing entry points (CLI and Desktop) drive the same engine. One project per module under the repo root, named `ParameterizationExtractor[.Submodule]`, all built through the single `SQL Buldozer.sln`. C# `RootNamespace` is `Quipu.ParameterizationExtractor[.Submodule]`. Non-entry-point projects target `netstandard2.0`; the CLI and Tests target `net10.0` (Tests on `net10.0-windows` because of the Desktop ProjectReference); the Desktop targets `net10.0-windows` (`<UseWPF>true</UseWPF>`).

| Path | Stack | Purpose |
|------|-------|---------|
| `ParameterizationExtractor/` | .NET / C# (CLI exe) | CLI entry point, host, DI/logging composition, app lifecycle |
| `ParameterizationExtractor.Desktop/` | WPF on `net10.0-windows` / C# (WinExe) | Operator-facing UI shell — workspace authoring (in development) |
| `ParameterizationExtractor.Common/` | C# (netstandard2.0 lib) | Shared abstractions; dependency-light |
| `ParameterizationExtractor.Logic/` | C# (netstandard2.0 lib) | Core engine — graph traversal, extraction strategies, SQL generation via T4 |
| `ParameterizationExtractor.DSL/` | F# (netstandard2.0 lib, FParsec) | Parser for the extraction DSL; AST |
| `ParameterizationExtractor.DSL.Connector/` | C# (netstandard2.0 lib) | Translates F# DSL AST → Logic engine config |
| `Tests/` | C# (`net10.0-windows`, NUnit) | Cross-module test project |

Build: `dotnet build "SQL Buldozer.sln"` · Test: `dotnet test "SQL Buldozer.sln"`

---

## Global Rules

Two tiers. **Universal** rules ship as non-negotiable defaults — flip them off only with an ADR. **Recommended Defaults** are opinionated starting points; turn off the ones your team disagrees with by deleting the bullet (and recording the reason in an ADR if the disagreement is non-obvious).

### Universal — keep on (override only via ADR)

- **Git is the system of record.** AI never merges. Changes flow through PRs / human review.
- **No secrets in code or commits.** Use env vars, secret stores, or vaults. `.env` files are local-only and gitignored.
- **No hardcoded URLs, connection strings, image tags, or environment-specific values.** Pull from configuration.
- **Tests required for new behavior.** If you skip tests, state the reason in the commit message and in the checklist's `## Notes`.
- **Validated input at every service boundary.** No trusting upstream payloads.
- **Parameterised queries only.** No string concatenation into SQL, NoSQL filters, or shell commands.
- **No architectural decisions without approval.** See the banner above.

### Recommended Defaults — opt out by deleting (record reason if non-obvious)

- **No `TODO` / `FIXME` / dead code left behind.** Either finish it inline or escalate. *(Soften to "no new TODOs without a tracking issue link" if your team intentionally uses TODO markers as work queue.)*
- **Structured logging only.** No `print` / `console.log` / `Console.WriteLine` in production paths.
- **Edit existing files before creating new ones.** Don't scatter new helpers — extend the closest existing module.
- **Do not create *task summary* markdown files.** The checklist + commit history + ADRs are the record. *(Release notes, RFC drafts, and similar deliverables are explicitly allowed.)*
- **No fallback values, default data, or graceful degradation for missing config / dependencies.** Fail explicitly with a clear error. *(High-friction in some brownfield contexts — drop if your codebase relies on graceful degradation by design.)*
- **No `try/catch` to silently mask missing modules, imports, or build-time dependencies.** Build failures must be explicit.

---

## Stack-Specific Tripwires

### .NET / C#

> Full recipe: `docs/methodology/dotnet-cli.md`

#### Tripwires — never do this
- No `Thread.Sleep`, `.Result`, `.Wait()`, `async void` outside event handlers.
- No hardcoded connection strings, server names, file paths, or secrets. Pull from `appsettings.json` + `Microsoft.Extensions.Configuration` + environment variables.
- No `Console.WriteLine` outside CLI startup or operator-facing CLI output. Diagnostics go through `ILogger<T>` (Serilog).
- No string interpolation into log messages — use named placeholders (`logger.LogInformation("Extracting {Table}", t)`).
- No `IConfiguration["..."]` lookups in business code. Configuration is bound to POCOs in `Program.cs` only.
- **Module layering is one-way.** Allowed reference graph: `CLI` → `Common`, `Logic`, `DSL`, `DSL.Connector`; `Desktop` → `Common`, `Logic`; `DSL.Connector` → `DSL`, `Logic`; `DSL` → `Logic`; `Logic` → `Common`; `Common` → nothing project-internal. Never add a back-edge. The Desktop never references `DSL` or `DSL.Connector` (ADR-005, ADR-007).
- Raw ADO.NET / `System.Data.SqlClient` types stay in `ParameterizationExtractor.Logic`. Other modules never open a `SqlConnection` or build SQL strings.
- SQL text generation lives in the T4 template (`Templates/DefaultTemplate.tt`) + Logic SQL builder. No new ad-hoc string-concat paths for emitting `INSERT`/`UPDATE`.
- `ParameterizationExtractor.Common` stays dependency-light. Only `Microsoft.Extensions.Logging.Abstractions`. New third-party deps need an ADR.
- No new `TODO`/`FIXME` comments. (Existing legacy `//todo` lines in `Program.cs` are tech debt; do not add more.)

### F#

> Full recipe: `docs/methodology/fsharp-dsl.md`

#### Tripwires — never do this
- F# code lives only in `ParameterizationExtractor.DSL`. The C#↔F# bridge is `ParameterizationExtractor.DSL.Connector`.
- The DSL is parsed with FParsec. No regex / `String.Split` substitutes for grammar parsing.
- `AST.fs` is the canonical DSL representation. Do not duplicate AST types in C#; `DSL.Connector` consumes the F# AST directly.
- F# compilation order is explicit and significant. Insert new `.fs` files into `<Compile Include>` in the right place; alphabetical doesn't work. Current order: `AST.fs` → `Mapper.fs` → `ParserResult.fs` → `InternalCommandParser.fs` → `Parser.fs`.
- Parse failures cross the boundary as `ParserResult` values, not exceptions.
- No `null` returns from F# functions consumed by C#. Use `Option<T>` (or `ParserResult`); the C# side unwraps explicitly.

### WPF (Desktop)

> Full recipe: `docs/methodology/wpf-desktop.md`

#### Tripwires — never do this
- WPF / `PresentationCore` / `PresentationFramework` code lives only in `ParameterizationExtractor.Desktop`. `Common`, `Logic`, `DSL`, `DSL.Connector` never reference WPF.
- The desktop project never references `ParameterizationExtractor.DSL` or `ParameterizationExtractor.DSL.Connector`. The F# DSL is frozen (ADR-005); the desktop's authoring surface is the workspace JSON, not the DSL.
- No logic in `.xaml.cs` code-behind beyond `InitializeComponent()` and necessary framework overrides (e.g. `OnRenderSizeChanged` on a custom-drawn control). No `Click=` event handlers, no `if/else`, no `MessageBox.Show`.
- ViewModels never reference WPF types — no `Window`, `Visibility`, `Brush`, `Dispatcher`, `MessageBox`. Use enums + value converters for UI state; depend on `IUiDispatcher` for marshalling.
- No hand-rolled `INotifyPropertyChanged`. Use `CommunityToolkit.Mvvm` source generators (`[ObservableObject]`, `[ObservableProperty]`, `[RelayCommand]`).
- No `MessageBox.Show` in VMs or services. Dialogs go through `IDialogService`.
- No `IConfiguration["..."]` lookups in VMs or services. Bind to POCOs in `App.xaml.cs` only (mirrors the .NET tripwire).
- No `Console.WriteLine`. Diagnostics go through `ILogger<T>` (Serilog) (mirrors the .NET tripwire).
- Theme resource dictionaries are loaded **once** in `App.xaml`. Per-window theme loading is forbidden.
- AvalonEdit is wrapped in the `SqlEditor` UserControl. ViewModels never see `TextDocument`, `TextEditor`, or any `ICSharpCode.AvalonEdit.*` type.
- No service-locator pattern. `IServiceProvider` is consulted exactly once in `App.xaml.cs` `OnStartup` to resolve `MainWindow`. Everything else flows through constructor injection.
- No `WorkspaceSource.PasswordEncrypted` reads from VMs or views — go through `IPasswordProtector` (ADR-010).
- No new `TODO`/`FIXME` comments (mirrors the .NET tripwire).
- `Thread.Sleep`, `.Result`, `.Wait()`, `async void` outside event handlers — still forbidden (mirrors the .NET tripwire).

---

## Tasks & Plans Workflow

- **One feature = one checklist + one steps folder + one architecture overview.** No separate `plan.md`.
- Active work: `ongoing-tasks/{feature-name}-checklist.md` (source of truth) + `ongoing-tasks/{feature-name}/feature-architecture.md` (big-picture context) + `ongoing-tasks/{feature-name}/NN-{feature-name}-<short-kebab>.md` (step files).
- Checklist sections: Goal · Scope · Architecture (link) · Steps (GitHub task list linking to step files) · Notes.
- Architecture file: feature-level pipeline / component diagram / data flow / data stores / extension points / security.
- Step file sections: Goal · Track · What Exists · What to Build · Acceptance Criteria · References.
- Numbering: zero-padded `NN`. Phased (`1.1`, `2.3`) only when the checklist explicitly groups into tracks.
- Completed or superseded → move pair into `ongoing-tasks/archive/`.
- **Architectural decisions** go in `adr/NNN-title.md` with sections: Status · Context · Decision · Consequences.
- Full conventions: `docs/methodology/task-workflow.md`. Template: `ongoing-tasks/_template/`.

When the AI starts work on a step:
1. Read `prompts/session-start.md` (session bootstrap).
2. Read `ongoing-tasks/{feature}/feature-architecture.md` for the feature's big-picture context.
3. Identify the target stack and re-read the matching tripwires section above.
4. If the task requires a recipe, read the matching `docs/methodology/*.md`.
5. Check `adr/` for constraints that forbid certain approaches.
6. Follow `prompts/execution.md` for TDD cycle, task classification, and reporting.
7. Implement → add/update tests → tick the checklist item → leave step files' scope intact (step files are specifications, not logs; log in the checklist's `## Notes`).

---

## AI Tool Usage

- Track progress with the agent's todo / task tracker — one entry per checklist item being worked.
- Fire **independent** file reads / searches in parallel; sequence only when one result feeds the next.
- Read the relevant `docs/methodology/*.md` recipe **before** proposing patterns — don't invent conventions.
- Prefer dedicated file tools over shelling out for file operations.
- Never amend a published commit; create a new one. Never force-push without explicit user instruction.

---

## Security (OWASP-aware)

- Validate all inputs at the service boundary.
- Parameterize all queries. No string concatenation into SQL, NoSQL filters, or shell commands.
- AuthN/AuthZ on every non-health endpoint. Health endpoints are anonymous and minimal.
- Treat LLM output as untrusted input — sanitize before executing, rendering as HTML, or writing to disk.
- Sandbox / resource-limit any user-driven code execution paths. Do not loosen these defaults without an ADR.

---

## When in Doubt

- Ask before making cross-cutting refactors.
- Prefer extending an existing pattern over inventing a new one.
- If a rule above contradicts the code you're editing, the **code is the legacy** — flag it as tech debt and match the closest recent pattern.
