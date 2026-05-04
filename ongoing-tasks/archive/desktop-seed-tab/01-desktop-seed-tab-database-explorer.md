# 01 — desktop-seed-tab — Engine seam: IDatabaseExplorer + MSSqlDatabaseExplorer

## Goal

Land the engine-side seam the Desktop will call for the Seed tab: list tables (schema-aware) and run bounded preview queries. Mirrors the `IConnectionTester` pattern from `desktop-connection-management` — engine owns SqlConnection; Desktop hands a connection string + SQL and gets back stringified rows. **No UI consumer yet** — step 04. Step 01 produces a registered, tested seam.

## Track

`engine` (.NET / C#).

## What Exists

- `engine-schema-aware-resolution` is **complete**. `MSSQLSourceSchema.GetMetaData` populates `PTable.Schema`; `sys.tables × sys.schemas` join is the reference SQL.
- [`Logic/Connectivity/MSSQLConnectionTester.cs`](../../ParameterizationExtractor.Logic/Connectivity/MSSQLConnectionTester.cs) — pattern exemplar for an engine-side connection-driven service.
- [`Tests/EngineConnectivityTests/`](../../Tests/EngineConnectivityTests/) — pattern exemplar for engine tests against the live test DB.

## What to Build

### `Logic/Schema/IDatabaseExplorer.cs`

```csharp
namespace Quipu.ParameterizationExtractor.Logic.Schema;

public interface IDatabaseExplorer
{
    Task<IReadOnlyList<TableRef>> ListTablesAsync(string connectionString, CancellationToken ct = default);
    Task<PreviewResult> PreviewQueryAsync(string connectionString, string sql, int maxRows, CancellationToken ct = default);
}

public sealed record TableRef(string Schema, string Name);

public sealed record PreviewResult(
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<string?[]> Rows,
    bool Truncated);
```

Plus `public sealed class DatabaseExplorerException : Exception { public DatabaseExplorerException(string message, Exception? inner = null) : base(message, inner) {} }` — the wrapper for SQL/argument failures so callers don't need to know `Microsoft.Data.SqlClient`.

Visibility: all `public` (engine surface).

### `Logic/Schema/MSSqlDatabaseExplorer.cs`

- `public sealed class MSSqlDatabaseExplorer : IDatabaseExplorer`. Constructor: `ILogger<MSSqlDatabaseExplorer>`.
- `ListTablesAsync(connectionString, ct)`:
  - `SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 10 }`.
  - `await using var conn = new SqlConnection(builder.ConnectionString); await conn.OpenAsync(ct);`
  - SQL: `SELECT s.name AS schema_name, t.name AS table_name FROM sys.tables t INNER JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.is_ms_shipped = 0 ORDER BY s.name, t.name`.
  - Materialise via `await reader.ReadAsync(ct)` into `List<TableRef>`. Return as `IReadOnlyList`.
  - Catch `SqlException` / `InvalidOperationException` / `ArgumentException` → throw `DatabaseExplorerException("Failed to list tables", ex)` (don't swallow into a sentinel result; list-tables failure should surface).
- `PreviewQueryAsync(connectionString, sql, maxRows, ct)`:
  - Same connection setup.
  - `using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };` (30s for a SELECT — preview is interactive).
  - `using var reader = await cmd.ExecuteReaderAsync(ct);`
  - Read column names from `reader.GetColumnSchema()` or `reader.GetName(i)` for `i in 0..reader.FieldCount`.
  - Read rows: read `maxRows` fully, then `await reader.ReadAsync(ct)` once more to test for truncation (don't materialise the +1 row). Set `Truncated = (one-extra-row-existed)`.
  - Stringify each cell: `cell is null or DBNull → null; else Convert.ToString(cell, CultureInfo.InvariantCulture)`.
  - Return `PreviewResult(columns, rows, truncated)`.
  - Catch `SqlException` / `InvalidOperationException` / `ArgumentException` → throw `DatabaseExplorerException("Preview query failed: {sqlSnippet}", ex)`.
  - `OperationCanceledException` propagates.

### DI registration

- **Desktop** ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs)): `services.AddSingleton<IDatabaseExplorer, MSSqlDatabaseExplorer>();`
- **CLI** ([`AppBootstrap.cs`](../../ParameterizationExtractor/AppBootstrap.cs) `AddMSSQL`): same — symmetric per the established pattern.

### Tests

Per `prompts/execution.md` — CODE: TDD cycle. Integration tests against the live test DB (mirror `MSSQLConnectionTesterTests`).

New file `Tests/EngineDatabaseExplorerTests/MSSqlDatabaseExplorerTests.cs`:

1. `ListTablesAsync_ReturnsSchemaNamePairs_OrderedBySchemaThenName` — assert at least one known table is present (`("dbo", "Patient")` or whatever the test DB has); ordering verified.
2. `ListTablesAsync_FiltersSystemTables` — no MS-shipped names appear.
3. `ListTablesAsync_BadConnection_ThrowsDatabaseExplorerException` — bogus server.
4. `ListTablesAsync_Cancelled_PropagatesOperationCancelled` — pre-cancelled token.
5. `PreviewQueryAsync_ValidSelect_ReturnsRowsAndColumns` — `SELECT TOP 5 * FROM Patient` (or whatever exists); assert row count + column names.
6. `PreviewQueryAsync_QueryReturnsMoreThanMaxRows_TruncatedTrue` — `SELECT TOP 250 * FROM …` with `maxRows = 200`; assert `Rows.Count == 200` and `Truncated == true`.
7. `PreviewQueryAsync_QueryReturnsExactlyMaxRows_TruncatedFalse` — `SELECT TOP 200 * FROM …` with `maxRows = 200`; `Truncated == false`.
8. `PreviewQueryAsync_NullCells_StringifyToNull` — query a table with a known nullable column; assert null cells.
9. `PreviewQueryAsync_MalformedSql_ThrowsDatabaseExplorerException`.
10. `PreviewQueryAsync_Cancelled_PropagatesOperationCancelled`.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test "SQL Buldozer.sln"` — pre-existing N + new 10 = N + 10, all green.

## Acceptance Criteria

- [ ] `Logic/Schema/{IDatabaseExplorer.cs, MSSqlDatabaseExplorer.cs}` exist; `public` surface; `DatabaseExplorerException` defined.
- [ ] `MSSqlDatabaseExplorer` uses `SqlConnectionStringBuilder` (mirrors tester); 10s connect timeout, 30s command timeout.
- [ ] `PreviewQueryAsync` reads `maxRows + 1` to detect truncation without materialising the +1 row.
- [ ] Cells stringified via `Convert.ToString(value, InvariantCulture)`; nulls and `DBNull` → null.
- [ ] DI registration in Desktop + CLI.
- [ ] All 10 new tests pass against the live test DB.
- [ ] `Microsoft.Data.SqlClient` types confined to `Logic/` (grep verifies).
- [ ] No tripwires introduced.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- ADR: [`adr/011-engine-schema-awareness.md`](../../adr/011-engine-schema-awareness.md) (transitive — table listing returns `(Schema, Name)`).
- Pattern exemplars: `Logic/Connectivity/MSSQLConnectionTester.cs`, `Tests/EngineConnectivityTests/`.
- Depends on: `engine-schema-aware-resolution` complete (must be archived before this step starts).
