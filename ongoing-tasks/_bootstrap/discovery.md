# Bootstrap Discovery — Approved

> Phase 1 output. Mode: **brownfield**. Approved by human on 2026-05-02.
> Feeds Phase 2 (Interview). Delete after Phase 3 (Apply) lands.

## Detected stacks

| Stack | Detected from | Build cmd | Test cmd | Test framework |
|---|---|---|---|---|
| .NET 6 / C# (CLI + module libraries) | `ParameterizationExtractor.csproj:5` (`OutputType=Exe`, `net6.0`); `ParameterizationExtractor.Common.csproj:4`, `ParameterizationExtractor.Logic.csproj:4`, `ParameterizationExtractor.DSL.Connector.csproj:4` (`netstandard2.0`); `SQL Buldozer.sln` | `dotnet build "SQL Buldozer.sln"` | `dotnet test "SQL Buldozer.sln"` | NUnit (`Tests.csproj:10` — nunit 3.12.0, NUnit3TestAdapter, Microsoft.NET.Test.Sdk) |
| F# / netstandard2.0 (DSL parser library) | `ParameterizationExtractor.DSL.fsproj:4` (`netstandard2.0`); FParsec dep at `fsproj:16` | (built as part of `SQL Buldozer.sln`) | (covered by the same NUnit `Tests` project — `Tests.csproj:18` references the fsproj) | NUnit (shared) |

> **Approved:** F# is a separate stack with its own tripwire section in `CLAUDE.md`. Build/test toolchain is shared with .NET via the single solution file.

## Approved factual values

### Project identity

| Placeholder | Approved value |
|---|---|
| `<<PROJECT_NAME>>` | **SQL Buldozer** |

### Repository Shape table (`CLAUDE.md`, `.github/copilot-instructions.md`)

> **Approved framing:** This repo is **one CLI application** composed of modules — not multiple services. Replace the table's "deployable artifact" framing accordingly. Suggested heading: *"Modules of the `SQL Buldozer` CLI"*.

| Path | Stack | Module purpose (DEFERRED to Interview — no per-dir readme) |
|---|---|---|
| `ParameterizationExtractor/` | .NET 6 / C# (CLI entry point — `Program.cs`) | UNKNOWN |
| `ParameterizationExtractor.Common/` | .NET / C# (netstandard2.0 library) | UNKNOWN |
| `ParameterizationExtractor.Logic/` | .NET / C# (netstandard2.0 library) | UNKNOWN |
| `ParameterizationExtractor.DSL/` | F# (netstandard2.0 library, FParsec) | UNKNOWN |
| `ParameterizationExtractor.DSL.Connector/` | .NET / C# (netstandard2.0 library) | UNKNOWN |
| `Tests/` | .NET 6 / C# (NUnit) | Test project |

### `<<TOP_LEVEL_REPO_MAP>>` (used in `CLAUDE.md`, `.github/copilot-instructions.md`, `prompts/session-start.md:34`)

```
.
├── ParameterizationExtractor/             (.NET 6 CLI — entry point)
├── ParameterizationExtractor.Common/      (netstandard2.0 — shared abstractions)
├── ParameterizationExtractor.Logic/       (netstandard2.0 — extraction / SQL build engine)
├── ParameterizationExtractor.DSL/         (F# netstandard2.0 — FParsec-based DSL parser)
├── ParameterizationExtractor.DSL.Connector/ (netstandard2.0 — DSL ↔ Logic glue)
├── Tests/                                 (.NET 6 NUnit test project)
├── SQL Buldozer.sln                       (Visual Studio solution — single solution for all stacks)
├── README.md, CLAUDE.md, ai-workflow.md, template-readme.md
└── adr/, docs/, ongoing-tasks/, prompts/, scripts/, .ignix/, .github/
```

### `prompts/execution.md` build/test table

| Stack | Test framework | Command |
|---|---|---|
| .NET (C#) | NUnit | `dotnet test "SQL Buldozer.sln"` |
| F# | NUnit (shared `Tests` project) | `dotnet test "SQL Buldozer.sln"` |

> Single-row variant is also acceptable since both stacks share the toolchain. Keeping two rows here so `STACK_2 = F#` is visible to readers.

## Deferred to Phase 2 — Interview

- **Tripwires** for the .NET (C#) stack and the F# stack — separate sections, per approval.
- `<<PROJECT_DESCRIPTION_ONE_PARAGRAPH>>` (CLAUDE.md / copilot-instructions.md) and `<<ONE_LINE_PROJECT_DESCRIPTION>>` (`prompts/session-start.md:9`).
- Per-module one-line purposes (table above) — no per-directory readme exists, so these need human input.
- Stack recipe decisions:
  - `docs/methodology/dotnet-service.md` — strong signal (5 csproj files); likely keep but **rename / reframe** as a *.NET CLI module recipe* (not "service") and consider whether the `<<HEALTH_PATH>>` and similar service-shaped sections apply at all.
  - `docs/methodology/fsharp-*.md` — does not exist; Interview should decide whether to create one (DSL/FParsec-specific guidance) or fold F# notes into the .NET recipe.
  - `docs/methodology/python-service.md`, `node-service.md`, `react-ui.md` — no signals in repo; likely delete.
- All `docs/architecture/overview.md` content beyond the directory listing (context diagram, component diagram, ingress/egress, data stores, AuthN/Z, logging, config, secrets, error handling, network/trust boundaries).
- `.ignix/review-instructions.md` content.
- Recipe-internal placeholders inside whatever recipes survive (`<<HEALTH_PATH>>`, `<<SERVICE_1>>`, ruff config, etc.).

## Confirmed open questions — closed

| Question | Answer |
|---|---|
| Project name? | **SQL Buldozer** |
| F# a separate stack? | **Yes** — separate tripwire section |
| Repository Shape framing? | **Modules of one CLI app**, not separate services |
| Build/test command phrasing? | Use `dotnet build "SQL Buldozer.sln"` / `dotnet test "SQL Buldozer.sln"` (does not matter — picked for explicitness) |
