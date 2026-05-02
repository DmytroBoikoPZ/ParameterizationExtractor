---
description: "Bootstrap phase 3 — strict write of approved values from decisions.md."
---

# Bootstrap — Phase 3: Apply (writes files)

> Reference this file after both `discovery.md` and `decisions.md` are approved:
>
> ```
> #file:prompts/bootstrap/03-apply.md
> ```
>
> No arguments. The two input files are read from disk.

---

You are running BOOTSTRAP PHASE 3 — APPLY.

## Inputs (READ BOTH FIRST)

- `ongoing-tasks/_bootstrap/discovery.md` — factual placeholders, approved.
- `ongoing-tasks/_bootstrap/decisions.md` — judgement calls, approved.

## Hard rules

- Write ONLY values that appear verbatim in the two input files.
- If a placeholder is not covered by either input, leave it as-is and list it in the final report under "Unresolved placeholders". Do NOT guess. Do NOT fill in defaults.
- If you find a contradiction between `discovery.md` and `decisions.md`, STOP and ask the human which one wins. Do not pick one yourself.
- After editing `CLAUDE.md`, mirror it byte-for-byte to `.github/copilot-instructions.md`. Do not regenerate from scratch.
- Do not invent feature checklists. Do not touch `ongoing-tasks/_template/` or `adr/_template.md`.
- For ADRs listed in `decisions.md` § Pending ADRs: create **stub files only** using the `adr/_template.md` structure (title + `Status: Proposed`). Do NOT fill in Context, Decision, or Consequences — the human drafts those post-bootstrap.
- One commit per logical unit (CLAUDE.md + its mirror = one commit; each `docs/methodology/<stack>.md` = one commit; etc.).

## Procedure

1. Apply factual placeholders from `discovery.md` across all template files.

2. Apply tripwires from `decisions.md` to `CLAUDE.md` (and mirror to `.github/copilot-instructions.md`). Replace each `<<TRIPWIRE_N>>` placeholder with the accepted tripwire text. Delete unused `<<TRIPWIRE_N>>` rows.

3. For each `docs/methodology/<stack>.md` stub, apply the action from `decisions.md`:
   - **KEEP-AS-IS**: leave the file untouched.
   - **FILL-WITH-PROJECT-SPECIFICS**: append project-specific notes under a `## Project-specific` section. Do not delete the generic content.
   - **DELETE**: delete the file. Also remove its row from the routing table in `CLAUDE.md`.

4. Write `docs/architecture/overview.md` from the approved sketch in `decisions.md`.

5. Write `.ignix/review-instructions.md` Exceptions and Project-specific notes from `decisions.md`.

6. For each title in `decisions.md` § Pending ADRs: create `adr/NNN-{short-kebab-title}.md` using `adr/_template.md` as the skeleton. Fill in only the title and set Status to `Proposed`. Leave Context, Decision, and Consequences as the template placeholders. Update `adr/readme.md` index. Number sequentially starting from `001`.

7. If the repo has NO `readme.md` at the root (the project's own readme, not `template-readme.md`), scaffold a minimal one with:
   - Project name (from `decisions.md` § Project identity)
   - One-line description
   - One-paragraph description
   - Pointer line: "For the AI-coding workflow this repo follows, see [template-readme.md](template-readme.md) and [ai-workflow.md](ai-workflow.md)."

   If `readme.md` already exists, leave it alone — never overwrite the project's own readme.

8. Run a final scan: grep for `<<` across the whole repo (excluding `_template/` folders, which intentionally retain placeholders). Any remaining markers go into the "Unresolved placeholders" section.

9. Run `scripts/verify-bootstrap.ps1 -RepoRoot <repo-root>`. Include the full output in the report. If it reports FAILures, list them as action items.

10. Print a final report — exactly this structure:

```
# Bootstrap Apply Report

## Files written
- path — what changed (one line each)

## Files deleted
- path — reason (from decisions.md)

## Tripwires applied
Per stack: count accepted, count rejected, count rewritten.

## Unresolved placeholders
| File | Placeholder | Why unresolved |

## Pending follow-ups (from decisions.md)
- ADRs scaffolded (need Context/Decision/Consequences drafted): ...
- Recipes to fill in detail later: ...

## Verification script output
{paste full output of scripts/verify-bootstrap.ps1}

## Cleanup
The folder ongoing-tasks/_bootstrap/ can now be deleted.
Suggested commit: "chore: bootstrap complete; remove _bootstrap workspace".
```

End with this exact line:

> APPLY COMPLETE.

---

## After running it

1. Review the report. Verify "Unresolved placeholders" is either empty or contains only items you intentionally deferred.
2. If clean: delete `ongoing-tasks/_bootstrap/` and commit.
3. Fill in the scaffolded ADR stubs in `adr/` — they have the title and `Proposed` status; you need to add Context, Decision, and Consequences.

## Why this phase exists

Apply has zero creative latitude on purpose. Every value it writes was approved by a human in Phases 1 or 2. If the inputs are wrong, the output is wrong in a predictable, reviewable way — not a hallucinated way.

The strict-stop-on-contradiction rule is the safety net: if Discovery and the Interview disagree, the human is the only one who can resolve it.
