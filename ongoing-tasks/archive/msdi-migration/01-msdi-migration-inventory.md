# 01 — MS.DI Migration — Inventory MEF usages

## Goal

Produce a complete catalogue of every `[Export]`, `[Import]`, `[ImportMany]`, and `[Shared]` usage in the C# projects, plus the two known MEF entry points (`AddMSSQL()`, `AddExecutor()`). Without this list, later steps can miss a registration and break wiring at runtime.

## Track

dotnet (planning)

## What Exists

- Two known MEF-attributed source files (per `grep [Export]` during bootstrap): `ParameterizationExtractor/FromFileExecutor.cs`, `ParameterizationExtractor/DSLExecutor.cs`. Likely more — this step proves the full extent.
- `AppBootstrap` extension methods (`AddMSSQL`, `AddExecutor`) — the registration entry points.
- `System.Composition.AttributedModel`, `System.Composition.Runtime` PackageReferences in `ParameterizationExtractor.csproj`.

## What to Build

- A markdown table appended to the checklist's `## Notes`, columns:
  - File:line
  - Attribute (`Export` / `Import` / `ImportMany` / `Shared` / other)
  - Type / contract being exported or imported
  - Lifetime if discernible (`[Shared]` = singleton, otherwise transient by default in MEF)
  - Notes (interface vs concrete, generic constraints, named exports)
- A short prose summary: how many distinct contracts are exported, how many are many-of-T (`[ImportMany]`), whether any export uses MEF's contract-name feature (`[Export("name")]`), whether any uses `[ExportMetadata]`.
- A list of every assembly currently scanned by the MEF container (the `AppBootstrap` extension methods name them). This list becomes the input to step 02.

## Acceptance Criteria

- [ ] Every `[Export]` / `[Import]` / `[ImportMany]` / `[Shared]` in the C# projects is in the table.
- [ ] No occurrence is summarised as "and others" — exhaustive list, not sample.
- [ ] Summary names the registration shape per contract (one-of, many-of, named, metadata).
- [ ] Reviewer (human) confirms the inventory before step 02 starts.

## References

- Related ADRs: `001-mef-di-container.md` (the decision being retired).
- Related methodology: `docs/methodology/dotnet-cli.md` § Composition.
- Depends on: `net10-upgrade-checklist.md` (run inventory against the modernised build).
