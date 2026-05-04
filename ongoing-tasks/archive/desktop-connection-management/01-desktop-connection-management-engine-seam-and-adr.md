# 01 — desktop-connection-management — ADR-010 + engine connection-test seam

## Goal

Land **ADR-010 — password-at-rest** (the formal record of the L6 design lock) and the engine-side `IConnectionTester` abstraction the Desktop will call from later steps. **No UI yet** — step 01 produces an ADR file, an interface + impl in `Logic`, a registered DI binding (CLI + Desktop), and integration tests against the test database. Subsequent steps depend on this seam being tested and reachable.

## Track

`engine` (.NET / C#) + `docs`.

## What Exists

- [`adr/readme.md`](../../adr/readme.md) — ADR index. Latest is `009-workspace-format.md`. Number this `010`.
- [`adr/009-workspace-format.md`](../../adr/009-workspace-format.md) — pattern exemplar for ADR shape (Status / Context / Decision / Consequences).
- [`ParameterizationExtractor.Logic/MSSQL/MSSQLSourceSchema.cs`](../../ParameterizationExtractor.Logic/MSSQL/MSSQLSourceSchema.cs) — pattern exemplar for engine code that opens `SqlConnection` (uses `IUnitOfWorkFactory` indirection). Read before designing the tester.
- [`Tests/CharacterisationTests/Harness/CharacterisationRunner.cs`](../../Tests/CharacterisationTests/Harness/CharacterisationRunner.cs) — pattern exemplar for engine integration tests against the test DB.
- [`Tests/CharacterisationTests/TestDbConfig.cs`](../../Tests/CharacterisationTests/TestDbConfig.cs) — connection-string resolver tests use.
- [`docs/methodology/dotnet-cli.md`](../../docs/methodology/dotnet-cli.md) — engine recipe.

## What to Build

### `adr/010-desktop-password-at-rest.md`

Sections:

- **Status:** Accepted (date: today). Supersedes nothing.
- **Context:** Operator workflows need to open the same `.bws` daily without retyping the SQL password. Storing plaintext is unacceptable; storing nothing forces re-entry on every open. Mockup M2's L6 design pass locked DPAPI `DataProtectionScope.CurrentUser` with an opt-out checkbox; this ADR formalises that.
- **Decision:**
  - Passwords are persisted in `WorkspaceSource.PasswordEncrypted` as base64-encoded DPAPI ciphertext.
  - Encryption uses `System.Security.Cryptography.ProtectedData.Protect` with `DataProtectionScope.CurrentUser` and an empty entropy parameter (`null`).
  - "Store credentials" checkbox in the connection editor is checked by default. When unchecked, the workspace is saved with `passwordEncrypted: null` and the operator must re-enter on each open.
  - Decrypt failures (different user / machine) are swallowed at the boundary: warn-log + treat as if `passwordEncrypted: null`. The Edit-connection flow is the recovery path.
  - The Desktop holds the decrypted password as a runtime shadow string for the lifetime of the loaded workspace; cleared on Close.
- **Consequences:**
  - `.bws` files are not portable across user accounts / machines without re-entering credentials. **Acceptable** — single-operator-per-machine is the canonical case.
  - No vault / KMS integration is needed for v1.
  - Future "share workspace" or "team library" features will need a plaintext-export path or a different encryption scheme; either way this ADR will be revisited.
  - The `WorkspaceSource.PasswordEncrypted` field's *semantics* change without a `$version` bump — the wire format was already future-proof for ciphertext.

### `adr/readme.md`

- Add the new entry to the index. Match the existing line shape.

### `ParameterizationExtractor.Logic/Connectivity/IConnectionTester.cs`

```csharp
namespace Quipu.ParameterizationExtractor.Logic.Connectivity;

public interface IConnectionTester
{
    Task<ConnectionTestResult> TestAsync(string connectionString, CancellationToken ct = default);
}

public sealed record ConnectionTestResult(
    bool Success,
    int TableCount,
    int FkCount,
    string? ErrorMessage);
```

Visibility: `public` (engine surface — Desktop consumes via project reference).

### `ParameterizationExtractor.Logic/Connectivity/MSSQLConnectionTester.cs`

- `public sealed class MSSQLConnectionTester : IConnectionTester`. Constructor takes `ILogger<MSSQLConnectionTester>` (engine pattern — see `MSSQLSourceSchema`).
- `TestAsync(connectionString, ct)`:
  1. Parse the connection string via `SqlConnectionStringBuilder`. If invalid → `ConnectionTestResult(false, 0, 0, ex.Message)`.
  2. Set `builder.ConnectTimeout = 10` (override only if not set; respect operator-supplied higher values? **Decide in impl** — pragmatic default: always cap at 10 to fail fast).
  3. `await using var conn = new SqlConnection(builder.ConnectionString)`.
  4. `await conn.OpenAsync(ct)`.
  5. Two `SELECT COUNT(*)` queries: `sys.tables` and `sys.foreign_keys`. Use `SqlCommand` with `ct`.
  6. Return `ConnectionTestResult(true, tableCount, fkCount, null)`.
  7. Catch `SqlException`, `InvalidOperationException`, `ArgumentException` → return `(false, 0, 0, ex.Message)`. **Do not catch `OperationCanceledException`** — let it propagate so the VM can distinguish cancellation from failure.
  8. Log a single warning on failure with the exception object; on success log `Information` with table/fk counts (named placeholders only).

### DI registration

- **Desktop** ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs)): `services.AddSingleton<IConnectionTester, MSSQLConnectionTester>();`
- **CLI** ([`AppBootstrap.cs`](../../ParameterizationExtractor/AppBootstrap.cs)): same line. Even though the CLI doesn't consume it today, registering symmetrically keeps the engine modules composable from either entry point. Cross-check: the existing CLI bootstrap is the place; do not register in Logic itself (Logic has no composition root).

