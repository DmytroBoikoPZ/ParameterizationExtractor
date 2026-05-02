---
description: "Two-phase audit of a checklist initiative — step compliance + feature-wide scan."
arguments:
  - name: checklist
    description: relative path to the checklist file
    required: true
---

# Checklist Audit

> Reference this file with the checklist path:
>
> ```
> #file:prompts/audit-checklist.md   checklist=ongoing-tasks/{feature}-checklist.md
> ```

## Arguments

- **checklist** — relative path to the checklist file to audit (e.g. `ongoing-tasks/payments-checklist.md`).

---

You are performing a **post-implementation audit** of the checklist initiative the user passed above.

## Preparation

1. Read the checklist file.
2. Initiative shape in this repo: checklist + `{feature}/feature-architecture.md` + numbered step files `{feature}/NN-*.md`. Read both the checklist and the feature-architecture file.
3. Identify the target stack(s) from the checklist / architecture and read the matching methodology recipe under `docs/methodology/` (the generic recipe is `feature-development.md`; if a stack-specific recipe exists, use it instead).
4. Read `CLAUDE.md` (or `.github/copilot-instructions.md`) — the tripwires you will check the implementation against.
5. Check `adr/` for ADRs referenced by or relevant to this initiative.

## Phase 1 — Step Compliance

For **each checked `[x]` step** in the checklist:

### A. Specification Compliance

- Open the step file.
- Read every acceptance criterion (AC).
- For each AC, locate the corresponding implementation in the codebase (file, class, method, route, etc.).
- For each AC, locate the corresponding test(s) that verify it.
- Flag: **AC not implemented**, **AC implemented but not tested**, **test exists but does not assert the AC**.
- **Heuristic for "test does not assert the AC":** extract the key noun-verb from the AC (e.g., "returns null on 404"); search the test for an assertion that mentions or implies it. If you cannot find an assertion that maps to the AC's claim, flag it for human review rather than silently passing.
- **Respect documented deviations:** if the checklist's `## Notes` explicitly documents a divergence from the step file (e.g., "Minor deviation: kept synchronous because pure string formatting"), treat that deviation as accepted and do NOT flag it. Only flag drift that is undocumented or contradicts user-approved decisions.

### B. Code Quality

- Check that the implementation follows the repo tripwires for its stack (from `CLAUDE.md`).
- Check for: hardcoded values that should come from configuration, missing DI registrations, dead code left behind, `TODO` / `FIXME` comments.
- Check that no business logic lives in thin layers (controllers, endpoints, page components) when the tripwires forbid it.
- Check that test naming follows the project's convention.

### C. Wiring & Integration

- Verify DI / IOC registrations exist in the appropriate location for all new services / interfaces (if applicable to the stack).
- Verify configuration sections are surfaced in the canonical config file (if CONFIG steps exist).
- Verify no orphaned interfaces (interface registered but never injected anywhere).
- Verify no orphaned implementations (class exists but not registered / not exported / not routed).
- Verify routes / endpoints / event handlers are reachable from outside the module.

### C2. INFRA steps (when applicable)

For any step that touches infrastructure-as-code (Helm, Terraform, CDK, Pulumi, raw YAML, container configs):

- Verify the manifest renders / synthesizes / validates without errors.
- Verify any new templated values have corresponding entries in the values / variables file (no orphaned references).
- Verify any new env-var references in deployment templates have corresponding declarations.
- Verify ignore-files (`.helmignore`, `.dockerignore`, etc.) keep internal-only files out of artifacts.

### D. Drift Detection

- Compare step file specifications against actual implementation — flag any divergence (extra features added, features described but missing, different naming).
- Check that the checklist's `## Notes` section documents any decisions that deviate from the original step files.

### E. Test Coverage

- Count total tests related to this initiative.
- For CODE steps, verify TDD was followed: each behavior should have at least one test.
- Run all tests for the affected components and report pass / fail counts.
  - Use the test commands declared in `prompts/execution.md` § Test Frameworks.
  - For initiatives that span multiple components, run each separately and report a per-component tally. Do not aggregate into a single number that hides which component is broken.
  - If tests fail, do not flag them all individually — note the count and check whether any failures are pre-existing (run on the main / trunk baseline). Pre-existing failures are out of scope for this audit.

