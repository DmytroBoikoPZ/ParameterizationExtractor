# Architectural Decision Records (ADRs)

This folder holds ADRs for the project. Each decision is a separate file: `NNN-title.md`.

ADRs are **constraint definitions** — the AI reads them to learn what approaches are off-limits, not to understand history. They sit at priority 2 in the rule hierarchy (above methodology, below the auto-loaded tripwires in `CLAUDE.md`).

---

## When to write an ADR

Write a new ADR when a decision:

- Constrains future work in a non-obvious way ("we will never X because Y").
- Picks one of several reasonable alternatives, where the alternative is plausible enough that someone might re-litigate it later.
- Touches multiple components, layers, or stacks.
- Trades off something the team is likely to want back later (performance, simplicity, optionality).

You do **not** need an ADR for:

- Local implementation choices inside a single module.
- Conventions already captured in `CLAUDE.md` tripwires or methodology recipes.
- Decisions that can be reversed without ripple effects.

---

## Format

Copy [`_template.md`](./_template.md) → `adr/NNN-short-kebab-title.md` and fill in:

- **Status** — `Proposed` | `Accepted` | `Superseded by NNN` | `Deprecated`
- **Context** — what is the issue we are deciding on?
- **Decision** — what did we decide?
- **Consequences** — what becomes easier or harder because of this decision?

Keep ADRs short. One screen is plenty. Long ADRs become read-once artifacts that nobody opens again.

---

## Numbering

- `NNN` is zero-padded three-digit, monotonically increasing: `001`, `002`, …
- Never reuse a number, even if the ADR is later superseded — supersession is a status, not a deletion.
- The number is permanent; the title in the filename can change if the decision's framing improves over time.

---

## Index

| ADR | Title | Status |
|-----|-------|--------|
| [001](001-mef-di-container.md) | DI container is `System.Composition` (MEF), not `Microsoft.Extensions.DependencyInjection` | Superseded by 006 |
| [002](002-module-layering.md) | Module layering is one-way; back-edges forbidden | Accepted |
| [003](003-fsharp-fparsec-dsl.md) | Extraction DSL is implemented in F# / FParsec | Accepted |
| [004](004-t4-sql-generation.md) | SQL `INSERT`/`UPDATE` text is generated via the T4 template `Templates/DefaultTemplate.tt` | Accepted |
| [005](005-freeze-fsharp-dsl.md) | Freeze the F# DSL — no new feature work; retire when superseded | Accepted |
| [006](006-msdi-container.md) | DI container is `Microsoft.Extensions.DependencyInjection` (corrects ADR-001) | Accepted |
| [007](007-desktop-wpf-stack.md) | Desktop UI stack — WPF on .NET 10 with CommunityToolkit.Mvvm and Generic Host | Accepted |
| [008](008-desktop-ui-controls.md) | Desktop UI controls — MahApps.Metro chrome, AvalonEdit code editor, no docking lib initially | Accepted |
| [009](009-workspace-format.md) | Workspace format — JSON via `System.Text.Json`, `$kind` polymorphic discriminator, `.bws` wrapper | Accepted |
| [010](010-desktop-password-at-rest.md) | Desktop password-at-rest — DPAPI under `DataProtectionScope.CurrentUser` with opt-out checkbox | Accepted |
| [011](011-engine-schema-awareness.md) | Engine schema-awareness — `Schema` on table-naming model; bare-name lookup throws on ambiguity | Accepted |
| [012](012-graph-visualisation-library.md) | Graph visualisation library — `AutomaticGraphLayout` 1.1.12 (community Msagl fork); edge-pick events satisfied | Accepted |
