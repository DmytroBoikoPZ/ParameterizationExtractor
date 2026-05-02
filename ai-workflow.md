# Working with AI in this repo

> **Audience: humans.** Operator handbook for driving AI coding sessions (Claude Code, GitHub Copilot, Cursor, etc.) against this codebase.
>
> The **prompts themselves** live in [`prompts/`](prompts/). This file describes **when** to use each one and how the lifecycle hangs together.
>
> If you want the *concept* (why the template is shaped this way, what the layered context model is), read [template-readme.md](template-readme.md) first. If you are an AI agent, read [`prompts/session-start.md`](prompts/session-start.md) instead.

---

## Table of contents

- [Lifecycle at a glance](#lifecycle-at-a-glance)
- [Bootstrapping a project](#bootstrapping-a-project)
- [Adding a new stack later](#adding-a-new-stack-later)
- [Day-to-day work](#day-to-day-work)
- [Auditing a checklist](#auditing-a-checklist)
- [Rules of thumb](#rules-of-thumb-for-humans)
- [Where to read more](#where-to-read-more)

---

## Lifecycle at a glance

```
                    Drop template into repo
                              │
                              ▼
                ┌─────────────────────────────┐
                │ BOOTSTRAP (one-time)        │
                │  prompts/bootstrap/01 → 02 → 03
                │  Discovery → Interview → Apply
                └─────────────────────────────┘
                              │
                              ▼
                ┌─────────────────────────────┐
                │ STEADY STATE (every session)│
                │  prompts/session-start.md   │
                │      → prompts/execution.md │
                │      → tick checklist       │
                │      → repeat               │
                └─────────────────────────────┘
                              │
                ┌─────────────┴─────────────┐
                ▼                           ▼
        New stack appears            Need to verify
        prompts/add-stack.md         finished work
                                     prompts/audit-checklist.md
```

---

## Bootstrapping a project

If `<<PLACEHOLDERS>>` (double angle brackets) appear anywhere in canonical files, you are in bootstrap mode. Bootstrap is **three phases** with a hard human gate between each.

| Phase | Prompt | What it does | Output |
|-------|--------|--------------|--------|
| 1 | [`prompts/bootstrap/01-discovery.md`](prompts/bootstrap/01-discovery.md) | Read-only scan. Fills only factual placeholders detectable from manifests / scripts. | Proposal printed in chat → save to `ongoing-tasks/_bootstrap/discovery.md` |
| 2 | [`prompts/bootstrap/02-interview.md`](prompts/bootstrap/02-interview.md) | AI asks the human structured questions for tripwires, architecture, recipe decisions. AI proposes; human accepts/rejects each item. | `ongoing-tasks/_bootstrap/decisions.md` |
| 3 | [`prompts/bootstrap/03-apply.md`](prompts/bootstrap/03-apply.md) | Strict write of approved values. Stops on any contradiction or unexpected case. | Edited template files + final report |

After Phase 3, delete `ongoing-tasks/_bootstrap/` and commit.

> **Why three phases?** Discovery is cheap and AI-reliable (mechanical fill-in-the-blanks). Interview is human-led (judgement calls). Apply has zero creative latitude (writes only what's approved). Mixing these into one prompt is the common cause of "the AI invented seven ADRs we did not ask for."

> **Greenfield vs brownfield.** Pass `mode=greenfield` or `mode=brownfield` at the top of the Discovery and Interview prompts. Greenfield runs are short (nothing to scan) and lean heavily on the Interview.

---

## Adding a new stack later

When a new language or framework lands in an already-bootstrapped repo (e.g., adding a Go service to a .NET + React project), use [`prompts/add-stack.md`](prompts/add-stack.md). It reuses the bootstrap interview pattern, scoped to one stack: confirm service identity → propose tripwires (human accepts each) → fill or create the matching `docs/methodology/<stack>.md` recipe → update CLAUDE.md and the architecture overview.

Do **not** re-run bootstrap for a new stack. Bootstrap is one-time.

---

## Day-to-day work

### 1. Make sure the initiative exists

Either an existing checklist (`ongoing-tasks/{feature-name}-checklist.md` + its `{feature-name}/` steps folder), or create one:

```
cp ongoing-tasks/_template/_template-checklist.md \
   ongoing-tasks/{feature-name}-checklist.md
cp -r ongoing-tasks/_template/_template \
   ongoing-tasks/{feature-name}
# Then rename step files: 01-{feature-name}-<short-kebab>.md
```

Write the Goal, Scope, and at least step `01` before invoking the AI.

### 2. Start the session

Paste verbatim, substituting `{feature-name}`:

```
Read prompts/session-start.md and prompts/execution.md.
Then execute ongoing-tasks/{feature-name}-checklist.md starting from
the first unticked step.
```

The AI will:
1. Load `CLAUDE.md` / `.github/copilot-instructions.md` automatically.
2. Read [`prompts/session-start.md`](prompts/session-start.md) for orientation.
3. Read [`prompts/execution.md`](prompts/execution.md) for the execution protocol.
4. Open the checklist, find the first unticked step, open that step file.
5. Implement → add/update tests → tick the checklist item → report briefly → next step.

### 3. Continue, jump, or scope

| Need | Use |
|------|-----|
| Continue after a break or context reset | [`prompts/continue-session.md`](prompts/continue-session.md) |
| Jump to a specific step number | [`prompts/continue-session.md`](prompts/continue-session.md) (last block) |
| Plan a step without writing code | [`prompts/scope-task.md`](prompts/scope-task.md) |

---

## Auditing a checklist

Use [`prompts/audit-checklist.md`](prompts/audit-checklist.md) before declaring a feature done, or when adopting a feature someone else built.

```
Read prompts/audit-checklist.md, then audit
ongoing-tasks/{feature-name}-checklist.md.
```

The audit cross-checks every ticked step against its Acceptance Criteria, tripwires, ADRs, and stack recipes.

---

## Rules of thumb for humans

- **One feature = one checklist.** Don't split a coherent initiative across several.
- **Checklists carry scope + notes; step files carry specifications.** Don't log progress inside a step file — log it in the checklist's `## Notes`.
- **If the AI wants to refactor beyond the step's scope, say no.** Open a new step or a new checklist instead.
- **Architectural decisions go in `adr/NNN-*.md`** — not inside a plan or checklist.
- **After a checklist is fully ticked:** move it + its steps folder to `ongoing-tasks/archive/`. Don't delete.
- **Keep `CLAUDE.md` and `.github/copilot-instructions.md` in sync.** They are duplicates by design — one canonical, one mirror. If you change one, mirror immediately.
- **Do not silence the PR reviewer with broad exceptions.** When a real false positive appears, add a narrow path/regex to `.ignix/review-instructions.md § Exceptions` with a one-line rationale.
- **All prompts live in [`prompts/`](prompts/).** If you find yourself improvising a prompt for a recurring task, add it there rather than scattering it across docs.

---

## Where to read more

| To learn about… | Read |
|------------------|------|
| The big picture / why the template is shaped this way | [template-readme.md](template-readme.md) |
| All available operator prompts | [prompts/readme.md](prompts/readme.md) |
| System architecture | [docs/architecture/overview.md](docs/architecture/overview.md) |
| How plans / checklists / steps are structured | [docs/methodology/task-workflow.md](docs/methodology/task-workflow.md) |
| Generic feature recipe | [docs/methodology/feature-development.md](docs/methodology/feature-development.md) |
| Stack-specific recipes | `docs/methodology/<stack>.md` (dotnet, python, node, react) |
| Automated PR review instructions | [.ignix/review-instructions.md](.ignix/review-instructions.md) |
| Hard coding rules | [CLAUDE.md](CLAUDE.md) (or [.github/copilot-instructions.md](.github/copilot-instructions.md)) |
| Active architectural constraints | [adr/readme.md](adr/readme.md) |
