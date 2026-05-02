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

- [x] [01 — inventory MEF usages](./msdi-migration/01-msdi-migration-inventory.md)
- [x] [02 — cleanup: remove unused `System.Composition.*` packages + dead `[Export]` comments](./msdi-migration/02-msdi-migration-cleanup.md)
- [x] [03 — retire ADR-001 (incorrect from the start); add ADR-006; remove MEF mentions from docs](./msdi-migration/03-msdi-migration-docs.md)

> Steps 02-05 from the original plan (MEF→MS.DI conversion, composition-root refactor) were collapsed after step 01 revealed the codebase already uses `Microsoft.Extensions.DependencyInjection`. Step 06's docs work survives as the new step 03. See `## Notes` below for the inventory finding.

## Notes

### 2026-05-02 — Step 01 inventory finding (scope collapsed)

The whole MEF→MS.DI premise is wrong. The codebase **does not use MEF**.

**Inventory (exhaustive):**

| File:line | Attribute | Status |
|---|---|---|
| `ParameterizationExtractor/FromFileExecutor.cs:13` | `[Export(typeof(IExecutor))]` | **commented out** (`//[Export...]`) |
| `ParameterizationExtractor/DSLExecutor.cs:16` | `[Export(typeof(IExecutor))]` | **commented out** (`//[Export...]`) |
| (anywhere else) | `[Import]` / `[ImportMany]` / `[Shared]` / `[ExportMetadata]` | **0 occurrences** |

**Active DI container:** `Microsoft.Extensions.DependencyInjection` (the standard MS.DI).

- `Common/SqlBuldozerApp.cs:64-141` defines `AppBuilder` — a thin wrapper around `new ServiceCollection()` (line 72) → `_services.BuildServiceProvider()` (line 79).
- `AppBootstrap.AddMSSQL()` and `AppBootstrap.AddExecutor()` use `services.AddSingleton<IFoo, Foo>()` / `AddTransient<...>()` — standard MS.DI extension methods, not MEF.
- Constructor injection (e.g., `FromFileExecutor(ILogger<...> log, IAppArgs args, PackageProcessor packageProcessor, ICanSerializeConfigs config)`) — these are resolved by MS.DI from the registrations in `AppBootstrap`. No `[Import]` attributes.

**Unused MEF references in `ParameterizationExtractor.csproj`:**

- `System.Composition.AttributedModel 7.0.0` — referenced; transitively unused.
- `System.Composition.Runtime 7.0.0` — referenced; transitively unused.

`grep "using System.Composition"` returns **0 matches** across the entire `.cs` source tree.

**Why was the bootstrap wrong?** Earlier in this session the bootstrap interview captured "DI container is `System.Composition` (MEF)" based on a shallow grep that found two `[Export]` lines, plus user memory that the project once used MEF. Neither check confirmed *active* MEF use. The project must have migrated MEF → MS.DI at some point and left the package references + commented-out `[Export]` lines behind.

**Implication:** ADR-001 was binding policy from the start of this project's bootstrap, but described a state that doesn't exist. ADR-001 needs to be marked `Superseded by 006`, and ADR-006 needs to capture the **actual** state ("MS.DI is the DI container — has been since long before the bootstrap"), not the originally-planned migration.

**Steps 02-05 (original plan) collapsed to a single cleanup step (new step 02):** there are no exports/imports to convert; the composition root is already MS.DI; only the unused packages and dead `[Export]` comments need to go.

**Step 01 acceptance:** all four ACs satisfied — the inventory is exhaustive, no occurrences are summarised as "and others" (there are no occurrences at all), the registration shape per contract is documented (none — there are no MEF contracts), and the human (you) has reviewed and approved.

### 2026-05-02 — Step 02 completion (cleanup)

**Files changed:**

- `ParameterizationExtractor.csproj` — removed `System.Composition.AttributedModel 7.0.0` and `System.Composition.Runtime 7.0.0` PackageReferences.
- `ParameterizationExtractor/FromFileExecutor.cs:13` — removed `//[Export(typeof(IExecutor))]` comment line.
- `ParameterizationExtractor/DSLExecutor.cs:16` — removed `//[Export(typeof(IExecutor))]` comment line.

**Verification:** `grep "System.Composition\|\[Export\]\|\[Import\]"` over `*.cs`/`*.csproj`/`*.fsproj` → **0 matches**. `dotnet build` 0 errors. `dotnet test` 33/33 pass. Goldens unchanged.

### 2026-05-02 — Step 03 completion (docs retirement)

