# Replace Stale Dependencies — Architecture Overview

> Refactor-shaped feature. No new code paths, no new components, no behaviour change. The "architecture" here is the surfaces touched and the regression-net story.

---

## 1. Pipeline / Integration

No runtime pipeline change. The feature touches:

- `Logic.csproj` — `<PackageReference>` for SqlClient.
- 6 source files referencing `System.Data.SqlClient` types — `using` swap to `Microsoft.Data.SqlClient`.
- `ParameterizationExtractor.csproj` — `<PackageReference>` for the args parser.
- `AppBootstrap.cs` — `AppArgs` class (attribute-decorated for `CommandLineParser`) + `AppArgs.GetAppArgs` body rewrite.
- `appsettings.json` — connection-string augmentations to keep working under Microsoft.Data.SqlClient's stricter `Encrypt` default.

```
csproj edits ──┐
using swaps    │──► dotnet restore ──► dotnet build ──► dotnet test (characterisation + new args-parser tests)
config edits   │
source edits  ─┘
```

---

## 2. Component Diagram

No new components. The map of who-uses-which-type stays identical:

| Type today | After this feature |
|---|---|
| `System.Data.SqlClient.SqlConnection` | `Microsoft.Data.SqlClient.SqlConnection` |
| `System.Data.SqlClient.SqlDataReader` | `Microsoft.Data.SqlClient.SqlDataReader` |
| `System.Data.SqlClient.SqlCommand` | `Microsoft.Data.SqlClient.SqlCommand` |
| `Fclp.FluentCommandLineParser<T>` | (gone — `CommandLineParser.Parser.Default.ParseArguments<T>(args)`) |
| `IAppArgs` / `AppArgs` POCO | unchanged contract; gains `[Verb]`/`[Option]` attributes |

Files affected (both swaps combined):

- `ParameterizationExtractor.Logic/Interfaces/IUnitOfWork.cs`
- `ParameterizationExtractor.Logic/MSSQL/UnitOfWork.cs`
- `ParameterizationExtractor.Logic/MSSQL/MSSQLSourceSchema.cs`
- `ParameterizationExtractor.Logic/MSSQL/ConnectionStringResolver.cs`
- `ParameterizationExtractor.Logic/Model/PTable.cs`
- `Tests/CharacterisationTests/ConnectivityTests.cs`
- `ParameterizationExtractor/AppBootstrap.cs`
- `ParameterizationExtractor/ParameterizationExtractor.csproj`
- `ParameterizationExtractor.Logic/ParameterizationExtractor.Logic.csproj`
- `ParameterizationExtractor/appsettings.json` (connection-string augmentation)

---

## 3. Data Flow

N/A — no runtime data flow change.

The two regression nets that *prove* no behaviour change:

- **Engine output:** existing 5 characterisation goldens. Driven by `CharacterisationRunner` which opens a real `SqlConnection` against `budzdorov_Core` — exercises the full SqlClient path during every assertion. Any drift (e.g., a Microsoft.Data.SqlClient quirk that changes how a column type is read) shows up as a golden diff.
- **CLI argument parser:** new tests authored in step 02. `AppArgs.GetAppArgs(string[] args)` is currently exercised by no test. Step 02 builds the net before step 03 swaps the parser.

---

## 4. Data Stores

N/A — same SQL Server target, same `appsettings.json` location, same goldens.

---

## 5. Extension Points

If the swap surfaces a Microsoft.Data.SqlClient connection-string knob we hadn't anticipated (e.g., a new mandatory auth scheme), that's an architectural decision to record in `## Notes` and surface for human approval before swapping. Same for any `CommandLineParser` quirk that diverges from FCLP.

---

## 6. Security & Isolation

- **Net positive:** Microsoft.Data.SqlClient resolves the open CVEs in `System.Data.SqlClient 4.8.0` (`GHSA-8g2p-5pqh-5jmc`, `GHSA-98g6-xh36-x2p7`).
- **Connection encryption:** Microsoft.Data.SqlClient ≥4 defaults to `Encrypt=Mandatory`. The committed dev connection strings in `appsettings.json` neither encrypt nor trust the cert. Step 01 adds `Encrypt=False;TrustServerCertificate=True` to each — preserves *current* behaviour (unencrypted, like System.Data.SqlClient default). The test config (`Tests/appsettings.test.json`) already has `TrustServerCertificate=True`.
- **No new auth surface introduced.** No new SQL injection vector — the engine still parameterises queries the same way.
