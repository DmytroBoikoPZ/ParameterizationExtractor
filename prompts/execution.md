---
description: "Execute the next unticked step in a checklist. TDD for CODE, pragmatic for the rest."
---

# Execution Prompt

**Purpose:** Guides AI agent execution of work in `ongoing-tasks/`. Apply TDD where it adds value, pragmatic verification elsewhere.

For repo orientation, read `prompts/session-start.md` first.
For stack rules and tripwires, see `CLAUDE.md` (or `.github/copilot-instructions.md`).
For file layout and templates, see `docs/methodology/task-workflow.md`.
For the per-stack feature recipe, see `docs/methodology/feature-development.md`.

---

## Test Frameworks

| Stack | Test Framework | Build / Test Command |
|-------|----------------|----------------------|
| .NET (C#) | NUnit | `dotnet test "SQL Buldozer.sln"` |
| F# | NUnit (shared `Tests` project) | `dotnet test "SQL Buldozer.sln"` |

---

## Task Classification

Classify each checklist item before starting it:

| Type | When | Approach |
|------|------|----------|
| **CODE** | Business logic, handlers, services, plugins, API endpoints | **TDD**: RED → GREEN → REFACTOR |
| **CONTRACT** | Interfaces, records, enums, DTOs, event contracts | Write type + serialization test. No RED phase needed for pure data types. |
| **CONFIG** | DI wiring, config files, middleware pipeline, build manifests | Implement → verify build compiles → integration test if behavior changes. |
| **INFRA** | Container configs, deployment manifests, infra-as-code | Implement → deploy to a sandbox → verify manually. |
| **CLEANUP** | File deletion, renames, moving code, removing dead code | Ensure existing tests still pass. Write new test only if a coverage gap is found. |
| **UI** | Components, styles, design tokens, layouts | Implement → visual verify → component test for interaction logic. |

**Rule:** TDD (RED → GREEN → REFACTOR) is mandatory for CODE tasks. For other types, apply the approach listed above.

---

## Execution Loop

```
1. Open the checklist: ongoing-tasks/{feature}-checklist.md
2. Read ongoing-tasks/{feature}/feature-architecture.md for the big-picture context
3. Find first unchecked [ ] step
4. Open the linked step file under ongoing-tasks/{feature}/NN-*.md
5. Classify task type (CODE / CONTRACT / CONFIG / INFRA / CLEANUP / UI)
6. Execute using the appropriate approach
7. Mark [x] in checklist; log decisions/blockers under the checklist's ## Notes
8. Report completion briefly
9. Repeat
```

Step files are specifications — do not edit their scope while executing. If scope changes, add a new step or open an ADR.

### For CODE tasks — TDD cycle

```
RED:      Write failing test
GREEN:    Write minimal code to pass
REFACTOR: Clean up while tests stay green
```

**Test naming** — match the host project's existing convention. Common patterns:
- `MethodName_Scenario_ExpectedResult` (e.g., C# / xUnit)
- `should [behavior] when [condition]` (e.g., Vitest / Jest)
- `test_{behavior}_when_{condition}` (e.g., pytest)

**Test structure** (Arrange / Act / Assert):
```
// Arrange — set up test data and mocks
// Act — call the method under test
// Assert — verify expected outcome
```

### Step dependencies

If a step lists `Depends on:` in its References section, verify those steps are `[x]` in the checklist before starting. Otherwise assume checklist order.

---

## Boundaries

**DO:**
- Follow the step file exactly — file paths, type/method names, acceptance criteria as documented.
- Write one test at a time, complete the cycle, then next test.
- Tick the checklist after each completed step.
- Log blockers in the checklist's `## Notes` (dated).
- Ask before: creating new abstractions not in the step, changing existing APIs, bulk migrations.

**DO NOT:**
- Write implementation before a failing test (for CODE tasks).
- Create components / features not listed in the step.
- Refactor or optimize beyond step scope.
- Edit a step file's scope while executing it.
- Change build configurations without explicit need.
- Add git commits per test phase (commit naturally at step boundaries).
- Create summary markdown files to document what you did — the checklist + commit history + ADRs are the record.

---

## Reporting

After each completed task:
```
✅ [Task name from checklist]
   Tests: [count written] | Files: [list] | Next: [next task name]
```

After a TDD cycle on CODE tasks, include phase summary:
```
✅ {Component} — {behavior}
   🔴 N tests written → build failed (as expected)
   🟢 {Component} implemented → N/N pass
   🔵 Extracted {detail} into private helper
   Tests: N new (M total) | Files: {list}
   Next: {next task name}
```

For non-CODE tasks, keep it short:
```
✅ {Task name}
   Updated: {files} | Next: {next task name}
```

### When issues arise
```
⚠️ [Task name] — [problem description]
   🐛 [what went wrong]
   💡 [proposed fix or question]
   ⏸️ Waiting for guidance
```

---

## Completion

When every top-level item in the checklist is `[x]`:
1. Run the full test suite for affected components.
2. Verify each step's Acceptance Criteria is satisfied.
3. Report final summary: total tests, files changed, any deferred items.
4. Wait for approval before moving the pair into `ongoing-tasks/archive/`.