## For unchecked `[ ]` steps

List them as **Not Started** — do not audit implementation (there should be none). If implementation exists for an unchecked step, flag it as **undocumented work**.

---

## Phase 2 — Feature-Wide Scan

Phase 2 runs AFTER Phase 1 is complete. It looks beyond step boundaries at the feature as a whole. Findings here are **heuristic** — flag for human review, not definitive failures.

### F. Implementation Surface

- Build a list of all files the feature touches: gather from step-file implementations found in Phase 1, plus scan the feature's directory/namespace for any production code.
- Flag any production file in the feature boundary that is NOT referenced by any step's ACs — this is potential **undocumented work** or leftover code.
- Flag any file outside the feature boundary that was modified (shared base classes, DI root, shared DTOs, middleware) — these are **leaked changes** that could affect other features.

### G. Cross-Step Integration

- Trace the feature's main flow end-to-end (request/event in -> processing -> response/side-effect out). Does it connect? Are there gaps between steps where data or control flow is assumed but not implemented?
- If the feature introduces new endpoints/consumers/handlers, verify they are all reachable from the entry point (routing, event subscription, job registration).

### H. Security Surface

- All new endpoints: are they behind authentication/authorization (unless the architecture explicitly marks them public)?
- All new inputs (request bodies, query params, message payloads): validated at the boundary?
- All new database queries: parameterized?
- Any new secrets/keys/tokens: loaded from config/vault, not hardcoded?
- Any new user-facing output: escaped/sanitized where applicable?

### I. Configuration Completeness

- Every new config key referenced in code has a corresponding entry in the canonical config file(s) (appsettings, .env.example, values.yaml, etc.).
- No config key has a fallback/default value that silently masks a missing setting (unless the repo's rules explicitly allow graceful degradation).

### J. Gaps the Plan Missed

- Based on the feature-architecture file and what you observed in Phase 1, are there obvious behaviors the feature needs that no step covers? (e.g., error handling, retries, logging, cache invalidation, cleanup/rollback).
- Are there edge cases visible from the code that no AC addresses?
- This section is intentionally subjective. Prefix each finding with **[SUGGESTION]** — these are not failures, they are items for the human to consider.

---

## Output Format

```
# Audit Report: {Initiative Name}

## Summary
- Steps checked: N/M
- Total tests: X (Y passing, Z failing)
- Phase 1 issues: K
- Phase 2 findings: L (M heuristic)

## Phase 1 — Step-by-Step Findings

### Step NN — {title} ✅ | ⚠️ | ❌
**Status:** All ACs met / Partial / Missing implementation
**ACs:**
- [x] {AC text} — verified in {test file}:{test name}
- [ ] {AC text} — ⚠️ {issue description}
**Code quality:** {any tripwire violations or concerns}
**Wiring:** {DI / config / routing issues if any}

### ...

## Phase 2 — Feature-Wide Findings

### Implementation surface
- Undocumented files: {list or "none"}
- Leaked changes to shared code: {list or "none"}

### Cross-step integration
{flow trace result, gaps found}

### Security surface
{findings or "all checks passed"}

### Configuration completeness
{findings or "all keys present"}

### Gaps the plan missed
- [SUGGESTION] {description}
- ...

## Recommendations
{Prioritized list — Phase 1 issues first (definitive), then Phase 2 findings (heuristic, for human review)}
```

## Rules

- Be thorough — read every step file and every AC.
- Be evidence-based — cite specific files and line numbers for every finding.
- Do not fix issues during audit — only report them.
- Do not create summary markdown files — the audit output in chat IS the report.
- If a step file references other steps as dependencies, verify those dependencies are satisfied.
- Respect explicitly documented deviations in the checklist's `## Notes` (rationale + user approval). Only flag undocumented divergence.
- If a check applies to a stack we don't have for the audited initiative, skip it silently — don't pad the report with "N/A" entries.
