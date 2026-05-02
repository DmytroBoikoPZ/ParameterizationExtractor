# 006 — DI container is `Microsoft.Extensions.DependencyInjection`

## Status

Accepted

## Context

The bootstrap-phase [`001-mef-di-container.md`](./001-mef-di-container.md) recorded "DI container is `System.Composition` (MEF)" as a binding decision, but never matched reality. Step 01 of the `msdi-migration` feature (2026-05-02) ran an exhaustive inventory:

- **Zero** `using System.Composition` directives across the entire `.cs` source tree.
- **Zero** active `[Export]` / `[Import]` / `[ImportMany]` / `[Shared]` / `[ExportMetadata]` attributes. (Two `[Export]` lines existed as `//[Export...]` comment-outs in `FromFileExecutor.cs:13` and `DSLExecutor.cs:16`; both were removed in step 02 of `msdi-migration`.)
- The active DI container is `Microsoft.Extensions.DependencyInjection`, accessed through a thin custom wrapper: `ParameterizationExtractor/Common/SqlBuldozerApp.cs:64-141` (`AppBuilder`) constructs `new ServiceCollection()`, accepts registrations through a `ConfigureServices(Action<IServiceCollection>)` shape, and produces an `IServiceProvider` via `BuildServiceProvider()`.
- `AppBootstrap.AddMSSQL()` and `AppBootstrap.AddExecutor()` register parts via `services.AddSingleton<IFoo, Foo>()` / `AddTransient<...>()` — standard MS.DI extension methods.
- Constructor injection is the only injection style. Parts receive their dependencies as constructor parameters resolved by MS.DI.

The history is informative: the project migrated MEF → MS.DI at some point in its lifetime (~2020-2024 timeframe; exact date unknown — predates this session) and left the `System.Composition.AttributedModel` / `System.Composition.Runtime` package references hanging plus the two commented-out `[Export]` lines as residue. The `msdi-migration` feature's step 02 cleaned all of that up.

This ADR documents the actual state as binding policy.

## Decision

We use **`Microsoft.Extensions.DependencyInjection`** as the application's DI container.

- **Composition root:** `AppBootstrap.CreateAppBuilder(args)` (in `ParameterizationExtractor/AppBootstrap.cs`) returns an `IAppBuilder` (a thin custom wrapper over `IServiceCollection`). `Program.Main` chains `.AddMSSQL()` and `.AddExecutor()` to register engine and executor parts, then calls `.Build()` which delegates to `ServiceCollection.BuildServiceProvider()` and resolves `IApp`.
- **Registration:** module-level `IAppBuilder` extension methods (`AddMSSQL`, `AddExecutor`) call `services.AddSingleton<...>()` / `AddTransient<...>()` against the underlying `IServiceCollection`. New module-level extension methods follow this shape.
- **Injection:** **constructor injection only**. `[Export]` / `[Import]` / `[ImportMany]` are forbidden in production code (and aren't permitted by reference any longer — the `System.Composition.*` packages are gone).

Alternatives considered and rejected:

- **Reverting to MEF (`System.Composition`):** considered only because ADR-001 had recorded it as the policy. Rejected — the code never matched that policy. There is no operational benefit to MEF for a single-binary CLI with a static dependency graph; MEF's strengths (assembly-scan plug-in discovery) are unused here.
- **Generic Host (`Host.CreateApplicationBuilder`):** plausible long-term direction, but currently the custom `AppBuilder` works and has no friction. Switching to Generic Host would be a separate, opt-in refactor; not imposed by this ADR.

## Consequences

- **Easier:** docs describe what the code actually does. Contributors don't get confused by a tripwire that says "use MEF" while every concrete registration uses `services.Add...`. New module wiring is the boring/standard `IServiceCollection` shape every .NET dev already knows.
- **Harder:** none. Removing the wrong policy doesn't lose any capability — there was no MEF behaviour to lose.
- **Open follow-ups:**
  - The other bootstrap-era ADRs (002 — module layering, 003 — F#/FParsec DSL, 004 — T4 SQL generation, 005 — F# DSL freeze) should be audited against actual code before being treated as binding. ADR-001's failure mode (encoded state that didn't match reality) might recur in any of them.
  - If/when the desktop UI feature (mentioned in ADR-005) needs a different composition pattern (e.g., a UI host or per-window scope), revisit whether the custom `AppBuilder` should migrate to `Host.CreateApplicationBuilder`. Not in scope here.