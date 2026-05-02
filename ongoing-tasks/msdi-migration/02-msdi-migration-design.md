# 02 — MS.DI Migration — Design IServiceCollection extension shape

## Goal

Propose the `IServiceCollection` extension method shape that replaces `AddMSSQL()` / `AddExecutor()`. Output is a one-page design (in checklist `## Notes`) that human approves before step 03 starts. No code in this step.

## Track

dotnet (design)

## What Exists

- Inventory from step 01.
- Today's two MEF registration extension methods on `AppBootstrap`.
- Architectural constraint: module layering (ADR-002) is preserved — registration extensions live in the modules that own the parts.

## What to Build

- A proposal naming:
  - One extension method per current MEF "module" (suggested split: `AddBuldozerSql()`, `AddBuldozerExecutor()` — final names approved by human).
  - The lifetime each registration uses (transient default, `[Shared]` → singleton).
  - The interface vs concrete registration shape (`AddSingleton<IFoo, Foo>()` vs `AddSingleton<Foo>()`).
  - Where the extension methods live: `Logic` for engine parts, `CLI` for top-level executor wiring. Common / DSL.Connector if they own MEF parts (per inventory).
  - Whether `IApp` / `BuildApp` survive or collapse into the host pattern. If they collapse, name what replaces them.
- One ASCII sketch of the new `Program.cs` composition (not the implementation — just the sequence of extension calls).
- *Requires explicit human approval before step 03.* This is the architectural shape; getting it wrong here cascades.

## Acceptance Criteria

- [ ] Proposal is in `## Notes`, dated.
- [ ] Every contract from the step-01 inventory has a named registration in the proposal.
- [ ] Lifetimes are explicit (no "default" handwaves).
- [ ] Module layering (ADR-002) preserved — proposal does not introduce back-edges.
- [ ] Human approval recorded in `## Notes` before any code change.

## References

- Related ADRs: `001-mef-di-container.md` (retiring), `002-module-layering.md` (preserving), upcoming `006-msdi-container.md` (this proposal becomes its Decision).
- Related methodology: `docs/methodology/dotnet-cli.md` § Composition.
- Depends on: `01-msdi-migration-inventory.md`.
