# 04 — Characterisation Tests — Lock golden outputs

## Goal

Run the harness once per scenario, capture the current engine output, and commit each as a golden file. After this step, *any* future change to the engine output will surface as a test diff.

## Track

cross-cutting (test data)

## What Exists

- Harness from step 03.
- Test DB config from step 02 (real `budzdorov_Core`).
- Scenario list from step 01.

## What to Build

- `Tests/CharacterisationTests/Goldens/{scenario-name}.sql` — one file per scenario, content = current normalised engine output.
- `Tests/CharacterisationTests/Goldens/_README.md` — short note describing how goldens were generated and the protocol for updating them (run the harness in "record" mode, review the diff, commit). Must explicitly note the **fragility caveat**: goldens are pinned against real dev-DB rows; schema or data drift on `budzdorov_Core` will break them. Step 01's seed-WHERE clauses minimise this surface but do not eliminate it.
- A "record vs. assert" mode toggle in the harness — env var or NUnit category — so that regenerating goldens after an intended engine change is not an exercise in copy-paste.

## Acceptance Criteria

- [ ] One committed golden file per scenario from step 01.
- [ ] Each golden is non-empty and ends with a trailing newline.
- [ ] Recording mode regenerates goldens for the active scenario only, not all scenarios.
- [ ] `_README.md` documents the regen workflow in fewer than 20 lines.

## References

- Related ADRs: —
- Related methodology: `docs/methodology/dotnet-cli.md` § Testing.
- Depends on: `03-characterisation-tests-harness.md`.
