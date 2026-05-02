# ongoing-tasks/

Active work orders. **One feature = one checklist + one steps folder.**

## TL;DR

- `{feature-name}-checklist.md` is the **single source of truth** for an initiative (goal, scope, step links, notes).
- `{feature-name}/` holds granular, self-contained step files: `NN-{feature-name}-<short-kebab>.md`.
- `{feature-name}/feature-architecture.md` is the feature's mental model — read at the start of every step.
- No separate `plan.md`. The checklist *is* the plan.
- Completed or superseded work moves to `archive/`.

## System prompts

All operator prompts (execution, audit, bootstrap, session-start, etc.) live in [`prompts/`](../prompts/). The two most relevant for ongoing-tasks work:

| Prompt | Purpose |
|--------|---------|
| [`prompts/execution.md`](../prompts/execution.md) | TDD cycle, task classification, reporting format. The AI follows this when executing any step. |
| [`prompts/audit-checklist.md`](../prompts/audit-checklist.md) | Post-implementation audit. Run against a finished or in-progress checklist to detect drift, missing tests, dead code, broken wiring. |

## Starting a new initiative

1. Copy `_template/_template-checklist.md` → `{feature-name}-checklist.md`.
2. Copy `_template/_template/` → `{feature-name}/`.
3. Rename every occurrence of `_template` / `feature-name` in filenames and inside the files.
4. Fill in `{feature-name}/feature-architecture.md` first (pipeline, components, data flow).
5. Write Goal + Scope in the checklist; write at least step `01` in detail.

## Full conventions

See **[`docs/methodology/task-workflow.md`](../docs/methodology/task-workflow.md)** for templates, naming rules, lifecycle, and relationship to ADRs.

See **[`docs/methodology/feature-development.md`](../docs/methodology/feature-development.md)** for the generic feature recipe (the "how" of building a step).
