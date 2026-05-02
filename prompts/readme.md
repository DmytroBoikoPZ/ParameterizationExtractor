# prompts/

> All operator prompts in one place. Each prompt is a plain markdown file you reference in chat with `#file:...`, optionally followed by arguments listed at the top of that file.

Humans drive the workflow by selecting a prompt; AI agents execute it. If you find yourself improvising a prompt for a recurring task, add it here.

## How to invoke

Each prompt declares its arguments in an `## Arguments` section near the top. Pass them on the same line as the file reference, `key=value` style. The reference syntax is tool-specific:

| Tool | Syntax |
|------|--------|
| Claude Code | `@prompts/bootstrap/01-discovery.md mode=brownfield` |
| GitHub Copilot Chat | `#file:prompts/bootstrap/01-discovery.md mode=brownfield` |
| Cursor | `@prompts/bootstrap/01-discovery.md mode=brownfield` |
| Other / generic | Paste the file contents into chat, or reference the path with your tool's file-reference convention. Always include the arguments. |

More examples (Copilot syntax shown; substitute your tool's prefix):

```
#file:prompts/audit-checklist.md      checklist=ongoing-tasks/payments-checklist.md
#file:prompts/continue-session.md     feature=payments   step=03
#file:prompts/add-stack.md            stack=go   service_path=services/notifier
```

Prompts with no arguments are referenced bare (no `key=value` tail):

```
#file:prompts/session-start.md
#file:prompts/execution.md
#file:prompts/bootstrap/03-apply.md
```

## Index

| When | Prompt | Arguments |
|------|--------|-----------|
| First time setting up the template in a project | [`bootstrap/01-discovery.md`](bootstrap/01-discovery.md) → [`02-interview.md`](bootstrap/02-interview.md) → [`03-apply.md`](bootstrap/03-apply.md) | `mode={brownfield\|greenfield}` (Phases 1–2); none (Phase 3) |
| Adding a new stack to an already-bootstrapped project | [`add-stack.md`](add-stack.md) | `stack=...   service_path=...` |
| Start of every AI session | [`session-start.md`](session-start.md) | none |
| Executing a checklist step | [`execution.md`](execution.md) | none (referenced by other prompts) |
| Continuing after a break / context reset | [`continue-session.md`](continue-session.md) | `feature=...   [step=NN]` |
| Scoping a step without writing code | [`scope-task.md`](scope-task.md) | `feature=...   step=NN` |
| Auditing a finished or in-progress checklist | [`audit-checklist.md`](audit-checklist.md) | `checklist=ongoing-tasks/{feature}-checklist.md` |

## How prompts compose

```
bootstrap/  ────────►  ongoing project work
   │                         │
   ▼                         ▼
discovery → interview      session-start  ───►  execution
   │                         │                     │
   ▼                         ▼                     ▼
   apply                  scope-task          audit-checklist
                                              continue-session
```

- `bootstrap/` runs once per repo, in order. Phase 1 → human review → Phase 2 → human review → Phase 3.
- `session-start.md` is read at the top of every chat where the AI will touch code.
- `execution.md` is referenced by `session-start.md` — agents read it before any step.
- `add-stack.md` reuses the bootstrap interview pattern, scoped to one new stack.

## Why prompts live here, not scattered

Earlier versions of this template kept prompts inside `ai-workflow.md`, `session-start.md`, and `ongoing-tasks/`. That made it impossible to answer "show me every prompt I would ever paste" without reading prose. This folder is the answer.

`ai-workflow.md` (operator handbook) **describes when** to use each prompt; the prompts themselves live here.
