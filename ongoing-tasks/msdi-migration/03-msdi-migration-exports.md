# 03 — MS.DI Migration — Convert exports

## Goal

Replace every `[Export]` attribute with the equivalent `IServiceCollection` registration in the extension methods designed in step 02. Build still works because consumers still use MEF imports — those are step 04. This step delivers a build that has both DI styles co-existing.

## Track

dotnet (refactor)

## What Exists

- Inventory from step 01.
- Approved extension-method design from step 02.
- Today's `AppBootstrap.AddMSSQL()` and `AddExecutor()` MEF-flavoured methods.

## What to Build

- New `IServiceCollection` extension methods (named per step 02), each adding the MS.DI registrations for the parts it owns.
- The MEF `[Export]` attributes can be left in place during this step — they are dead-code once step 04 lands but harmless. Easier to delete in one sweep at the end.
- Wire the new extension methods into `Program.cs` *alongside* the existing MEF bootstrap. Both DI containers produce the same component graph until step 04 finishes.
- For now, prefer registering against the same interfaces MEF exported.

## Acceptance Criteria

- [ ] Every export from the step-01 inventory has a matching `services.AddX<>()` line in the new extension methods.
- [ ] Lifetimes match the proposal in step 02.
- [ ] `dotnet build "SQL Buldozer.sln"` succeeds.
- [ ] Characterisation suite still green (the runtime still uses MEF resolution at this point — MS.DI runs in parallel but isn't consumed yet).

## References

- Related ADRs: `001-mef-di-container.md` (retiring), `006-msdi-container.md` (in progress).
- Related methodology: `docs/methodology/dotnet-cli.md` § Composition.
- Depends on: `02-msdi-migration-design.md`.
