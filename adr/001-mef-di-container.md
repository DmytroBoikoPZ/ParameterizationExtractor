# 001 — DI container is `System.Composition` (MEF), not `Microsoft.Extensions.DependencyInjection`

## Status

Superseded by 006

## Context

> **This ADR was incorrect from the moment it was authored.** It was scaffolded during the bootstrap phase based on a shallow grep that found two `[Export]` attributes and stale memory that the project once used MEF. Step 01 of the `msdi-migration` feature (2026-05-02) ran an exhaustive inventory and found: zero `using System.Composition` directives, zero active `[Export]`/`[Import]`/`[Shared]`/`[ExportMetadata]` attributes (the two `[Export]` lines were commented-out), and a custom `AppBuilder` (`ParameterizationExtractor/Common/SqlBuldozerApp.cs`) that wraps `Microsoft.Extensions.DependencyInjection.ServiceCollection`. The codebase had migrated MEF → MS.DI long before the bootstrap; only unused PackageReferences and dead `[Export]` comments remained.
>
> Superseded by [`006-msdi-container.md`](./006-msdi-container.md). This file is preserved as history — both for the lesson (verify before encoding state into binding policy) and for the trail back to the audit that uncovered the mistake.

## Decision

(Authored as a stub during the bootstrap. Never carried real content. The intent had been: *"We will use `System.Composition` (MEF) as the DI container."* That intent never matched the code.)

## Consequences

- **Easier:** —
- **Harder:** the binding tripwire derived from this ADR ("don't migrate to MS.DI") was wrong-way-around. It actively encouraged contributors to leave the legitimate MS.DI wiring in place while believing the app was MEF-driven.
- **Open follow-ups:** audit the other bootstrap-era ADRs (002, 003, 004) against actual code before trusting them as binding.
