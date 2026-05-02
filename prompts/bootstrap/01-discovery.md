---
description: "Bootstrap phase 1 — read-only scan, fills factual placeholders only."
arguments:
  - name: mode
    description: brownfield | greenfield
    required: true
---

# Bootstrap — Phase 1: Discovery (read-only)

> Reference this file to start a fresh bootstrap. Pass the project mode:
>
> ```
> #file:prompts/bootstrap/01-discovery.md   mode=brownfield
> ```
>
> (or `mode=greenfield`)

## Arguments

- **mode** — `brownfield` if the host project already has source code, build files, tests; `greenfield` if the repo is empty or nearly empty.

## When to use

The first prompt to run after dropping the template into a new (or existing) project, while `<<PLACEHOLDERS>>` are still present.

---

You are running BOOTSTRAP PHASE 1 — DISCOVERY in the `mode` the user passed above.

## Hard rules

- Do not write or edit ANY file in the repo. Read-only.
- Do not invent ADRs, checklists, tripwires, or architecture content.
- Do not propose values for placeholders that require human judgement (tripwires, architecture topology, project rationale). Defer those to Phase 2 — Interview.
- Stop after producing the proposal. Wait for human approval.

## In-scope placeholders (factual only)

| Placeholder | Source |
|-------------|--------|
| `<<PROJECT_NAME>>` | `package.json`, `*.csproj`, `pyproject.toml` |
| `<<TOP_LEVEL_REPO_MAP>>` | top-level directory listing |
| `<<STACK_N>>` | manifest files present |
| `<<TEST_FRAMEWORK_N>>` | test deps in manifests |
| `<<COMMAND_N>>` | `package.json` scripts, Makefile, csproj targets, pyproject scripts |
| `<<PATH_N>>` | top-level deployable directories |
| `<<PURPOSE_N>>` | only if a directory's `readme.md` states it in one line; otherwise UNKNOWN |

## Out of scope (defer to Interview)

- Tripwires for any stack
- `docs/architecture/overview.md` content beyond a directory listing
- Which recipe stubs to keep, fill, or delete
- Project description / rationale
- `.ignix/review-instructions.md` content

## Procedure

1. Read `prompts/readme.md`, `template-readme.md`, and `ai-workflow.md` so you know the target shape.
2. If `mode=brownfield`: walk the repo. Read manifest files (`package.json`, `pyproject.toml`, `*.csproj`, `*.sln`, `go.mod`, `Cargo.toml`, `Dockerfile`, `.github/workflows/`, `Makefile`). Do not read source code beyond entry points (`main.*`, `Program.cs`, `app.py`, `index.ts`, etc.).
3. List EVERY file in the template that contains `<<PLACEHOLDER>>` markers — use grep, not memory.
4. For each placeholder in scope, classify:
   - **CONFIDENT** — value derivable from one or more files; cite `file:line`.
   - **UNCERTAIN** — guess plus alternatives considered.
   - **UNKNOWN** — cannot infer; defer to Interview.
5. For each placeholder NOT in scope, list it as DEFERRED-TO-INTERVIEW with no proposed value.

## Output

Print a single document titled "Bootstrap Discovery Proposal" with this structure:

```
## Detected stacks
| Stack | Detected from | Build cmd | Test cmd | Test framework |

## Factual placeholders (CONFIDENT)
| File | Placeholder | Proposed value | Evidence (file:line) |

## Factual placeholders (UNCERTAIN)
| File | Placeholder | Best guess | Alternatives | Why uncertain |

## Factual placeholders (UNKNOWN)
| File | Placeholder | Why we cannot infer |

## Deferred to Interview phase
| File | Placeholder | Reason |

## Recipe stubs found
| File | Stack signal present in repo? | Recommendation (informational only — Interview decides) |

## Open questions for the human (factual only)
- ...
```

End with this exact line:

> DISCOVERY COMPLETE. Awaiting human review before proceeding to Phase 2 — Interview.

---

## After running it

1. Review the proposal. Strike or amend any UNCERTAIN values.
2. Answer the UNKNOWN questions inline.
3. Save the approved version to `ongoing-tasks/_bootstrap/discovery.md` (create the folder).
4. Reference [`02-interview.md`](02-interview.md) with the same `mode`.

## Why this phase exists

The original single-prompt bootstrap let the AI hallucinate tripwires, ADRs, and architecture from a code scan. Discovery is now the *narrow* phase: only placeholders the AI cannot reasonably get wrong. Everything else is a human decision, gathered in Phase 2.
