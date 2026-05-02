# Feature Development — Generic Recipe

> The "how" of building a feature in this repo. Stack-agnostic. When a stack-specific recipe exists (e.g., `dotnet-service.md`, `react-ui.md`), follow that one and use this doc as a fallback.

> Read **before** proposing patterns. Don't invent conventions.

---

## TL;DR — the cycle

```
1. Understand           → read checklist + step file + feature-architecture.md
2. Locate what exists   → grep / read existing patterns the step points at
3. Design contracts     → types, signatures, data shapes (no implementation yet)
4. Implement minimally  → enough code to satisfy the step, nothing more
5. Test                 → per the execution protocol's TDD/CONTRACT/CONFIG rules
6. Wire up              → DI / routing / config / build manifests
7. Verify               → run tests, run the thing, check acceptance criteria
8. Tick + report        → mark `[x]` in checklist, brief report, move on
```

The cycle assumes the checklist and step file already exist. If they don't, see `task-workflow.md` first.

---

## 1. Understand the work

Before writing anything:

- Read the checklist's **Goal**, **Scope**, and **Notes**.
- Read the feature's `feature-architecture.md` — pipeline, component diagram, data flow.
- Read the step file end-to-end. Pay attention to:
  - **What Exists** — current state of the codebase for this step.
  - **What to Build** — the concrete deliverables.
  - **Acceptance Criteria** — what "done" means, testably.
  - **References** — ADRs, methodology, upstream/downstream steps.
- Re-read the matching tripwires section in `CLAUDE.md`.

If anything is ambiguous, ask the human before coding. A 60-second clarification beats a 30-minute rework.

---

## 2. Locate what exists

For every entity, service, or module the step touches:

- Find the closest existing example in the codebase. Match its conventions.
- Identify the file(s) you'll edit vs. the file(s) you'll create.
- If the step references a "pattern exemplar" (e.g., "follow the pattern of X"), read that exemplar before designing.

> **The repo's existing code outranks any abstract advice in methodology docs.** If conventions diverge, match the closest recent code and flag the inconsistency in the checklist's `## Notes`.

---

## 3. Design contracts first

Before implementation:

- Sketch the public surface — function signatures, type definitions, request/response shapes, event payloads.
- Confirm naming matches the conventions in `CLAUDE.md` (entities, DTOs, services, controllers, etc.).
- Validate the contract against the step's Acceptance Criteria — every AC should be expressible in terms of the contract.

If the contract introduces a new pattern (new abstraction, new module boundary, new shared interface), **stop and ask**. New patterns are architectural decisions, not implementation details.

---

## 4. Implement minimally

- Write only what the step requires. No future-proofing, no "while I'm here" cleanup.
- Edit existing files before creating new ones.
- Keep functions focused. Long functions are a smell — break them up only if the break-up improves clarity, not because of an arbitrary line count.
- No comments unless the WHY is non-obvious. Don't narrate WHAT the code does.
- No `TODO` / `FIXME` left behind. If you can't finish, stop and escalate.

---

## 5. Test

Follow `prompts/execution.md` for the per-task-type rules. Short version:

| Task type | Approach |
|-----------|----------|
| **CODE** (business logic, handlers, services, endpoints) | TDD: RED → GREEN → REFACTOR. Write the failing test first. |
| **CONTRACT** (interfaces, records, DTOs, event payloads) | Type + serialization test. No RED phase needed for pure data types. |
| **CONFIG** (DI wiring, config files, middleware pipeline) | Implement → verify build → integration test if behavior changes. |
| **INFRA** (manifests, charts, container configs) | Implement → deploy to a sandbox → verify manually. |
| **CLEANUP** (deletes, renames, moves) | Ensure existing tests still pass. New test only if a coverage gap is found. |
| **UI** (components, layouts, design tokens) | Implement → visual verify in the browser → component test for interaction logic. |

**Test structure** (Arrange / Act / Assert):
```
// Arrange — set up test data and mocks
// Act — call the method under test
// Assert — verify expected outcome
```

Tests live next to (or in the conventional test location for) the code they test. Naming matches the project's existing convention.

---

## 6. Wire up

Implementation is not done until it's reachable. Verify:

- DI / IOC registration (if applicable).
- Route / endpoint registration (if applicable).
- Configuration sections in the canonical config file.
- Build manifests, deployment specs, infra charts (if INFRA-touched).
- Public API exports / barrel files (if applicable).

A common bug: the code is correct but never registered, never routed, never imported. Catch this here.

---

## 7. Verify

Before ticking the checklist item:

- Run the relevant test suite. All pass.
- Run the build. No new errors or warnings introduced.
- For CODE / UI: exercise the feature end-to-end where feasible (HTTP call, UI flow, CLI invocation).
- Walk through the step's Acceptance Criteria one by one. Every box checks.
- Walk through the methodology validation list (next section).

---

## 8. Tick + report

- Mark the checklist item `[x]`.
- Add a dated entry to the checklist's `## Notes` if anything non-obvious happened (a deviation, a discovered gotcha, a deferred follow-up).
- Report briefly per `prompts/execution.md`'s format. No standalone summary documents.

---

## Validation checklist (run before declaring done)

- [ ] Acceptance criteria from the step file all hold.
- [ ] Tests pass — relevant suite green.
- [ ] No `TODO` / `FIXME` / dead code added.
- [ ] No hardcoded secrets, URLs, or environment-specific values.
- [ ] No tripwires from `CLAUDE.md` violated.
- [ ] No new abstractions / new modules / new interfaces beyond what the step calls for.
- [ ] DI / routing / config / build wiring all done.
- [ ] Naming matches the closest existing code, not just abstract advice.
- [ ] Step file's scope was not edited mid-flight (only `## Notes` in the checklist).
- [ ] No standalone summary markdown files created.
- [ ] Checklist item ticked; concise report sent.

---

## Anti-patterns

Things that look reasonable but are wrong here:

- **Adding "while I'm here" cleanup.** Open a new step instead.
- **Inventing a new abstraction without an ADR.** Architectural decisions need approval.
- **Mocking what the step asked you to integrate.** If the step says "wire to the real X," wire to the real X.
- **Writing the test after the code (for CODE tasks).** Defeats TDD's design pressure.
- **Editing the step file to make the implementation look correct.** That's drift. Fix the implementation, or open a new step if scope was wrong.
- **Creating a summary doc to explain what you did.** The checklist tick, commit message, and (if relevant) ADR are the record.
- **Bundling unrelated changes into one commit.** One step, one logical change set. Many small commits inside a step are fine.