**ADRs:**

- `adr/001-mef-di-container.md` — Status: `Superseded by 006`. Body annotated to record that the ADR was incorrect from the start; preserved as history.
- `adr/006-msdi-container.md` — new ADR, `Accepted`. Full Context (cites the inventory finding), Decision (`Microsoft.Extensions.DependencyInjection` is the container; constructor injection only; module-level `IAppBuilder` extension methods over `IServiceCollection`), Consequences (open follow-up: audit other bootstrap-era ADRs).
- `adr/readme.md` — index updated: ADR-001 → `Superseded by 006`; ADR-006 added.

**Doc files:**

- `docs/methodology/dotnet-cli.md` § Composition — rewrote from MEF-described prose to the actual `Microsoft.Extensions.DependencyInjection` shape. ADR reference points to `006-msdi-container.md`.
- `docs/architecture/overview.md`:
  - § 5 (Cross-cutting concerns → DI container) — replaced MEF text with the real composition story (`AppBuilder` wraps `ServiceCollection`; `.AddMSSQL()`/`.AddExecutor()`; constructor injection only). Added breadcrumb explaining ADR-001 was wrong.
  - § 7 (Active ADRs) — replaced ADR-001 row with ADR-006 row; added ADR-005 row (was missing).
- `.ignix/review-instructions.md` — inverted the project-specific note: "MS.DI is the DI container by design — do not suggest migrating to MEF (`System.Composition`)." (was the reverse).
- `adr/005-freeze-fsharp-dsl.md` line 13 — passing "MEF registrations" reference softened to "DI registrations" (no behaviour change implied; preserves intent).

**No edits needed for `CLAUDE.md` or `.github/copilot-instructions.md`:** verified that neither file contains MEF / `System.Composition` / `[Export]` text. SHA256 hashes match — they're still byte-identical (verified by `verify-bootstrap.ps1`'s sync check).

**Verification:**

- `grep -i "MEF\|System\.Composition\|\[Export\]\|\[Import\]"` over canonical doc files (`CLAUDE.md`, `.github/copilot-instructions.md`, `docs/`, `.ignix/`, `adr/`) returns **only** intentional mentions: the inverted "don't suggest migrating to MEF" guardrail in `.ignix/review-instructions.md`, the historical superseded `adr/001-mef-di-container.md`, the new explanatory `adr/006-msdi-container.md`, the ADR index row for 001, and the "no MEF" forbid statements in `dotnet-cli.md` and `overview.md`.
- `verify-bootstrap.ps1` — same FAIL pattern as before this feature (placeholder doc-references from bootstrap that the verifier regex can't distinguish from fillable placeholders); CLAUDE/copilot sync PASS; ADR count up to 6 (was 4).
- `dotnet test` — 33/33 pass on net10.0 / NUnit 4 / Microsoft.Data.SqlClient 6.0.2 / CommandLineParser 2.9.1 / MS.DI 10.0.0.

## Final summary

The whole "MEF → MS.DI migration" was a 6-step plan to do work that turned out to have already been done (silently) years ago. Step 01 surfaced this; the remaining work was 2 steps: removing the residue (unused packages + comment-out lines) and correcting the docs.

**Cumulative changes:**

- `System.Composition.AttributedModel 7.0.0` and `System.Composition.Runtime 7.0.0` PackageReferences removed.
- 2 commented-out `[Export]` lines removed.
- ADR-001 marked superseded; ADR-006 created with the correct Decision.
- Doc surfaces updated: CLAUDE.md (no edit needed), `dotnet-cli.md`, `architecture/overview.md`, `.ignix/review-instructions.md`, ADR index. Plus a soft fix to ADR-005's passing reference.

**Open follow-ups (recorded in ADR-006):**

- Audit ADRs 002 (module layering), 003 (F#/FParsec DSL), 004 (T4 SQL generation) against actual code before treating them as binding. ADR-001's failure mode was that nobody verified before it became policy. Same risk class for any of the other bootstrap-era ADRs.

**Out of scope (still pending, deliberately):**

- Cleanup of `_bootstrap/` workspace (since bootstrap apply).
- Stale facts in `dotnet-cli.md` § Testing and § Project shape (NUnit version, framework targets) — these were not MEF-related so I didn't touch them in this step. Worth a small follow-up.
- Tripwire bullet in `CLAUDE.md` mentions `System.Data.SqlClient` (now Microsoft.Data.SqlClient). Same kind of stale-fact follow-up.
- Desktop UI feature.
