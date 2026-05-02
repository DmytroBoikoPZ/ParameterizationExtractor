# .NET 10 Upgrade

## Goal

Move every project off `net6.0` / `netstandard2.0` to a current .NET 10 target where appropriate, and bump `Microsoft.Extensions.*`, Serilog, and NUnit dependencies to versions compatible with that target. No behavioural change — pinned by the characterisation suite.

## Scope

- **In scope:**
  - `TargetFramework` bumps:
    - `ParameterizationExtractor` (CLI): `net6.0` → `net10.0`
    - `Tests`: `net6.0` → `net10.0`
    - `ParameterizationExtractor.Common`, `Logic`, `DSL.Connector`: evaluate `netstandard2.0` → `net10.0`. Default to bumping unless there's an external consumer that requires netstandard.
    - `ParameterizationExtractor.DSL` (F#): same evaluation; project is frozen (ADR-005) but still has to build.
  - `Microsoft.Extensions.*` 3.1.0 → matching .NET 10 versions.
  - `Serilog` + sinks → current.
  - `NUnit` 3.12 → current major (NUnit 4 if low-friction, otherwise current 3.x).
  - `Microsoft.NET.Test.Sdk` → current.
  - `System.Composition.AttributedModel` / `Runtime` / `System.Configuration.ConfigurationManager` / `System.CodeDom` / `System.Data.SqlClient` — leave for now (`System.Data.SqlClient` may want to migrate to `Microsoft.Data.SqlClient`, but that's a behaviour-affecting change — separate decision).
- **Out of scope:**
  - MEF → MS.DI (separate feature: `msdi-migration`).
  - `System.Data.SqlClient` → `Microsoft.Data.SqlClient` migration.
  - F# DSL feature work (frozen).
  - Any new behaviour or refactor not driven by a breaking-change fix.
- **Dependencies:** `characterisation-tests` must be green before this feature starts. Any test failure during the bump is a regression to investigate, not a golden to update.

## Architecture

See [feature-architecture.md](./net10-upgrade/feature-architecture.md) for the per-project framework matrix and the breaking-change surfaces to watch.

## Steps

- [x] [01 — bump TargetFramework on CLI + Tests](./net10-upgrade/01-net10-upgrade-bump-targets.md)
- [x] [02 — evaluate netstandard2.0 → net10.0 for libraries; bump where chosen](./net10-upgrade/02-net10-upgrade-libraries.md)
- [x] [03 — bump Microsoft.Extensions.* + Serilog packages](./net10-upgrade/03-net10-upgrade-msext-serilog.md)
- [x] [04 — bump NUnit + test SDK](./net10-upgrade/04-net10-upgrade-test-stack.md)
- [x] [05 — fix breaking changes; characterisation suite green](./net10-upgrade/05-net10-upgrade-breaking-changes.md)

## Notes

### 2026-05-02 — Step 01 completion

**Files changed:** `ParameterizationExtractor.csproj` and `Tests.csproj` — `<TargetFramework>net6.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`. Package versions unchanged.

**Build:** clean (0 errors) after one fix. Initial build failed with 2 F# errors:

```
ParserResult.fs(19,6): error FS0058: Nested type definitions are not allowed.
ParserResult.fs(23,6): error FS0058: Nested type definitions are not allowed.
```

Root cause: indentation drift. Lines 19 (`type DslOK`) and 23 (`type CommandOK`) had 5-space indent; the surrounding module-level types (`ParseResult`, `OK`, `Fail`) all use 4 spaces. The 5-space indent placed them inside `OK`'s scope under F#'s offside rule. F# 10's compiler is stricter about this; older F# versions tolerated the ambiguity. Fix: trimmed one space from lines 19-25. Behaviour-preserving — the types are now siblings of `OK` as the original code intended.

This edit is in scope per ADR-005 (*"the existing parser remains functional and is kept buildable through framework / dependency upgrades"*).

**Warnings (informational, not introduced by this step):**

- `NU1903`/`NU1902` for `System.Data.SqlClient` 4.8.0 (high+moderate CVEs) and `Newtonsoft.Json` 9.0.1 (high CVE). Migration to `Microsoft.Data.SqlClient` is explicitly out of scope per the checklist; revisit as a separate decision after this feature finishes.
- `MSB3270` FParsec architecture mismatch — pre-existing.

**Test run on net10.0:** 18/18 passed in 3s. All goldens still match (no behavioural drift from the framework bump or the F# compatibility fix).

### 2026-05-02 — Step 02 proposal (awaiting human approval)

Per the step file, library targets need a decision before any csproj edit: keep `netstandard2.0` or bump to `net10.0`?

**Inventory:**

| Project | Current | External NuGet consumers? |
|---|---|---|
| `ParameterizationExtractor.Common` | netstandard2.0 | None |
| `ParameterizationExtractor.Logic` | netstandard2.0 | None |
| `ParameterizationExtractor.DSL.Connector` | netstandard2.0 | None |
| `ParameterizationExtractor.DSL` (F#) | netstandard2.0 | None |

`packages.config` and the four csproj files were the only places these targets appear; no `<PackageReference>` from outside this solution depends on these libraries. The DSL is frozen (ADR-005) but still builds (now confirmed with the indent fix).

**Recommendation: bump all four to `net10.0`.**

Reasons:

- No external consumer requiring netstandard2.0 — no compatibility loss.
- `Microsoft.Extensions.Logging.Abstractions` 3.1.0 is referenced by all four; step 03 wants to bump it to a current major version (10.x). The 10.x packages are net10/net8/netstandard2.0 multi-targeted, so theoretically netstandard2.0 still works with 10.x packages — but keeping libs on netstandard2.0 means we juggle compatibility shims and lose access to net10-only APIs (e.g., modern System.Text.Json, source generators) for no reason.
- One uniform target across the solution simplifies tooling (e.g., MSBuild output paths, test runners, IDE F5 behaviour).
- Reverting later if a need for netstandard2.0 actually appears is one-line per csproj.

**Counter-argument considered and rejected:** "Keep DSL on netstandard2.0 because it's frozen." Not a good reason — the freeze is on *features*, not on build config. Keeping it on netstandard2.0 would mean the F# project ships under different rules than its three C# siblings, which is the kind of inconsistency that bites later.

**Risk:** the F# compiler may surface another indentation/syntax issue when the netstandard2.0 → net10.0 bump triggers a fuller language-version check. If that happens, the fix is the same shape as the step-01 fix (small, behaviour-preserving). If anything more invasive surfaces, I'll stop and ask.

**Action requested:** approve the bump-all-four plan, and I'll proceed.

### 2026-05-02 — Step 02 completion

Approved by human. Bumped all four library projects `netstandard2.0` → `net10.0`:

- `ParameterizationExtractor.Common.csproj`
- `ParameterizationExtractor.Logic.csproj`
- `ParameterizationExtractor.DSL.Connector.csproj`
- `ParameterizationExtractor.DSL.fsproj`

**Build:** 0 errors. No additional F# breaks beyond the step-01 indent fix.

**Test run on full net10 stack (CLI + 4 libs + Tests):** 18/18 passed in 2s. Goldens still match.

### 2026-05-02 — Step 03 completion

**Microsoft.Extensions.\*** all bumped to **10.0.0** (CLI + 4 libs + Tests — 16 PackageReference entries total).

**Serilog:**

| Package | Was | Now |
|---|---|---|
| `Serilog` | 2.9.0 | 4.2.0 |
| `Serilog.Extensions.Logging` | 3.0.1 | 9.0.0 |
| `Serilog.Settings.Configuration` | 3.1.0 | 9.0.0 |
| `Serilog.Sinks.Console` | 3.1.1 | 6.0.0 |
| `Serilog.Sinks.File` | 4.1.0 | 6.0.0 |

Out of scope per checklist (not bumped this step): `System.Composition.AttributedModel/Runtime`, `System.Configuration.ConfigurationManager`, `System.CodeDom`, `System.Data.SqlClient`, `FluentCommandLineParser.NETStandard`, `docfx.console`. NUnit/Test SDK come in step 04.

**Build:** 0 errors. No source edits required — `IConfiguration` extension surface, `Serilog.Settings.Configuration.ReadFrom.Configuration(IConfiguration)`, and `LoggerConfiguration` are API-stable across these versions (the existing Program.cs lambda still compiles).

**Test run:** 18/18 passed, 3s. Goldens unchanged.

### 2026-05-02 — Step 04 proposal (awaiting human approval)

Step 04 needs a decision before any csproj edit: **NUnit 3.x current vs NUnit 4?**

**Current state in `Tests.csproj`:**

| Package | Version |
|---|---|
| `nunit` | 3.12.0 |
| `NUnit3TestAdapter` | 3.15.1 |
| `Microsoft.NET.Test.Sdk` | 16.2.0 |

**Two paths:**

1. **NUnit 3 latest** (3.14.x). Lowest friction — every existing assertion already uses `Assert.That(...)` which is unchanged. Companion: `NUnit3TestAdapter` 4.x latest, `Microsoft.NET.Test.Sdk` 17.x. Pure version bumps, expected zero source edits.
2. **NUnit 4** (4.x latest). Long-term direction. Most breaking changes are in classic `Assert.AreEqual`/`StringAssert.Contains`-style API which the codebase doesn't use. Companion: same adapter (compatible) + Test.Sdk 17.x. Probable source impact: zero or near-zero.

**Recommendation: NUnit 4.** Reasons:

- The codebase already uses constraint-based `Assert.That(...)` style — the part of NUnit that NUnit 4 cleaned up was the legacy `Assert.AreEqual`/`StringAssert.*` helpers, none of which appear in `ParserTests.cs`, `ConnectivityTests.cs`, `SqlNormalizerTests.cs`, `CharacterisationRunnerSmokeTests.cs`, or `CharacterisationTests.cs`.
- Avoids a near-future "we should be on NUnit 4 by now" task. This feature exists *because* we let things drift — same lesson applies to test framework choice.
- NUnit 4 mandates explicit `await` semantics in async test methods (already what we do).

Risk: a third-party assertion or extension package incompatible with NUnit 4. None used in this repo.

**Action requested:** approve **NUnit 4 + latest companions** (or push back for NUnit 3 latest).

### 2026-05-02 — Step 04 + 05 completion

Approved by human. Bumped:

| Package | Was | Now |
|---|---|---|
| `NUnit` | 3.12.0 | 4.3.2 |
| `NUnit3TestAdapter` | 3.15.1 | 4.6.0 |
| `Microsoft.NET.Test.Sdk` | 16.2.0 | 17.12.0 |

**My pre-bump claim was wrong.** I said the codebase didn't use legacy Assert API. The build immediately surfaced 14 compile errors in `Tests/ParserTests.cs`: `Assert.IsTrue`, `Assert.IsNotNull`, `Assert.IsInstanceOf`, `Assert.AreEqual` — all removed in NUnit 4.

**Fix (step 05's catch-all):** translated each call to constraint-based form:

| Was | Now |
|---|---|
| `Assert.IsTrue(x)` | `Assert.That(x, Is.True)` |
| `Assert.IsNotNull(x, msg)` | `Assert.That(x, Is.Not.Null, msg)` |
| `Assert.IsInstanceOf(typeof(T), x)` | `Assert.That(x, Is.InstanceOf<T>())` |
| `Assert.AreEqual(expected, actual)` | `Assert.That(actual, Is.EqualTo(expected))` |

Pure-mechanical. No behaviour change.

**Test run:** 18/18 passed in 2s on NUnit 4. Goldens unchanged.

**Step 04 acceptance:**

- [x] Decision recorded (NUnit 4 over 3.x), dated, approved by human.
- [x] Test packages bumped per decision.
- [x] `dotnet test "SQL Buldozer.sln"` exits 0.
- [x] Characterisation suite still passes.

**Step 05 acceptance:**

- [x] `dotnet build "SQL Buldozer.sln"` succeeds with zero errors.
- [x] `dotnet test` exits 0; characterisation suite green.
- [x] No new packages introduced (every change was a version bump on an existing package, or a code-side translation in `ParserResult.fs` / `ParserTests.cs`).
- [x] No behavioural change captured by the characterisation suite.

**Feature `net10-upgrade` complete.**

## Final summary

Cumulative changes across the feature:

- `TargetFramework`: `net6.0` and `netstandard2.0` → `net10.0` across all 6 csproj/fsproj.
- `Microsoft.Extensions.*`: 3.1.0 → 10.0.0 (16 references).
- `Serilog` family: 2.9.0 / 3.0.1 / 3.1.0 / 3.1.1 / 4.1.0 → 4.2.0 / 9.0.0 / 9.0.0 / 6.0.0 / 6.0.0.
- `NUnit` family: 3.12.0 / 3.15.1 / 16.2.0 → 4.3.2 / 4.6.0 / 17.12.0.
- Source edits (in ADR-005 scope and aligned with the methodology's "kept buildable through framework upgrades" clause):
  - `ParameterizationExtractor.DSL/ParserResult.fs` — fixed indentation drift (5 spaces → 4) on `DslOK`/`CommandOK` definitions; F# 10's compiler is stricter about offside rule.
  - `Tests/ParserTests.cs` — translated 14 legacy Assert calls to NUnit 4 constraint syntax.

Out-of-scope items still pending (per the original feature scope, deliberately deferred):

- `System.Data.SqlClient` 4.8.0 → `Microsoft.Data.SqlClient` (vulnerability warnings persist).
- `Newtonsoft.Json` transitive at 9.0.1 (vulnerability warning).
- `FluentCommandLineParser.NETStandard` 1.5.0.31-commands (unmaintained — replacement is its own decision).
- MEF → MS.DI (next feature: `msdi-migration`).
