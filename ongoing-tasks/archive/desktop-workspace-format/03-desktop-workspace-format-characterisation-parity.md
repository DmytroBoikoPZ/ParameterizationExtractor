# 03 — desktop-workspace-format — Characterisation parity (XML ≡ JSON goldens)

## Goal

Prove behavioural parity between the existing XML path and the new JSON path. For each of the 5 pinned characterisation scenarios, express the same package as JSON; assert the engine's emitted SQL is **byte-equal** to the existing golden (which was captured against the XML path).

This step is the strongest evidence that "engine takes both XML + JSON, indefinitely" ([ADR-007](../../adr/007-desktop-wpf-stack.md), L10) is real and not aspirational.

## Track

`backend` (.NET / C# — characterisation harness extension).

## What Exists

- After step 02: `JsonPackageReader`, `JsonGlobalConfigReader`, `ConfigSerializer` extension dispatch — green.
- [`Tests/CharacterisationTests/`](../../Tests/CharacterisationTests/) folder with the 5 pinned scenarios:
  - `only-one-table-employee`
  - `only-parent-from-payment`
  - `only-children-from-therapy-program`
  - `fk-dep-therapy-item`
  - `where-filter-during-child-walk`
- [`Tests/CharacterisationTests/Harness/CharacterisationRunner.cs`](../../Tests/CharacterisationTests/Harness/CharacterisationRunner.cs) — harness that builds packages programmatically, runs the engine, normalises output, diffs vs goldens. Pattern exemplar.
- [`Tests/CharacterisationTests/Scenarios.cs`](../../Tests/CharacterisationTests/Scenarios.cs) — `[TestCaseSource]` registry.
- 5 golden `.sql` files under `Tests/CharacterisationTests/Goldens/` (or wherever they sit — confirm during the step).

## What to Build

### Per-scenario JSON expression

For each of the 5 scenarios, produce a JSON file in `Tests/CharacterisationTests/Json/` that expresses the same `Package` as the existing programmatic build. Naming: `{scenario-name}.json`.

These files are **fixtures**, not goldens — they define the input. The output goldens are the same `.sql` files already pinned for the XML path.

### Harness extension — parametric (locked)

Extend `CharacterisationRunner` (and/or its `[TestCaseSource]` registry) with a **`PackageLoader`** dimension:

```csharp
internal enum PackageLoader
{
    Programmatic,   // existing — builds the Package by hand, in C#
    Json            // new — loads from Tests/CharacterisationTests/Json/{scenario}.json via JsonPackageReader
}
```

Tests are parameterised on `(scenario, loader)`. The runner picks the loader at the start, builds the `Package`, then runs the **identical** downstream pipeline (engine invocation, normaliser, golden diff). One assertion path, two ways to construct the input.

`Scenarios.cs` (or equivalent) yields a Cartesian product: `5 scenarios × 2 loaders = 10` characterisation cases. Test names should make the dimension visible — e.g. `Characterisation(only-one-table-employee, Programmatic)` and `Characterisation(only-one-table-employee, Json)`.

If a programmatic build and a JSON fixture for the same scenario diverge, both rows show in the test report; if they converge they pass identically — the parity claim is observable in the suite at all times.

> Rationale (locked 2026-05-02): Option B picked over a parallel `JsonCharacterisationRunner` to keep one assertion path. The single-runner shape means there is no risk of XML and JSON paths drifting in their normalisation, golden lookup, or error reporting — they share the code that does all three.

### Tests

- Existing 5 XML/programmatic test cases reframed inside the new parametric structure (still 5 cases, same goldens).
- 5 new JSON cases — same 5 goldens, new `Json/{scenario}.json` fixtures, loader = `PackageLoader.Json`.
- Total characterisation count after this step: **10 cases over 5 goldens** (5 × Programmatic + 5 × Json).

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — characterisation count now **5 + 5 = 10** (Programmatic + Json over the 5 scenarios). Pre-existing non-characterisation tests still pass; new total is `existing - 5 (old characterisation) + 10 (new parametric characterisation)` = +5 net.
- Goldens unchanged. JSON path output passes the same normaliser (`__GENERATED_TIMESTAMP__` masking etc.) and is byte-equal.

## Acceptance Criteria

- [ ] `PackageLoader` enum (`Programmatic`, `Json`) exists in the harness; `CharacterisationRunner` selects between them at the start of the run and shares the rest of the pipeline.
- [ ] `Scenarios.cs` (or equivalent registry) yields a Cartesian product `(scenario × loader)` so each scenario is exercised through both loaders. Test names include the loader so failures are diagnosable at-a-glance.
- [ ] One JSON fixture per characterisation scenario lives under `Tests/CharacterisationTests/Json/`; each is valid JSON parseable by `JsonPackageReader` from step 02.
- [ ] Each JSON fixture is a faithful translation of the corresponding programmatic `Package` build — comparable in shape, with no engine-side modifications to make the fixture pass.
- [ ] All 10 cases (5 scenarios × 2 loaders) pass with **byte-equal** golden output. Both loaders share one normaliser, one diff path, one error reporter.
- [ ] No golden file modified.
- [ ] No engine code changed in this step. (Engine code touched only in step 02. If a scenario reveals a JSON-path bug, the fix lands in step 02 and step 03 re-runs.)
- [ ] `dotnet build` 0 errors. `dotnet test` all green.

## References

- ADR: [`adr/009-workspace-format.md`](../../adr/009-workspace-format.md)
- Methodology: [`docs/methodology/workspace-format.md`](../../docs/methodology/workspace-format.md)
- Existing harness: [`Tests/CharacterisationTests/`](../../Tests/CharacterisationTests/)
- Depends on: steps 01 and 02 must be `[x]` first.
