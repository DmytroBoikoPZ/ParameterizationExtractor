# Replace Stale Dependencies

## Goal

Replace two unmaintained / security-flagged packages with their actively-maintained counterparts:

- `System.Data.SqlClient 4.8.0` → `Microsoft.Data.SqlClient` (Microsoft has migrated; old package has open CVEs).
- `FluentCommandLineParser.NETStandard 1.5.0.31-commands` → `CommandLineParser` (TM Henrik) — FCLP last released 2018, no .NET 10 testing, no known CVE but a maintenance liability.

Observable behaviour preserved end-to-end. Engine output pinned by the existing characterisation suite. CLI argument-parser behaviour pinned by **new** tests authored as part of this feature (no regression net exists for that path today).

## Scope

- **In scope:**
  - `System.Data.SqlClient` → `Microsoft.Data.SqlClient` (target version: `6.0.2`).
  - Connection-string adjustments to handle the changed `Encrypt` default in Microsoft.Data.SqlClient ≥4 (defaults to `Mandatory`; existing committed connection strings have neither `Encrypt=` nor `TrustServerCertificate=`).
  - Pin current `AppArgs.GetAppArgs(string[])` behaviour with NUnit tests covering: each option (short and long form), each default, the `PathToPackage` required-throw, and the two paired-args validation rules at `AppBootstrap.cs:65-69`.
  - `FluentCommandLineParser.NETStandard` → `CommandLineParser` (target version: `2.9.1`). Attribute-decorate the existing `AppArgs` POCO; rewrite `GetAppArgs` body. Same tests must stay green.
  - Remove the FCLP package + `using Fclp;` directive at the end.
- **Out of scope:**
  - Adding new CLI options or changing the existing `IAppArgs` contract.
  - Fixing the swapped error messages on `AppBootstrap.cs:65-69` (`"Please specify DBName!"` fires when DBName is *non-empty*, and vice versa). **Pinning the bug as-is** in step 2 is the right call — the current behaviour is what consumers expect; fixing the messages is a separate decision after this feature ships.
  - MEF → MS.DI migration (separate feature).
  - Refactoring `IAppArgs` consumers (`UnitOfWorkFactory`, `FromFileExecutor`, etc.).
- **Dependencies:**
  - `characterisation-tests` (archived) — green; provides the engine-output regression net.
  - `net10-upgrade` (archived) — completed; this feature operates on the .NET 10 / NUnit 4 build.

## Architecture

See [feature-architecture.md](./replace-stale-deps/feature-architecture.md). Refactor-shaped feature; most architecture sections are intentionally N/A.

## Steps

- [x] [01 — System.Data.SqlClient → Microsoft.Data.SqlClient](./replace-stale-deps/01-replace-stale-deps-sqlclient.md)
- [x] [02 — pin current AppArgs.GetAppArgs behaviour with tests](./replace-stale-deps/02-replace-stale-deps-args-tests.md)
- [x] [03 — swap FluentCommandLineParser → CommandLineParser; remove FCLP](./replace-stale-deps/03-replace-stale-deps-args-swap.md)

## Notes

### 2026-05-02 — Step 01 completion (SqlClient swap)

**Files changed:**

- `Logic.csproj` — `System.Data.SqlClient 4.8.0` → `Microsoft.Data.SqlClient 6.0.2`.
- 6 `.cs` files: `using System.Data.SqlClient;` → `using Microsoft.Data.SqlClient;` (Logic/Interfaces/IUnitOfWork.cs, Logic/MSSQL/UnitOfWork.cs, Logic/MSSQL/MSSQLSourceSchema.cs, Logic/MSSQL/ConnectionStringResolver.cs, Logic/Model/PTable.cs, Tests/CharacterisationTests/ConnectivityTests.cs). Type names identical across both namespaces — no further source edits.
- `ParameterizationExtractor/appsettings.json` — appended `;Encrypt=False;TrustServerCertificate=True` to all 4 committed connection strings. Preserves pre-feature unencrypted-dev-DB behaviour (Microsoft.Data.SqlClient ≥4 defaults to `Encrypt=Mandatory`).
- `ParameterizationExtractor.csproj` — `System.Configuration.ConfigurationManager 7.0.0` → `10.0.0`. Forced by `Microsoft.Data.SqlClient 6.0.2`'s transitive constraint (>= 9.0.4); going to 10.0.0 instead of 9.0.4 keeps it consistent with the rest of the `Microsoft.Extensions.*` 10.x family. CVE warnings for `System.Data.SqlClient` are gone.

**Verification:** 0 build errors; 18/18 tests pass; goldens unchanged; zero `System.Data.SqlClient` references remain in source.

