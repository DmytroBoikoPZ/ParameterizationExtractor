---
description: "Add a new stack to a bootstrapped repo: tripwires + recipe + architecture update."
arguments:
  - name: stack
    description: short name of the new stack (go, rust, kotlin, etc.)
    required: true
  - name: service_path
    description: relative path to the new service
    required: true
---

# Add a Stack

> Use this AFTER bootstrap is complete (no `<<PLACEHOLDERS>>` in canonical files), when introducing a new language or framework that is not yet covered by the repo's tripwires or recipes.
>
> Reference this file with the new stack's name and path:
>
> ```
> #file:prompts/add-stack.md   stack=go   service_path=services/notifier
> ```

## Arguments

- **stack** — short name of the new stack (e.g. `go`, `rust`, `kotlin`).
- **service_path** — relative path to the new service in the repo.

## When NOT to use this

- Adding a second service in an **existing** stack already covered by tripwires + a recipe → just follow the recipe; no prompt needed.
- Replacing a stack (deprecating one, swapping for another) → write an ADR first, then use this prompt for the new stack.

---

You are running ADD-A-STACK for `stack=${stack}` at `service_path=${service_path}`. The project is already bootstrapped.

## Hard rules

- Do not edit any file until the human has approved your proposal.
- Do not weaken existing tripwires for other stacks.
- If a recipe stub already exists for this stack (`docs/methodology/<stack>.md`), read it and propose updates rather than starting fresh.

## Procedure

1. Read `CLAUDE.md` (especially the Stack-Specific Tripwires section) and `ai-workflow.md` to understand the conventions of this repo.

2. Scan the new service:
   - Manifest files (`go.mod`, `Cargo.toml`, `build.gradle`, etc.)
   - Build / test commands
   - Test framework
   - Folder layout

3. INTERVIEW the human, one topic at a time:

   a) **Service identity**:
      - Service name, path, one-line purpose.
      - How does it fit into the system architecture? (Will it appear in `docs/architecture/overview.md`?)

   b) **Tripwires for this stack**:
      - Propose a starter list (cite where each item came from — common community guidance, similar tripwires from other stacks in this repo, or patterns you noticed in the new service).
      - Flag every proposed item as **REQUIRES_HUMAN_REVIEW**.
      - Ask: "What has your team gotten burned by in this stack recently?" — these become the highest-value additions.
      - Final accepted list goes into `CLAUDE.md`.

   c) **Recipe**:
      - Does `docs/methodology/<stack>.md` already exist?
        - If yes: propose specific edits to align with how the new service is built.
        - If no: propose the table of contents for a new stub. Confirm with the human before drafting any sections.
      - Recipe stub structure should mirror the existing recipes in `docs/methodology/` (folder structure, naming, layering, DI, config, HTTP clients, logging, testing, tripwires).

4. After approval, write:
   - **`CLAUDE.md`** (and mirror to `.github/copilot-instructions.md`):
     - Add the routing-table row for the new stack recipe.
     - Add the Stack-Specific Tripwires subsection.
     - Update the Repository Shape table.
   - **`docs/methodology/<stack>.md`**: create or update per the agreed plan.
   - **`docs/architecture/overview.md`**: add the new service to the component table / topology if applicable.
   - **`.ignix/review-instructions.md`**: add any new exceptions if needed (rare).

5. Print a summary of changes (files written, tripwires added, architecture updated y/n).

End with this exact line:

> ADD-STACK COMPLETE.

---

## Why this prompt exists

Without it, teams either (a) skip adding tripwires for the new stack and lose the safety net, or (b) ask the AI to "do bootstrap again" and get inconsistent results. This prompt is narrow — one stack, one interview, one set of writes — and reuses the bootstrap principles (AI proposes, human decides; strict writes after approval).
