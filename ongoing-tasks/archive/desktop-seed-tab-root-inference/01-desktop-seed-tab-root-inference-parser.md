# 01 — desktop-seed-tab-root-inference — `SeedQueryParser` (Logic helper) + tests

## Goal

Land a small static helper that extracts the first `FROM <schema>.<table>` (or bare `<table>`) from a SQL string. Pure / stateless / thread-safe; no DI. Lives in `Logic` so it can be tested without WPF and reused if the CLI ever wants similar inference.

## Track

`engine` (.NET / C#).

## What Exists

- [`Logic/Helpers/`](../../ParameterizationExtractor.Logic/Helpers/) — existing folder for engine helpers (e.g. `ConfigHelper`, `SqlHelper`).
- [`Logic/Schema/IDatabaseExplorer.cs`](../../ParameterizationExtractor.Logic/Schema/IDatabaseExplorer.cs) — defines `TableRef(Schema, Name)`. Reuse the same shape; do NOT introduce a parallel record.

## What to Build

### `Logic/Helpers/SeedQueryParser.cs`

```csharp
namespace Quipu.ParameterizationExtractor.Logic.Helpers;

public static class SeedQueryParser
{
    /// <summary>
    /// Extracts the first <c>FROM &lt;schema&gt;.&lt;table&gt;</c> (or bare <c>FROM &lt;table&gt;</c>)
    /// from a SQL string. Returns null if no <c>FROM</c> is found, the SQL is empty,
    /// or the next token after FROM is not an identifier (e.g. a subquery).
    /// </summary>
    public static SeedRoot? TryExtract(string? sql);
}

public sealed record SeedRoot(string Schema, string Name);
```

Parsing rules per `feature-architecture.md` § 2:

- Strip `-- line comments` and `/* block comments */` first.
- Locate the first whole-word `FROM` (case-insensitive); skip if inside a string literal.
- Capture the identifier-pattern that follows: optionally bracketed `[...]`, optionally `schema.` prefixed.
- Stop at whitespace / `,` / `;` / `(` / newline.
- Accept identifier characters: `[A-Za-z0-9_#@$]` plus anything inside `[...]`.
- If the next token is `(`, return null (subquery / table-valued function).

### Tests

Per `prompts/execution.md` — CODE: TDD cycle.

New file `Tests/EngineHelperTests/SeedQueryParserTests.cs`:

| # | Input | Expected |
|---|---|---|
| 1  | `SELECT * FROM Patient` | `("", "Patient")` |
| 2  | `SELECT * FROM dbo.Patient` | `("dbo", "Patient")` |
| 3  | `SELECT * FROM [dbo].[Patient]` | `("dbo", "Patient")` |
| 4  | `SELECT * FROM [Patient]` | `("", "Patient")` |
| 5  | `   SELECT *   FROM   audit.Logs   WHERE Id = 1` | `("audit", "Logs")` |
| 6  | multi-line SQL with newline before `FROM` | the identifier |
| 7  | `SELECT 1` (no FROM) | `null` |
| 8  | `null` / `""` | `null` |
| 9  | `-- FROM Patient\n SELECT 1` (FROM inside line comment) | `null` |
| 10 | `/* FROM Patient */ SELECT 1` (FROM in block comment) | `null` |
| 11 | `SELECT * FROM (SELECT 1) x` (subquery) | `null` |
| 12 | `SELECT * FROM Patient;` (trailing semicolon) | `("", "Patient")` |
| 13 | `SELECT * FROM Patient WHERE LastName = 'FROM cheat'` (FROM in string) | `("", "Patient")` (first FROM wins) |
| 14 | `WITH cte AS (SELECT 1) SELECT * FROM cte` (CTE — limitation) | `("", "cte")` (documented; operator overrides) |
| 15 | `SELECT * FROM dbo.[my table]` (bracketed identifier with space) | `("dbo", "my table")` |
| 16 | `select * from PATIENT` (lowercase keyword) | `("", "PATIENT")` |
| 17 | `select * From #Temp` (temp table) | `("", "#Temp")` |

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-existing N + 17 = N + 17, all green.

## Acceptance Criteria

- [ ] `Logic/Helpers/SeedQueryParser.cs` exists; `public static class`; `SeedRoot` record `public sealed`.
- [ ] All 17 tests pass.
- [ ] No DI / state / mutable fields — pure function.
- [ ] No new dependencies (regex from `System.Text.RegularExpressions` only).
- [ ] Grep confirms `SeedQueryParser` is not consumed by anyone yet (step 02 wires it in).

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 2.
- Pattern exemplar: [`Logic/Helpers/ConfigHelper.cs`](../../ParameterizationExtractor.Logic/Helpers/ConfigHelper.cs) (static helper, no state).
- Depends on: nothing (this step is self-contained).