### 2026-05-02 — Step 02 completion (pin AppArgs behaviour)

**Files changed:**

- `Tests/Tests.csproj` — added `<ProjectReference Include="..\ParameterizationExtractor\ParameterizationExtractor.csproj" />` so tests can see the `AppArgs` type. Implied by step intent (can't pin behaviour you can't reach).
- `Tests/AppArgsTests.cs` — new fixture, 15 NUnit 4 tests, all passing against the **current** `FluentCommandLineParser` implementation.

**Pinned quirks (documented inline as comments):**

1. **FCLP's `.Required()` does not throw** when the parse result is ignored. The current `GetAppArgs` ignores the result, so missing `--package` silently returns `AppArgs { PathToPackage = null }`. → Test `PathToPackage_Missing_Returns_Null` (replaced in step 03).
2. **FCLP treats leading `/` as a switch prefix on Windows**, so `-o /tmp` did not bind. The override-behaviour test uses `-o MyOut` (non-slash) so it remains about override semantics rather than switch-prefix parsing.
3. **The two cross-arg validation messages are flipped** (`AppBootstrap.cs:65-69`) — `"Please specify DBName!"` fires when DBName *is* set but ServerName isn't. Pinned exactly. Fixing the wording is a separate follow-up.

**Verification:** 15/15 new tests + 18/18 prior = **33/33 green**.

### 2026-05-02 — Step 03 completion (FCLP → CommandLineParser swap)

**Files changed:**

- `ParameterizationExtractor.csproj` — removed `FluentCommandLineParser.NETStandard 1.5.0.31-commands`, added `CommandLineParser 2.9.1`.
- `ParameterizationExtractor/AppBootstrap.cs`:
  - `using Fclp;` → `using CommandLine;`.
  - `AppArgs` properties decorated with `[Option]` attributes (mapping the same short/long forms and defaults as before).
  - `GetAppArgs` body rewritten using `Parser.Default.ParseArguments<AppArgs>(args)` + `Parsed<AppArgs>` pattern match. Cross-arg validation block preserved verbatim (including the pinned-bug error messages).

**Deliberate divergence from FCLP era — recorded:**

- `PathToPackage_Missing_Returns_Null` test was renamed to `PathToPackage_Missing_Throws` and its assertion changed from `Assert.That(a.PathToPackage, Is.Null.Or.Empty)` to `Assert.Throws<Exception>(...)`. CommandLineParser surfaces missing-required-option as a `NotParsed<T>` result, which `GetAppArgs` translates to an exception. Failing fast on missing required is correct behaviour; the prior FCLP silent-null was a quirk caused by the original code ignoring the parse result. Documented as a behaviour upgrade (not a regression) in the test's comment.

**No other divergences surfaced.** All other 14 tests pass against the new parser as-is — short/long-form parsing, defaults, override semantics, the cross-arg validation throw paths with their pinned-bug messages, and the bool `Interactive` arg with `-i true`/`--Interactive true`.

**Cleanup confirmed:** zero `Fclp.` / `FluentCommandLineParser` references remain in the source tree (only mention is a doc-comment in `AppArgsTests.cs` describing historical context).

**Final test run:** 33/33 green on net10.0 / NUnit 4 / Microsoft.Data.SqlClient 6.0.2 / CommandLineParser 2.9.1.

## Final summary

Cumulative changes across the feature:

- **Packages:**
  - `System.Data.SqlClient 4.8.0` → `Microsoft.Data.SqlClient 6.0.2` (CVEs cleared).
  - `FluentCommandLineParser.NETStandard 1.5.0.31-commands` → `CommandLineParser 2.9.1` (live, maintained).
  - `System.Configuration.ConfigurationManager 7.0.0` → `10.0.0` (transitive forced; aligned with rest of stack).
- **Source:** 6 `using` swaps; `AppArgs` decorated with `[Option]` attributes; `GetAppArgs` body rewritten to use `Parser.Default.ParseArguments`.
- **Config:** `Encrypt=False;TrustServerCertificate=True` on all 4 committed connection strings.
- **Test infra:** `Tests` now references the CLI project; new `AppArgsTests.cs` (15 tests) pins parser behaviour going forward.

Pinned-but-not-fixed bugs (deliberate — out of scope):

- Flipped error messages on `AppBootstrap.cs:65-69` ("Please specify DBName!" fires when DBName is set). Tests pin them; fix is a separate single-line follow-up.

Out-of-scope items still pending:

- MEF → MS.DI migration (next feature: `msdi-migration`).
- F# DSL retirement decision (after UI ships, per ADR-005).