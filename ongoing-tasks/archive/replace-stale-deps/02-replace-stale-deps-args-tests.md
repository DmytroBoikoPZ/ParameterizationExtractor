# 02 — Replace Stale Deps — Pin `AppArgs.GetAppArgs` behaviour with tests

## Goal

Build a regression net for `AppBootstrap.cs::AppArgs.GetAppArgs(string[])` — the only consumer of `FluentCommandLineParser` — *before* the parser is swapped in step 03. No production code touched in this step.

The current parser's behaviour, including its quirks (e.g., the swapped error messages on lines 65-69), is pinned as-is. Step 03's swap must keep the same tests green.

## Track

dotnet (test infrastructure)

## What Exists

- [ParameterizationExtractor/AppBootstrap.cs:36-72](../../ParameterizationExtractor/AppBootstrap.cs) — `AppArgs.GetAppArgs(string[] args)` static method using `FluentCommandLineParser<AppArgs>`.
- 6 options:
  - `ConnectionName`: `-n` / `--connectionName`, default `"SourceDB"`.
  - `PathToPackage`: `-p` / `--package`, **required**, no default. Description: `"Path to package"`.
  - `DBName`: `-d` / `--database`, no default.
  - `ServerName`: `-s` / `--serverName`, no default.
  - `OutputFolder`: `-o` / `--outputFolder`, default `"Output"`.
  - `Interactive`: `-i` / `--Interactive` (capital I in long form), bool, default `false`.
- 2 cross-arg validation rules (note: error messages are flipped — pin as-is):
  - `if (ServerName empty && DBName non-empty) throw "Please specify DBName!"` (line 65-66 — message says "specify DBName" but the user already specified DBName; bug pinned).
  - `if (DBName empty && ServerName non-empty) throw "Please specify ServerName!"` (line 68-69 — same flip).
- No existing test exercises this method. Characterisation suite bypasses it entirely (builds `SourceForScript` in-memory).

## What to Build

- `Tests/AppArgsTests.cs` — single NUnit fixture covering the matrix below. Use plain `Assert.That(...)` constraint syntax (NUnit 4).

Test matrix (one `[Test]` per row unless noted):

| Test | Input args | Asserts |
|---|---|---|
| `Required_PathToPackage_Missing_Throws` | `[]` | throws (any exception type) |
| `PathToPackage_Short_Form` | `-p somepkg` | `PathToPackage == "somepkg"` |
| `PathToPackage_Long_Form` | `--package somepkg` | `PathToPackage == "somepkg"` |
| `ConnectionName_Default_Is_SourceDB` | `-p somepkg` | `ConnectionName == "SourceDB"` |
| `ConnectionName_Short_Form_Overrides_Default` | `-p x -n MyConn` | `ConnectionName == "MyConn"` |
| `ConnectionName_Long_Form_Overrides_Default` | `-p x --connectionName MyConn` | `ConnectionName == "MyConn"` |
| `OutputFolder_Default_Is_Output` | `-p x` | `OutputFolder == "Output"` |
| `OutputFolder_Short_Override` | `-p x -o /tmp` | `OutputFolder == "/tmp"` |
| `Interactive_Default_Is_False` | `-p x` | `Interactive == false` |
| `Interactive_Short_Form_True` | `-p x -i true` | `Interactive == true` |
| `Interactive_Long_Form_True` | `-p x --Interactive true` | `Interactive == true` |
| `DBName_And_ServerName_Both_Set` | `-p x -d Db1 -s Srv1` | `DBName == "Db1"`, `ServerName == "Srv1"`, no throw |
| `DBName_Without_ServerName_Throws_With_Pinned_Message` | `-p x -d Db1` | throws with `Message == "Please specify ServerName!"` (pinned bug — current code throws this when *DBName* was the one provided) |
| `ServerName_Without_DBName_Throws_With_Pinned_Message` | `-p x -s Srv1` | throws with `Message == "Please specify DBName!"` (pinned bug — symmetrical) |
| `Neither_DBName_Nor_ServerName_Is_Allowed` | `-p x` | no throw; both null/empty (only `ConnectionName` is used by `UnitOfWorkFactory` in this case) |

Notes:

- For the boolean `Interactive`, FCLP accepts `-i true`, `-i false`. Confirm exact syntax during authoring; if FCLP also accepts `-i` without a value, add a test for that. Pin whatever the current parser does.
- Don't enumerate options the test pin doesn't reach (e.g., the `WithDescription("Path to package")` chain). Description text is parser-internal and not consumed by `AppArgs`.

## Acceptance Criteria

- [ ] `Tests/AppArgsTests.cs` exists with at least the rows above; ~14-15 tests.
- [ ] All tests pass against the *current* `FluentCommandLineParser`-based implementation. (RED is unnecessary — the production code already exists; the goal is locking it in, not driving new code.)
- [ ] Tests use NUnit 4 constraint syntax (`Assert.That`, no `Assert.IsTrue`/`Assert.AreEqual`).
- [ ] `dotnet test "SQL Buldozer.sln"` — green; the existing 18 tests still pass; new test count visible.
- [ ] No production code change in this step.
- [ ] The two pinned-bug tests have a comment explaining "pinned bug — error message is flipped vs intent; see step 03 for the swap; fix is a separate follow-up."

## References

- Related ADRs: —
- Related methodology: `docs/methodology/dotnet-cli.md` § Testing.
- Depends on: `01-replace-stale-deps-sqlclient.md` (run on the SqlClient-swapped tree, but the swap doesn't affect args parsing — could in principle run in parallel).
