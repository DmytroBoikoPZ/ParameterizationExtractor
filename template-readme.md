# Context-Driven Development — Template Repo

> A reusable scaffold for AI-assisted software projects. Drop it into a fresh repo or layer it onto an existing one, then let an AI agent fill in the placeholders.

This repo is **not a working project**. It is a structured prompt system — a set of files that tell an AI coding agent (Claude Code, GitHub Copilot, Cursor, etc.) where to find rules, what to read first, how to plan work, and how to execute it.

The same idea drives serious AI-coding setups in larger codebases. This repo distils the pattern into a stack-agnostic skeleton that you can copy and adapt.

> **What you'll find where.** This file explains *what the template is and why it works that way*. For prompts, lifecycle commands, and day-to-day operator recipes, see [ai-workflow.md](ai-workflow.md).

---

## The 30-second pitch

AI coding here is not "chat and hope." It runs on **three layers of guidance** plus **structured work orders**:

1. **Auto-loaded rules** (`CLAUDE.md` + `.github/copilot-instructions.md`) — what the AI must never violate. Loaded every turn, no matter what.
2. **Session bootstrap** (`prompts/session-start.md`) — read once per session. Where things live, what the non-negotiables are.
3. **Execution protocol** (`prompts/execution.md`) — how to run a task: classification, TDD where it applies, reporting format.

Work is organised as **checklists + step files**. One feature = one checklist (the source of truth) + one folder of granular, self-contained steps. The human points the AI at a checklist; the AI ticks its way through the steps.

Architectural decisions live in **ADRs** under [`adr/`](adr/). The AI reads them as constraints, not history.

---

## Repo structure

```
.
├── template-readme.md               This file — template concept, context flow, why-it-works
├── readme.md                        (the project's own readme — not part of this template; create or keep your existing one)
├── ai-workflow.md                  Operator handbook — prompts, lifecycle, daily commands
├── CLAUDE.md                       Auto-loaded routing table + tripwires (Claude Code)
├── .github/
│   └── copilot-instructions.md     Mirror of CLAUDE.md (auto-loaded by GitHub Copilot)
├── .ignix/
│   └── review-instructions.md      Per-repo automated PR review rules
│
├── prompts/                         All operator prompts in one place
│   ├── readme.md                    Index of prompts
│   ├── session-start.md             Read-once-per-session bootstrap
│   ├── execution.md                 TDD / task-classification / reporting protocol
│   ├── audit-checklist.md           Post-implementation audit prompt
│   ├── continue-session.md          Resume after a break / context reset
│   ├── scope-task.md                Dry-run a step (plan only, no code)
│   ├── add-stack.md                 Introduce a new stack post-bootstrap
│   └── bootstrap/                   First-time setup, three phases
│       ├── 01-discovery.md          Read-only scan, factual placeholders
│       ├── 02-interview.md          AI asks human for tripwires/architecture
│       └── 03-apply.md              Strict write of approved values
│
├── docs/
│   ├── methodology/
│   │   ├── task-workflow.md        Checklists + step files conventions, lifecycle
│   │   ├── feature-development.md  Generic feature recipe — the "how" of building
│   │   ├── dotnet-service.md       Stub recipe — fill on bootstrap
│   │   ├── python-service.md       Stub recipe — fill on bootstrap
│   │   ├── node-service.md         Stub recipe — fill on bootstrap
│   │   └── react-ui.md             Stub recipe — fill on bootstrap
│   └── architecture/
│       └── overview.md             System topology (placeholders to fill on bootstrap)
│
├── adr/
│   ├── readme.md                   ADR index + format guide
│   └── _template.md                Copy this when writing a new ADR
│
└── ongoing-tasks/
    ├── readme.md                   TL;DR + pointer to methodology
    ├── _template/                  Copy-paste starter for a new initiative
    │   ├── _template-checklist.md
    │   └── _template/
    │       ├── feature-architecture.md
    │       └── 01-_template-first-step.md
    └── archive/                    Completed or superseded checklists land here
```

---

## How the AI builds context — the layered flow

When a human says *"Read prompts/session-start.md. Follow methodology. Execute the next unticked step in `feature-x-checklist.md`"*, the agent assembles its working context in layers:

