# 01 — {Feature Name} — {Short Title}

> Template. Rename this file to `01-{feature-name}-<short-kebab>.md`.
> Keep the `NN-` zero-padded order prefix + feature name + short kebab title.

## Goal
One paragraph. What this step produces.

## Track
Which stack or service this step targets (e.g., `backend`, `frontend`, `infra`, `cross-cutting`).

## What Exists
- Files, classes, endpoints already in place that this step modifies or depends on.
- Paths are workspace-relative.
- If a "pattern exemplar" exists in the codebase that this step should mimic, name it here.

## What to Build
- Bulleted list of concrete changes.
- Name the files / types / endpoints, not the implementation.
- One bullet per deliverable. If a bullet has more than three sub-points, consider splitting into a new step.

## Acceptance Criteria
- [ ] Checkable, testable statement (with the test that proves it: unit / integration / manual).
- [ ] Another criterion.
- [ ] One line per criterion. No multi-line ACs — they hide ambiguity.

## References
- Related ADRs: `adr/NNN-*.md`
- Related methodology: `docs/methodology/*.md`
- Upstream/downstream steps: link to other step files if relevant
- External docs / RFCs / API specs if relevant
- Depends on: list of other step files that must be `[x]` before this one starts
