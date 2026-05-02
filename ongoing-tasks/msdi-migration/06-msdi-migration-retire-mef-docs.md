# 06 — MS.DI Migration — Retire MEF docs and tripwires

## Goal

Update every doc surface that references MEF as the DI container. Retire ADR-001, add ADR-006 capturing the new decision, remove MEF-specific tripwires from `CLAUDE.md` (and mirror), and update the `dotnet-cli.md` recipe.

## Track

cross-cutting (docs)

## What Exists

- `adr/001-mef-di-container.md` — Status: Proposed (or Accepted by the time this step runs).
- `CLAUDE.md` and `.github/copilot-instructions.md` — `.NET / C#` tripwire about MEF being the DI container.
- `docs/methodology/dotnet-cli.md` — § Composition mentions MEF, `[Export]` / `[Import]`.
- `docs/architecture/overview.md` — § 5 cross-cutting concerns describes MEF.
- `.ignix/review-instructions.md` — project-specific note: *"do not suggest migrating to Microsoft.Extensions.DependencyInjection"*.

## What to Build

- Update `adr/001-mef-di-container.md` status to `Superseded by 006`. Leave the body intact (history matters).
- Create `adr/006-msdi-container.md` from `_template.md`. Title: *"DI container is `Microsoft.Extensions.DependencyInjection`"*. Status: `Accepted`. Context: brief — MEF was chosen in 2019; the desktop UI work and modernisation made standard MS.DI a better fit; characterisation tests held the migration. Decision: the present-tense statement of the new shape. Consequences: easier (less to teach contributors), harder (loss of "drop-a-DLL" plug-in story — confirmed unused).
- Update `CLAUDE.md` and `.github/copilot-instructions.md` (mirror byte-for-byte): remove the MEF tripwire from the `.NET / C#` section. Replace with — or just delete — as appropriate to current state.
- Update `docs/methodology/dotnet-cli.md` § Composition to describe MS.DI; remove the MEF paragraph and the `[Export]` / `[Import]` notes.
- Update `docs/architecture/overview.md` § 5 (Cross-cutting concerns → DI container) to describe MS.DI; remove the MEF text.
- Update `.ignix/review-instructions.md` § Project-specific notes: remove the MEF defence line.
- Update `adr/readme.md` index with ADR-006.

## Acceptance Criteria

- [ ] ADR-001 marked `Superseded by 006`; ADR-006 exists with full content (Context / Decision / Consequences).
- [ ] No reference to `System.Composition` / MEF / `[Export]` / `[Import]` remains in `CLAUDE.md`, `.github/copilot-instructions.md`, `docs/methodology/dotnet-cli.md`, `docs/architecture/overview.md`, or `.ignix/review-instructions.md`.
- [ ] `CLAUDE.md` and `.github/copilot-instructions.md` are byte-identical (`scripts/verify-bootstrap.ps1` passes).
- [ ] `adr/readme.md` index lists ADR-006.

## References

- Related ADRs: `001-mef-di-container.md`, `006-msdi-container.md`.
- Related methodology: `docs/methodology/dotnet-cli.md`.
- Depends on: `05-msdi-migration-composition-root.md`.