```
┌─────────────────────────────────────────────────────────────────────┐
│  LAYER 0 — ALWAYS PRESENT (auto-loaded)                             │
│                                                                     │
│  CLAUDE.md                       (Claude Code reads this)           │
│  .github/copilot-instructions.md (GitHub Copilot reads this)        │
│                                                                     │
│  ROLE: Hard rules — naming, architecture, forbidden patterns,       │
│        repo shape. The tripwires the AI must never violate.         │
└─────────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  LAYER 1 — SESSION BOOTSTRAP                                        │
│  Trigger: human says "Read prompts/session-start.md"                 │
│                                                                     │
│  prompts/session-start.md instructs the agent to read, in order:    │
│    • adr/readme.md            (active constraints)                  │
│    • docs/architecture/*.md   (system topology)                     │
│    • ongoing-tasks/readme.md  (where active work lives)             │
│                                                                     │
│  ROLE: Project awareness — what's decided, what exists, what's      │
│        active. Prevents re-deciding settled questions.              │
└─────────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  LAYER 2 — METHODOLOGY                                              │
│  Trigger: human says "Follow methodology" (or session-start points) │
│                                                                     │
│  docs/methodology/feature-development.md                            │
│    → the recipe: how to design, implement, test, wire, ship         │
│  docs/methodology/task-workflow.md                                  │
│    → checklist + step file conventions, naming, lifecycle           │
│                                                                     │
│  ROLE: Process — HOW to build. Step-by-step recipe with checklist   │
│        of validations to run before declaring done.                 │
└─────────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  LAYER 3 — STEP FILE                                                │
│  Trigger: human says "Execute step NN" or "next unticked step"      │
│                                                                     │
│  ongoing-tasks/{feature}-checklist.md  (source of truth)            │
│  ongoing-tasks/{feature}/feature-architecture.md  (feature mental model)    │
│  ongoing-tasks/{feature}/NN-{feature}-<short>.md  (the work order)  │
│                                                                     │
│  Step file sections: Goal · Track · What Exists · What to Build ·   │
│                      Acceptance Criteria · References               │
│                                                                     │
│  ROLE: Task specification — WHAT to build. Field-level detail.      │
└─────────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  LAYER 4 — DERIVED (agent reads autonomously)                       │
│                                                                     │
│  • Existing code referenced by the step file (patterns to mimic)    │
│  • ADRs cited by the step file or the checklist                     │
│  • External docs / RFCs / API specs the step references             │
│                                                                     │
│  ROLE: Domain truth + pattern conformance. The agent fills these    │
│        in based on what the step file points at.                    │
└─────────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  EXECUTION                                                          │
│                                                                     │
│  Layer 0 (rules)        → never violated                            │
│  Layer 2 (methodology)  → followed step by step                     │
│  Layer 3 (step file)    → built to spec                             │
│  Layer 4 (existing)     → patterns matched                          │
│                                                                     │
│  Agent ticks the checklist · reports briefly · moves to next step.  │
└─────────────────────────────────────────────────────────────────────┘
```

### Rule hierarchy — when guidance conflicts

```
PRIORITY 1 (highest)  Tripwires in CLAUDE.md / copilot-instructions.md
                      Cannot be overridden by any step or methodology.

PRIORITY 2            ADRs (adr/NNN-*.md)
                      Architectural constraints. Override methodology suggestions.

PRIORITY 3            Methodology (docs/methodology/*.md)
                      The default recipe. A step file can specialize it.

PRIORITY 4 (lowest)   Step file specifics
                      Task-level details, within the rules above.
```

---

## CLAUDE.md vs `.github/copilot-instructions.md`

Both files are **auto-loaded** by their respective tools — Claude Code reads `CLAUDE.md` from workspace roots; GitHub Copilot reads `.github/copilot-instructions.md`. Other agents (Cursor, Aider, Continue) tend to follow one of these two conventions, or both.

**This template ships them as duplicates.** The content is identical. Pick one as canonical, mirror to the other, and treat keeping them in sync as part of the change.

> Why duplicate instead of pointing? Auto-load is path-specific. A pointer file (`> See ../CLAUDE.md`) is unreliable — the agent may not chase it before generating code. Two copies of the rules is annoying; the AI silently violating them because it never read the rules is worse.

If your team only uses one tool, delete the other file and update this map to match.

---

## Two modes of operation

**Bootstrap mode** — if `<<PLACEHOLDERS>>` (double angle brackets) are still present anywhere in the repo, you are in bootstrap mode. The bootstrap workflow runs in **three phases** — a read-only **Discovery** pass (scans the repo for factual placeholders), an **Interview** pass (AI asks the human about tripwires, architecture, and recipe decisions), and an **Apply** pass (strict write of approved values). All three prompts live in [`prompts/bootstrap/`](prompts/bootstrap/). The AI should **not** invent ADRs, methodology recipes, tripwires, or feature checklists during bootstrap — those are human-driven decisions captured in the Interview phase.

**Steady-state mode** — once placeholders are gone, work proceeds checklist-by-checklist. Driving prompts, continuation prompts, audit prompts, and the post-bootstrap "add a new stack" prompt all live in [`prompts/`](prompts/). The operator handbook in [ai-workflow.md](ai-workflow.md) describes when to use each one.

---

## Adapting the template

| Want to… | Do this |
|----------|---------|
| Fill in a stack recipe stub | Edit the matching file under `docs/methodology/` (`dotnet-service.md`, `python-service.md`, `node-service.md`, `react-ui.md`). Already linked from `CLAUDE.md` “Where to Read What”. |
| Add a brand-new stack recipe (e.g., `go-service.md`) | Add a file under `docs/methodology/`. Link it from `CLAUDE.md` “Where to Read What”. |
| Add a stack-specific tripwire | Edit `CLAUDE.md`. Mirror to `.github/copilot-instructions.md`. |
| Record an architectural decision | Copy `adr/_template.md` → `adr/NNN-title.md`. Update `adr/readme.md` index. |
| Start a feature | Copy `ongoing-tasks/_template/_template-checklist.md` → `ongoing-tasks/{feature}-checklist.md`. Copy `_template/_template/` → `ongoing-tasks/{feature}/`. Rename and fill. |
| Archive a finished feature | Move the checklist + folder pair into `ongoing-tasks/archive/`. |
| Customise automated PR review | Edit `.ignix/review-instructions.md` — it is fetched from the target branch and prepended to the reviewer's default prompt. |

---

## Credits

The pattern is distilled from working setups across larger codebases — the layered context model, the checklist + step files convention, the rule hierarchy, the audit prompt. This repo is the stack-agnostic skeleton; your project-specific rules and recipes are what make it useful.
