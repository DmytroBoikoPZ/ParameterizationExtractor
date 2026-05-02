---
description: "Bootstrap phase 2 — AI asks the human for tripwires, architecture, and recipe decisions."
arguments:
  - name: mode
    description: brownfield | greenfield
    required: true
---

# Bootstrap — Phase 2: Interview (AI asks, human answers)

> Reference this file after Phase 1 is complete and `ongoing-tasks/_bootstrap/discovery.md` is approved:
>
> ```
> #file:prompts/bootstrap/02-interview.md   mode=brownfield
> ```

## Arguments

- **mode** — same value used in Phase 1 (`brownfield` or `greenfield`).

---

You are running BOOTSTRAP PHASE 2 — INTERVIEW in the `mode` the user passed above.

## Inputs

- `ongoing-tasks/_bootstrap/discovery.md` — the approved Discovery proposal.
- The human, who will answer your questions in chat.

## Hard rules

- Do not edit any template file. Only write to `ongoing-tasks/_bootstrap/decisions.md` (create the folder if missing).
- Ask ONE topic at a time. Wait for the human's answer before moving on.
- For tripwires: you MAY propose a starter list per detected stack, but flag every proposed item as **REQUIRES_HUMAN_REVIEW**. The human must explicitly accept, reject, or rewrite each one.
- Do not propose ADRs.
- If the human says "skip" or "not sure", record DEFERRED and move on. Apply phase will leave the corresponding placeholder unresolved and surface it in the final report.

## Procedure — ask topics in order, one at a time

1. **PROJECT IDENTITY**
   - Project name (one phrase)?
   - One-line project description (what it does, for whom)?
   - One-paragraph description (for the project's `readme.md` / `CLAUDE.md`)?

2. **STACKS** — confirm or correct the stacks Discovery detected. For each confirmed stack:
   - Service inventory: name → path → one-line purpose.
   - If multiple services in the same stack: any shared layout convention?

3. **TRIPWIRES** — for EACH confirmed stack:
   - Show the matching `docs/methodology/<stack>.md` tripwire section as a starting set, plus any patterns you noticed in their code that suggest additional tripwires.
   - For each candidate tripwire, ask: ACCEPT / REJECT / REWRITE?
   - Also ask: "What are the 3-5 things your team has gotten burned by recently in this stack?" — these become the highest-value tripwires.
   - Record the final accepted list per stack.

4. **RECIPES** — for each `docs/methodology/<stack>.md` stub:
   - KEEP-AS-IS / FILL-WITH-PROJECT-SPECIFICS / DELETE?
   - For FILL: ask for the project's specific deviations from the stub (e.g., "we use MediatR everywhere", "we never use attribute routing").

5. **SYSTEM ARCHITECTURE** (`docs/architecture/overview.md`):
   - Walk through the directory structure Discovery found.
   - Which components communicate with which? (build a small list)
   - What data stores / queues / external services exist?
   - Any cross-cutting concerns (auth, observability, feature flags) worth a top-level mention?
   - Sketch a draft. Show it. Get accept / amend / reject.

6. **PR REVIEW EXCEPTIONS** (`.ignix/review-instructions.md`):
   - Any path globs or regex patterns the automated reviewer should skip? (generated code, vendored libs, fixtures)
   - Any project-specific style notes the reviewer should know?
   - Default: leave Exceptions empty.

7. **INITIAL ADRS**:
   - "Are there any architectural decisions already locked in that should be recorded as ADRs before any feature work begins?"
   - If yes: list them by title only. Do NOT draft the ADRs themselves in this phase.

## Output — write to `ongoing-tasks/_bootstrap/decisions.md`

```
# Bootstrap Decisions

## Project identity
- name: ...
- one-liner: ...
- paragraph: ...

## Stacks
| Stack | Path | Purpose |

## Tripwires
### <stack>
- [accepted] ...
- [accepted] ...

## Recipe decisions
| Stub | Action | Project-specific notes |

## System architecture
{paste the agreed sketch}

## PR review
- Exceptions: ...
- Notes: ...

## Pending ADRs (titles only)
- ...
```

End with this exact line:

> INTERVIEW COMPLETE. decisions.md written. Awaiting human review before proceeding to Phase 3 — Apply.

---

## After running it

1. Review `ongoing-tasks/_bootstrap/decisions.md`. Edit it directly to change anything — Apply will read whatever is in this file.
2. Reference [`03-apply.md`](03-apply.md).

## Why this phase exists

Tripwires, architecture topology, and recipe-fit decisions are not deductions from a code scan — they are team knowledge. The AI's job here is to **structure the conversation and capture answers**, not invent them. The hybrid mode for tripwires (AI proposes, every item flagged for review) keeps the human's effort focused on judgement rather than blank-page paralysis.
