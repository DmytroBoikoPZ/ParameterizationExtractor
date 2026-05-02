# Task Workflow — Plans, Checklists, Steps

> How AI agents and humans plan and execute work in this repo. Read this when starting or refactoring a plan. Referenced by `CLAUDE.md` and `prompts/session-start.md`.

---

**One feature = one checklist + one folder of granular steps.** The checklist is the single source of truth. There is no separate `plan.md`.

---

## Layout

```
ongoing-tasks/
├── readme.md                                    short conventions pointer
├── _template/                                   copy-paste starter
│   ├── _template-checklist.md
│   └── _template/
│       ├── feature-architecture.md              feature architecture overview
│       └── 01-_template-first-step.md
├── {feature-name}-checklist.md                  THE source of truth
├── {feature-name}/                              steps folder (name matches checklist)
│   ├── feature-architecture.md                  pipeline, component diagram, data flow
│   ├── 01-{feature-name}-<short-kebab>.md
│   ├── 02-{feature-name}-<short-kebab>.md
│   └── ...
└── archive/                                     completed or superseded work
    └── {old-feature}-checklist.md + {old-feature}/
```

### Naming rules

| Artifact | Pattern | Example |
|----------|---------|---------|
| Checklist | `{feature-name}-checklist.md` | `user-profile-api-checklist.md` |
| Step folder | `{feature-name}/` (matches checklist prefix) | `user-profile-api/` |
| Step file | `NN-{feature-name}-<short-kebab>.md` | `03-user-profile-api-wire-validation.md` |

- `feature-name` is kebab-case, stable, used identically across the checklist and folder.
- `NN` is zero-padded two-digit order (`01`, `02`, …, `99`).
- Include `feature-name` in every step filename so references in editor tabs and PR titles remain unambiguous.
- Phased numbering (`1.1`, `2.3`) is allowed **only** when the checklist explicitly groups steps into tracks/phases. Default is flat `NN`.

---

## Checklist template

A checklist has five sections. Keep it short — detail lives in steps.

```markdown
# {Feature Name}

## Goal
One to three sentences. What changes for the user or system when this is done.

## Scope
- In scope: bullets
- Out of scope: bullets
- Dependencies: links to other checklists / ADRs / services

## Architecture
See [feature-architecture.md](./{feature-name}/feature-architecture.md) for the big-picture overview.

## Steps
- [ ] [01 — short title](./{feature-name}/01-{feature-name}-short-kebab.md)
- [ ] [02 — short title](./{feature-name}/02-{feature-name}-short-kebab.md)
  - [ ] sub-bullet for in-step granularity that doesn't warrant its own step file

## Notes
Free-form log of decisions, blockers, links to PRs. Dated entries preferred (YYYY-MM-DD).
```

Rules:
- Use GitHub task-list syntax: `- [ ]` / `- [x]`.
- Each top-level item links to its step file.
- Sub-bullets (`- [ ]` nested) handle granularity below a step — do NOT create a new step file for every sub-task.
- Mark `[x]` as soon as the step's acceptance criteria are met and tested.

---

## Step file template

One step = one cohesive, testable unit of work. If a step grows past ~150 lines or sprouts multiple acceptance clusters, split it.

```markdown
# {NN} — {Feature Name} — {Short Title}

## Goal
One paragraph. What this step produces.

## Track
Which stack or service this step targets.

## What Exists
- Files, classes, endpoints already in place that this step modifies or depends on.
- Paths are workspace-relative.

## What to Build
- Bulleted list of concrete changes. Name files/types/endpoints, not the implementation.

## Acceptance Criteria
- [ ] Checkable, testable statements. One line per criterion.
- [ ] Include the test that proves it (unit / integration / manual).

## References
- Related ADRs: `adr/NNN-*.md`
- Related methodology: `docs/methodology/*.md`
- Upstream/downstream steps in other checklists (if any)
- External docs / RFCs if relevant
```

---

## Architecture file template

Every feature folder ships a `feature-architecture.md`. It is the feature's mental model — read by every contributor before any step work begins.

Required sections:
1. **Pipeline / integration** — how this feature fits into the system's data or request flow.
2. **Component diagram** — every service, store, queue, external dependency. Mark new vs. existing.
3. **Data flow** — inbound / processing / serving paths.
4. **Data stores** — table for any new collections, indices, queues.
5. **Extension points** — how new adapters or handlers plug in without modifying existing code.
6. **Security & isolation** — auth model, network boundaries, credential management, trust boundaries.

The full template is `ongoing-tasks/_template/_template/feature-architecture.md`.

---

## Lifecycle

1. **Create**
   - Copy `_template/_template-checklist.md` → `{feature-name}-checklist.md`.
   - Copy `_template/_template/` folder → `{feature-name}/`.
   - Fill in `{feature-name}/feature-architecture.md` first — shared mental model before step work.
   - Rename and write at least the first step. Subsequent steps can be stubbed and filled as scope clarifies.

2. **Execute**
   - Agent reads the checklist, then `{feature-name}/feature-architecture.md`.
   - Agent picks the next unticked step, opens the step file.
   - Agent implements → writes/updates tests → ticks the checklist item.
   - Agent does **not** edit the step file's scope mid-flight except to correct factual mistakes. If scope changes, add a new step.
   - Never edit a completed step's scope — write a follow-up step instead.
   - Do not create summary markdown files — the checklist + commit history + ADRs are the record.

3. **Log decisions**
   - Small, local decisions → `## Notes` in the checklist.
   - Broader / architectural decisions → new ADR in `adr/NNN-*.md` (then link from the checklist).

4. **Complete**
   - When every top-level item is `[x]`, move the checklist and the step folder into `ongoing-tasks/archive/`.
   - Git history is the audit trail; do not add a summary commit beyond the usual.

5. **Abandon / supersede**
   - Move the pair into `archive/` with a one-line reason added at the top of the checklist.

---

Architectural decisions that constrain future work go in `adr/NNN-*.md` (ADR). Link to them from the step's `## References`.
