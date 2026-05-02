---
description: "Dry-run a step — plan only, no code."
arguments:
  - name: feature
    description: kebab-case feature name
    required: true
  - name: step
    description: zero-padded step number
    required: true
---

# Scope a task without coding

> Dry-run prompt. Reference this file with the feature name and step number you want scoped:
>
> ```
> #file:prompts/scope-task.md   feature={feature-name}   step=03
> ```

## Arguments

- **feature** — kebab-case feature name; checklist at `ongoing-tasks/{feature}-checklist.md`.
- **step** — zero-padded step number to scope (e.g. `03`).

---

You are SCOPING the step the user passed above. **Do not write code, do not edit any file.**

1. Read `ongoing-tasks/{feature}-checklist.md`.
2. Read the matching step file `ongoing-tasks/{feature}/{step}-{feature}-*.md`.
3. Report:
   1. What you would do first.
   2. Any ambiguities in the acceptance criteria.
   3. Files you would create or modify.

Stop after the report. The human approves, refines, or rejects before invoking [`execution.md`](execution.md).
