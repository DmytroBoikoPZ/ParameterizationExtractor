# 01 — Replace Stale Deps — `System.Data.SqlClient` → `Microsoft.Data.SqlClient`

## Goal

Replace `System.Data.SqlClient` with `Microsoft.Data.SqlClient`. Type names are identical; the swap is the namespace + the package + connection-string adjustments to handle the changed `Encrypt` default. Engine output unchanged — pinned by the characterisation suite.

## Track

dotnet (cross-cutting — Logic + CLI + Tests)

## What Exists

- `Logic.csproj` references `System.Data.SqlClient 4.8.0` (CVE warnings: `GHSA-8g2p-5pqh-5jmc`, `GHSA-98g6-xh36-x2p7`).
- 6 source files use the namespace via `using System.Data.SqlClient;`:
  - `Logic/Interfaces/IUnitOfWork.cs` (interface signature uses `SqlDataReader`)
  - `Logic/MSSQL/UnitOfWork.cs`
  - `Logic/MSSQL/MSSQLSourceSchema.cs`
  - `Logic/MSSQL/ConnectionStringResolver.cs`
  - `Logic/Model/PTable.cs`
  - `Tests/CharacterisationTests/ConnectivityTests.cs`
- 4 committed connection strings in `ParameterizationExtractor/appsettings.json` use `Integrated Security=True` with **no** `Encrypt=` or `TrustServerCertificate=`. Microsoft.Data.SqlClient ≥4 defaults to `Encrypt=Mandatory` — these will fail TLS unless updated.
- `Tests/appsettings.test.json` already has `TrustServerCertificate=True`. No change needed there beyond verifying.

## What to Build

- `Logic.csproj`: `<PackageReference Include="System.Data.SqlClient" Version="4.8.0" />` → `<PackageReference Include="Microsoft.Data.SqlClient" Version="6.0.2" />`.
- Replace `using System.Data.SqlClient;` → `using Microsoft.Data.SqlClient;` in the 6 source files. Type names (`SqlConnection`, `SqlDataReader`, `SqlCommand`, `SqlTransaction`) are the same in both namespaces — no further source edits expected.
- `ParameterizationExtractor/appsettings.json`: append `;Encrypt=False;TrustServerCertificate=True` to each of the 4 connection strings. Rationale: preserve unencrypted dev-DB behaviour as today; deferring the move to encrypted connections is a separate decision.
- `Tests/appsettings.test.json`: connection string already has `TrustServerCertificate=True` — verify it still works under Microsoft.Data.SqlClient. Add `Encrypt=False` only if the connection fails.

## Acceptance Criteria

- [ ] `Logic.csproj` references `Microsoft.Data.SqlClient 6.0.2` and no longer references `System.Data.SqlClient`.
- [ ] No `using System.Data.SqlClient;` in any `.cs` file in the repo.
- [ ] `dotnet build "SQL Buldozer.sln"` — 0 errors. CVE warnings (`NU1903`/`NU1902`) for `System.Data.SqlClient` no longer surface.
- [ ] `dotnet test "SQL Buldozer.sln"` — 18/18 pass. Connectivity test connects. Characterisation suite green (5 goldens unchanged).
- [ ] Each committed connection string in `appsettings.json` has `Encrypt=False;TrustServerCertificate=True`.

## References

- Related ADRs: —
- Related methodology: `docs/methodology/dotnet-cli.md` § Module layering (SqlClient stays in `Logic` only — but the new namespace must respect the same rule).
- Depends on: `net10-upgrade` (archived, complete).
