# MEF → Microsoft.Extensions.DependencyInjection Migration

## Goal

Replace `System.Composition` (MEF) as the application DI container with `Microsoft.Extensions.DependencyInjection`. After this feature: `[Export]` / `[Import]` / `[ImportMany]` are gone; composition is via `IServiceCollection` extension methods registered in `Program.cs`. ADR-001 retires; the MEF tripwire and recipe section retire with it.

## Scope

- **In scope:**
  - Inventory every MEF attribute usage in the C# projects.
  - Translate each `[Export]` to a `services.AddX(...)` registration.
  - Translate each constructor `[Import]` / `[ImportMany]` to plain DI parameters.
  - Replace the `AppBootstrap` composition root with a `HostBuilder` / `ServiceCollection`-based equivalent.
  - Remove `System.Composition.AttributedModel` and `System.Composition.Runtime` PackageReferences from the CLI csproj.
  - Update tripwires in `CLAUDE.md` (mirror to `.github/copilot-instructions.md`) and `docs/methodology/dotnet-cli.md` to remove the MEF-specific guidance.
  - Update `adr/001-mef-di-container.md` status to `Superseded`. Create `adr/006-msdi-container.md` capturing the new decision.
  - Update `docs/architecture/overview.md` § 5 (Cross-cutting concerns → DI container) to reflect the change.
- **Out of scope:**
  - Any change to extraction/SQL-emission behaviour. Pinned by characterisation suite.
  - F# code (frozen, ADR-005). The DSL Connector consumes Logic types — only its registration changes.
  - Replacement of `FluentCommandLineParser` or other non-DI packages.
- **Dependencies:**
  - `characterisation-tests` (must stay green throughout).
  - `net10-upgrade` (run on the modern Microsoft.Extensions.DependencyInjection surface from the start; do not migrate against 3.1.0).

## Architecture

See [feature-architecture.md](./msdi-migration/feature-architecture.md) for the inventory approach, the proposed `IServiceCollection` extension shape (requires approval before step 03), and the retirement of ADR-001.

## Steps

- [ ] [01 — inventory MEF usages](./msdi-migration/01-msdi-migration-inventory.md)
- [ ] [02 — propose IServiceCollection extension shape](./msdi-migration/02-msdi-migration-design.md)
- [ ] [03 — convert exports to MS.DI registrations](./msdi-migration/03-msdi-migration-exports.md)
- [ ] [04 — convert imports to constructor injection](./msdi-migration/04-msdi-migration-imports.md)
- [ ] [05 — replace AppBootstrap with HostBuilder; remove MEF packages](./msdi-migration/05-msdi-migration-composition-root.md)
- [ ] [06 — retire ADR-001, MEF tripwires, recipe sections; add ADR-006](./msdi-migration/06-msdi-migration-retire-mef-docs.md)

## Notes
