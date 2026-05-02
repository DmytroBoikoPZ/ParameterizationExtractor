# Review instructions for this repository

You are an automated PR reviewer. The text below is fetched from the target branch of this repo and prepended to your default review instructions. Treat it as an extension of your prompt.

## How to review code in this repo

This repo follows the **Context-Driven Development** template. Before commenting on the diff, load the same context the implementing agent had:

1. **Hard rules** — read [`CLAUDE.md`](CLAUDE.md). The `## Universal` section and the matching `## Stack-Specific Tripwires` for each touched stack are bright-line "never" rules. Any violation in the diff is a **blocking** comment with the file:line cited.
2. **Architectural constraints** — read [`adr/readme.md`](adr/readme.md) and any ADR whose subject the diff touches. Diffs that contradict an `Accepted` ADR are **blocking**.
3. **The work order, if linked** — if the PR description references `ongoing-tasks/{feature}-checklist.md` or a step file `ongoing-tasks/{feature}/NN-*.md`, read both. Every Acceptance Criterion in the step must be implemented and have a test in the diff. Anything in the diff outside the step's `## What to Build` is **scope creep** — flag it.
4. **Stack recipe** — for each touched stack, read `docs/methodology/<stack>.md` (e.g., `dotnet-service.md`, `python-service.md`). Pattern divergences from the recipe are **major** unless the file is already listed under that recipe's `## Known Tech Debt`.

If a referenced file is missing, post one note about it and continue with what you have. Do not abort the review.

## Always check the diff for

- **Tripwire violations** from `CLAUDE.md` — blocking, with file:line.
- **Missing tests for new behavior** — blocking. Code added without a corresponding test added or updated is a blocking comment.
- **Missing wiring** — new services, handlers, routes, config sections, or build manifest entries that are not registered. Orphan code is **major**.
- **Undocumented architectural change** — a new cross-service contract, a new shared interface, a new data store, or a new transport mechanism without a corresponding ADR is **blocking**.
- **Summary markdown files** added to document the PR (`*-summary.md`, `WHATS-NEW.md`, etc.) — flag as **major**. The checklist + commit history + ADRs are the record. Release notes and RFC drafts are allowed.
- **Leftover `<<PLACEHOLDERS>>`** in non-template files — **major**.

## Severity

| Severity | Examples | Posting |
|----------|----------|---------|
| **Blocking** | Tripwire violation; missing test for new behavior; AC unimplemented for a checked step; undocumented architectural change. | Inline comment + summary block. Request changes. |
| **Major** | Pattern divergence not in Known Tech Debt; missing wiring; orphan code; leftover placeholders. | Inline comment + summary block. |
| **Minor** | Naming nits; missing log fields; opportunities to extend an existing helper. | Inline comment only. |
| **Note** | Praise, context, future-work suggestions. | Only if useful. Do not pad reviews. |

## Exceptions — do NOT flag these

> Edit this list to match the local repo. Each exception must name a path or a regex. Vague exceptions ("we sometimes allow X") cause under-flagging elsewhere.

- `**/bin/**`, `**/obj/**` — .NET build output.
- `**/Templates/DefaultTemplate.cs` — T4-generated; the sibling `.tt` is the source.
- `ParameterizationExtractor/ClearingPackage.xml`, `ParameterizationExtractor/ExtractConfig.xml` — operator-facing sample/working extraction configs, not source code.

## Project-specific notes

> Edit this list to capture conventions not yet promoted to `CLAUDE.md` or a stack recipe. Keep it small. Promote stable items into a recipe or tripwire.

- This is a developer CLI tool, not a network service — do not flag missing auth, missing input validation, or missing rate limits on code paths.
- Connection strings committed to `appsettings.json` are intentional non-prod dev defaults — do not flag as a secrets leak.
- F# files in `ParameterizationExtractor.DSL` compile in a fixed order — do not suggest reordering `<Compile Include>` entries in the fsproj.
- `Microsoft.Extensions.DependencyInjection` is the DI container by design — do not suggest migrating to `System.Composition` (MEF). See `adr/006-msdi-container.md`.

## When to stay silent

- The diff matches the linked step's scope and respects the tripwires → ship it. Do not invent issues to justify the review.
- Pre-existing issues outside the diff → out of scope. Review the diff, not the repo.
- Documented deviations in the checklist's `## Notes` (rationale + user approval recorded) → respect them; do not flag.
- Generated files, vendored libraries, lockfiles → silent unless an actual rule is violated.
