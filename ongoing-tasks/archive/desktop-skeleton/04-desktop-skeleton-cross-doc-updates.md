# 04 — desktop-skeleton — Cross-doc updates

## Goal

Keep the system-level docs honest with the new project. After this step, anyone reading `docs/architecture/overview.md` or `adr/readme.md` sees the desktop project, the two new ADRs, and the unchanged engine layering.

## Track

`cross-cutting` (docs only).

## What Exists

- `docs/architecture/overview.md` — system topology, last updated when ADR-006 retired ADR-001. § 2 (Components) lists 5 modules + Tests; § 7 (Active ADRs) lists 002 / 003 / 004 / 005 / 006.
- `adr/readme.md` — ADR index. Updated in step 01 to add rows for 007 / 008. This step only verifies and consolidates.
- `verify-bootstrap.ps1` (if present) — script that checks doc invariants. Run at the end.
- After step 03: a working desktop project exists in code. This step writes the doc reflection of that fact.

## What to Build

- `docs/architecture/overview.md` § 1 (System Context) — update the ASCII topology diagram to reflect that the operator can launch *either* the CLI or the Desktop UI. Both ultimately run the same `Logic` engine against the source DB.
- `docs/architecture/overview.md` § 2 (Components) — add `ParameterizationExtractor.Desktop` row: WPF, `net10.0-windows`, "Operator-facing UI shell — hosts the workspace authoring views", path `ParameterizationExtractor.Desktop/`.
- `docs/architecture/overview.md` § 2 — update the ProjectReference graph block to add `Desktop → Common, Logic`.
- `docs/architecture/overview.md` § 5 (Cross-cutting concerns → DI container) — add a sentence noting the Desktop uses the same Generic Host pattern (cite ADR-007, which itself cites ADR-006). No new pattern, no contradiction.
- `docs/architecture/overview.md` § 7 (Active ADRs) — add rows for ADR-007 and ADR-008 in the same shape as the existing rows.
- `docs/architecture/overview.md` § 8 (Where to add detail) — no change expected; verify the existing pointers still apply.
- `adr/readme.md` — verify rows for 007 and 008 added in step 01 are consistent with the actual ADR titles and Status. Re-run the index sanity check.
- Sanity check `CLAUDE.md` `## Repository Shape` table — add a row for `ParameterizationExtractor.Desktop/`. Mirror to `.github/copilot-instructions.md`.
- `ongoing-tasks/desktop-skeleton-checklist.md` — `## Notes` entry dated `YYYY-MM-DD` summarising cumulative changes (per the methodology — see how `archive/msdi-migration-checklist.md` did its `## Final summary`).

## Acceptance Criteria

- [ ] `docs/architecture/overview.md` § 2 has a row for `ParameterizationExtractor.Desktop` and the reference graph block lists `Desktop → Common, Logic`.
- [ ] `docs/architecture/overview.md` § 7 has rows for ADR-007 and ADR-008.
- [ ] `docs/architecture/overview.md` § 1 topology diagram shows both CLI and Desktop as operator-facing entry points.
- [ ] `adr/readme.md` index rows for 007 / 008 match the actual ADR titles and `Accepted` status.
- [ ] `CLAUDE.md` `## Repository Shape` table has a row for `ParameterizationExtractor.Desktop/`.
- [ ] `.github/copilot-instructions.md` is byte-identical to `CLAUDE.md`. SHA256 verified.
- [ ] `verify-bootstrap.ps1` (if present) — same pass/fail pattern as before this feature, plus the new ADRs counted in the ADR-count check.
- [ ] `ongoing-tasks/desktop-skeleton-checklist.md` `## Notes` has a closing summary entry.
- [ ] `dotnet build` and `dotnet test` still 0 / 34 (sanity).
- [ ] All four checklist items in `desktop-skeleton-checklist.md` are `[x]`.

## References

- Pattern exemplar for closing-summary notes: `ongoing-tasks/archive/msdi-migration-checklist.md` § Final summary.
- Architecture: `docs/architecture/overview.md`
- ADRs: `adr/007-desktop-wpf-stack.md`, `adr/008-desktop-ui-controls.md`, `adr/002-module-layering.md`
- Depends on: steps 01, 02, and 03 must all be `[x]` first.
