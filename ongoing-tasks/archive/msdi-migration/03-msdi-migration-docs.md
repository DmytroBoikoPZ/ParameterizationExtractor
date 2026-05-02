# 03 — MS.DI Migration — Retire ADR-001, add ADR-006, fix docs

## Goal

Make the documentation match reality. ADR-001 was a binding constraint that described state that doesn't exist; replace it with ADR-006 which captures the actual situation. Strip MEF references from `CLAUDE.md`, the .NET recipe, the architecture overview, and the PR review instructions.

## Track

CLEANUP / docs.

## What Exists

- `adr/001-mef-di-container.md` — Status: `Accepted`. Decision currently reads "DI container is `System.Composition` (MEF)…" (incorrect — see step 01).
- `adr/readme.md` — index lists ADR-001 as Accepted.
- `CLAUDE.md` § Stack-Specific Tripwires § .NET / C#:
  - "Module layering is one-way. … `Common` → nothing project-internal." (preserve — true)
  - But the .NET tripwire list is currently MEF-flavoured at multiple bullets and the recipe link points at `dotnet-cli.md`'s MEF-described section.
- `.github/copilot-instructions.md` — byte-identical mirror of `CLAUDE.md`. Must stay in sync.
- `docs/methodology/dotnet-cli.md`:
  - § Composition — describes MEF as the DI container, mentions `[Export]`/`[Import]` etc.
  - References `adr/001-mef-di-container.md`.
- `docs/architecture/overview.md` § 5 (Cross-cutting concerns):
  - "**DI container:** `System.Composition` (MEF). Composition root is `AppBootstrap.CreateAppBuilder(args)` …"
  - Active ADRs list mentions `001-mef-di-container.md`.
- `.ignix/review-instructions.md`:
  - Project-specific note: "`System.Composition` (MEF) is the DI container by design — do not suggest migrating to `Microsoft.Extensions.DependencyInjection`." Preserve the *don't-flag* spirit but invert: "MS.DI is the DI container — do not suggest migrating to MEF."

## What to Build

### ADRs

- Update `adr/001-mef-di-container.md`:
  - Status: `Superseded by 006`.
  - Add a short note at the top of Context: "This ADR was authored during the bootstrap phase and was incorrect from the start — see ADR-006 and `ongoing-tasks/archive/msdi-migration*` for the correction."
  - Leave the rest of the body intact (history matters).
- Create `adr/006-msdi-container.md`:
  - Status: `Accepted`.
  - Title: "DI container is `Microsoft.Extensions.DependencyInjection`".
  - Context: brief — bootstrap mistakenly recorded MEF (ADR-001); step 01 of `msdi-migration` proved zero MEF usage and that the active container is MS.DI via `AppBuilder` wrapping `ServiceCollection`. ADR-006 captures the correction.
  - Decision: `Microsoft.Extensions.DependencyInjection` is the application DI container. Registration via `AppBootstrap.{AddMSSQL,AddExecutor}` extension methods over `IServiceCollection`. Constructor injection is the only injection style. `[Export]` / `[Import]` / `[ImportMany]` are forbidden.
  - Consequences: easier (boring; widely-known pattern; no MEF assembly-scan footprint to worry about); harder (none — there's no plug-in-from-disk story to lose because there never was one); open follow-ups (audit the other bootstrap-era ADRs 002/003/004 against actual code before trusting them).
- Update `adr/readme.md` index:
  - ADR-001 status: `Superseded by 006`.
  - Add ADR-006 row.

### CLAUDE.md (and mirror)

- In the .NET / C# tripwires list:
  - The current MEF-flavoured tripwires (about `[Export]`, MEF DI behaviour) — none should remain. Looking at the current bullet list in `CLAUDE.md`: no individual bullet explicitly mentions MEF (the DI tripwire is implicit via the recipe link). Verify no MEF text remains. Leave the existing layering / SqlClient / T4 tripwires alone.
- Replace the recipe link target if it currently points at a MEF-described `dotnet-cli.md` section — once `dotnet-cli.md` is updated below, the link still goes there.
- Mirror byte-for-byte to `.github/copilot-instructions.md`. Verify SHA256 match (the repo's `scripts/verify-bootstrap.ps1` enforces this).

### `docs/methodology/dotnet-cli.md`

- Replace § Composition (MEF-described) with the real shape:
  - DI container: `Microsoft.Extensions.DependencyInjection`.
  - Composition root: `AppBootstrap.CreateAppBuilder(args)` returns an `IAppBuilder` (custom thin wrapper over `ServiceCollection`); `.AddMSSQL()` / `.AddExecutor()` are the registration extension methods; `BuildServiceProvider()` happens in `AppBuilder.Build()`.
  - Anything advertised to the rest of the app uses constructor injection. No attributes.
- Replace the ADR reference from ADR-001 to ADR-006.

### `docs/architecture/overview.md` § 5

- Replace the "DI container: MEF" bullet with "DI container: `Microsoft.Extensions.DependencyInjection`. Composition root: `AppBootstrap.CreateAppBuilder(args)` → `AppBuilder` (in `Common/SqlBuldozerApp.cs`) wraps `ServiceCollection`; `.AddMSSQL()` and `.AddExecutor()` register parts via `services.AddSingleton<...>()` / `AddTransient<...>()`. Constructor injection only — no attribute-based registration."
- Update § 7 (Active ADRs): replace the ADR-001 row with the ADR-006 row.

### `.ignix/review-instructions.md`

- Replace the MEF defence: "`System.Composition` (MEF) is the DI container by design — do not suggest migrating to `Microsoft.Extensions.DependencyInjection`."
  - With: "`Microsoft.Extensions.DependencyInjection` is the DI container by design — do not suggest migrating to MEF (`System.Composition`)."

## Acceptance Criteria

- [ ] `adr/001-mef-di-container.md` status is `Superseded by 006`; the body has the brief "incorrect from start" note.
- [ ] `adr/006-msdi-container.md` exists with the full Context / Decision / Consequences sections.
- [ ] `adr/readme.md` index lists ADR-006 (`Accepted`) and ADR-001 (`Superseded by 006`).
- [ ] `grep -i "MEF\|System.Composition\|\\[Export\\]\\|\\[Import\\]"` over `CLAUDE.md`, `.github/copilot-instructions.md`, `docs/methodology/dotnet-cli.md`, `docs/architecture/overview.md`, `.ignix/review-instructions.md` returns **only** the inverted "don't suggest migrating to MEF" line in `.ignix/review-instructions.md` (kept as a guardrail) — nothing else.
- [ ] `CLAUDE.md` and `.github/copilot-instructions.md` have the same SHA256 hash.
- [ ] `scripts/verify-bootstrap.ps1` passes (or surfaces only pre-existing failures unrelated to this step).
- [ ] No build / test impact (docs-only step). Run `dotnet test` to confirm no accidental code breakage.

## References

- Related ADRs: `001-mef-di-container.md` (retiring), `006-msdi-container.md` (creating).
- Related methodology: `docs/methodology/dotnet-cli.md` (rewrite of § Composition).
- Depends on: `02-msdi-migration-cleanup.md`.
