# 04 — MS.DI Migration — Convert imports

## Goal

Replace every `[Import]` / `[ImportMany]` constructor parameter with plain DI constructor injection. After this step, components resolve from the MS.DI container; MEF-side resolution is no longer needed at runtime. MEF packages are still referenced — removed in step 05.

## Track

dotnet (refactor)

## What Exists

- Inventory from step 01.
- Step-03 MS.DI registrations live and resolvable.

## What to Build

- For each `[Import]` parameter: remove the attribute. The constructor parameter remains.
- For each `[ImportMany]`: replace the attribute with a plain `IEnumerable<T>` (or array) constructor parameter — MS.DI resolves multi-registrations into `IEnumerable<T>` natively.
- For any non-trivial MEF feature surfaced in step 01 (named exports, metadata): translate using `IServiceProvider` keyed services or a small dispatch interface. *If a non-trivial translation appears, stop and bring the case back for approval before continuing.*
- Switch `Program.cs` to resolve from the MS.DI service provider instead of the MEF container.

## Acceptance Criteria

- [ ] No `[Import]` or `[ImportMany]` attribute remains in the C# source.
- [ ] `Program.cs` resolves the top-level executor from the MS.DI provider only.
- [ ] `dotnet build "SQL Buldozer.sln"` succeeds.
- [ ] Characterisation suite green — same SQL output as before migration, byte-equal to the goldens.
- [ ] Any MEF feature that did not have a clean MS.DI translation is recorded as an open item in `## Notes` with the chosen workaround.

## References

- Related ADRs: `001-mef-di-container.md`, `006-msdi-container.md`.
- Related methodology: `docs/methodology/dotnet-cli.md`.
- Depends on: `03-msdi-migration-exports.md`.