### Tests

New file `Tests/Engine/Connectivity/MSSQLConnectionTesterTests.cs` (mirror the `Tests/CharacterisationTests/` folder layout — Engine tests group by namespace).

Per `prompts/execution.md` — engine code is **CODE**: TDD cycle.

1. `TestAsync_ValidConnection_ReturnsSuccessWithCounts` — uses `TestDbConfig.ResolveSourceConnectionString()`. Asserts `Success == true`, `TableCount > 0`, `FkCount > 0`, `ErrorMessage == null`.
2. `TestAsync_InvalidServer_ReturnsFailureWithMessage` — connection string with `Server=nonexistent.invalid,1433`. Asserts `Success == false`, `ErrorMessage` non-empty.
3. `TestAsync_BadCredentials_ReturnsFailureWithMessage` — valid server, wrong password. Asserts `Success == false`, `ErrorMessage` mentions login (the SQL error).
4. `TestAsync_CancelledBeforeOpen_ThrowsOperationCancelled` — pass an already-cancelled `CancellationToken`. Asserts `OperationCanceledException` thrown (i.e. **does not** wrap into a `ConnectionTestResult`).
5. `TestAsync_MalformedConnectionString_ReturnsFailureWithoutThrowing` — pass `"not a connection string"`. Asserts `Success == false`, no throw.

These tests **require** the live test SQL Server (same as Characterisation tests). Mark the fixture with `[Category("RequiresSqlServer")]` if the project has such a category; otherwise just leave a comment matching `CharacterisationTests`. They run as part of `dotnet test "SQL Buldozer.sln"`.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test "SQL Buldozer.sln"` — pre-existing 94 + 5 = **99 tests**, all green.

## Acceptance Criteria

- [ ] `adr/010-desktop-password-at-rest.md` exists with the four standard sections; referenced from `adr/readme.md`.
- [ ] `IConnectionTester` + `ConnectionTestResult` exist in `Quipu.ParameterizationExtractor.Logic.Connectivity` and are `public`.
- [ ] `MSSQLConnectionTester` is the registered impl. Connection timeout capped at 10s. Catches SQL/Argument/InvalidOperation, lets `OperationCanceledException` propagate.
- [ ] DI registration in `DesktopHost.cs` and `AppBootstrap.cs`.
- [ ] All 5 `MSSQLConnectionTesterTests` pass against the test DB.
- [ ] No `Microsoft.Data.SqlClient` types leak into `Common`, `Desktop`, `DSL`, `DSL.Connector` (grep verifies).
- [ ] No new `TODO`/`FIXME`/`Console.WriteLine`/`IConfiguration[..]`/`Thread.Sleep`/`.Result`/`.Wait()` introduced.
- [ ] `dotnet build` 0 errors; `dotnet test` 99/99 pass.

## References

- ADR: this step authors `adr/010-desktop-password-at-rest.md`; references `009-workspace-format.md` for the `WorkspaceSource` schema.
- Methodology: [`docs/methodology/dotnet-cli.md`](../../docs/methodology/dotnet-cli.md) (engine recipe).
- Pattern exemplars: [`ParameterizationExtractor.Logic/MSSQL/MSSQLSourceSchema.cs`](../../ParameterizationExtractor.Logic/MSSQL/MSSQLSourceSchema.cs), [`Tests/CharacterisationTests/`](../../Tests/CharacterisationTests/).
- Depends on: nothing (first step).
