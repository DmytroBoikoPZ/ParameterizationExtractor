# Characterisation Tests

## Goal

Pin the current behaviour of the SQL Buldozer engine — graph walk, strategy application, SQL emission — with golden-output tests that fail loudly if subsequent modernisation work changes anything observable. Goal is *regression net first*, before .NET 10 and MEF→MS.DI work touches the codebase.

## Scope

- **In scope:**
  - Test scenarios run against the existing dev SQL Server (`budzdorov_Core` on `129.212.168.210,1433`) — real ClinicV2 schema, ~370 tables across 17 schemas. FK constraints for the test scenarios' relation chains are added by the DBA out-of-band before step 01 finishes. Disposable test DB; connection string committed in `Tests/appsettings.test.json` per the same convention as `ParameterizationExtractor/appsettings.json`.
  - At least one scenario per `ExtractStrategy` variant (`FKDependency`, `OnlyChildren`, `OnlyParent`, `OnlyOneTable`) and at least one `Where`-filtered scenario.
  - Test harness that runs the engine end-to-end and captures emitted SQL as committed golden files.
  - One NUnit test per scenario asserting current SQL output is byte-equal (after normalisation) to its golden.
  - Tests run via the existing `dotnet test "SQL Buldozer.sln"` path — no new test project.
- **Out of scope:**
  - New behaviour, bug fixes, or "improvements" to the engine. If a current output is ugly, that goes in a separate task.
  - Synthetic fixture schema / seed-data SQL files (the real dev DB is the source).
  - Coverage of the F# DSL parser (frozen — see ADR-005).
  - CI integration / test-runner orchestration outside `dotnet test`.
- **Dependencies:** None. This is the first feature; the other two (`net10-upgrade`, `msdi-migration`) depend on this one being green.
- **Known fragility:** Goldens are pinned against real DB rows. Schema drift or data drift on the dev DB invalidates goldens. Step 04 captures the regen workflow; step 02 picks deterministic seed-row IDs to minimise drift surface.

## Architecture

See [feature-architecture.md](./characterisation-tests/feature-architecture.md) for fixture shape, harness layout, and golden-file convention.

## Steps

- [x] [01 — survey extraction scenarios to pin](./characterisation-tests/01-characterisation-tests-survey-scenarios.md)
- [x] [02 — test DB config + schema survey](./characterisation-tests/02-characterisation-tests-test-db-config.md)
- [x] [03 — test harness: run engine against fixture, capture SQL](./characterisation-tests/03-characterisation-tests-harness.md)
- [x] [04 — lock golden outputs per scenario](./characterisation-tests/04-characterisation-tests-goldens.md)
- [x] [05 — wire scenarios into NUnit `Tests/`; verify `dotnet test` runs them green](./characterisation-tests/05-characterisation-tests-wire-suite.md)

## Notes

### 2026-05-02 — Step 01 scenario survey (proposal, awaiting human sign-off)

**Step 01 task type:** planning / research deliverable. Output is this section, not code.

#### Engine behaviour pinned by these scenarios

- `MSSQLSourceSchema.Init` populates relations exclusively from `sys.foreign_keys` via `ObjectMetaDataProvider.GetDependentTables`.
- `DependencyBuilder.GetRelatedTables` is the walker; reads only `_schema.DependentTables` (i.e. live FKs).
- Per-table `ExtractStrategy.Where` is appended to the child-walk SQL via `str = $"{str} AND {tableExtractStrategy.Where}"` ([DependencyBuilder.cs:148-149](../ParameterizationExtractor.Logic/MSSQL/DependencyBuilder.cs#L148-L149)).
- SQL emission is via T4 (`Templates/DefaultTemplate.tt`) — that's the surface being pinned.

#### Scenarios

| # | Name (kebab) | Strategy | Seed root record | Per-table `Where` | Multi-level chain | What it pins |
|---|---|---|---|---|---|---|
| 1 | `only-one-table-employee` | `OnlyOneTableExtractStrategy` (`ProcessChildren=false`, `ProcessParents=false`) | `dbo.Employees` `WHERE Id = 1` | none | no — single row | T4 SQL emission for one row, no graph walk. Baseline. |
| 2 | `only-parent-from-payment` | `OnlyParentExtractStrategy` (`ProcessChildren=false`, `ProcessParents=true`) | `dbo.Payments` `WHERE Id = 2488889` | none | yes — 8 outgoing FKs from `dbo.Payments`; many chain ≥3 levels up | Multi-FK upward walk; per-FK N:1 PK lookup; ordering of parent inserts |
| 3 | `only-children-from-therapy-program` | `OnlyChildrenExtractStrategy` (`ProcessChildren=true`, `ProcessParents=false`) | `dbo.TherapyPrograms` `WHERE Id = 904` | none | yes — `TherapyPrograms` → `TherapyProgramItems` (~4 rows; FK column `Program_Id`) → its grandchildren | Downward walk, per-table strategy default carry-through, child-of-child traversal |
| 4 | `fk-dep-therapy-item-bidirectional` | `FKDependencyExtractStrategy` (`ProcessChildren=true`, `ProcessParents=true`) | `dbo.TherapyProgramItems` `WHERE Id = <MIN(Id)>` (resolved in step 02) | none | yes — up to `TherapyPrograms` (and `Services`, `PatientInsurances`); through `TherapyPrograms` up to `Patients`/`Diagnosis`; down to anything referencing `TherapyProgramItems` | Both-direction walk, parents-before-children ordering, dedup at the join points |
| 5 | `where-filter-during-child-walk` | `OnlyChildrenExtractStrategy` on `dbo.TherapyPrograms`; `TablesToProcess` entry for `dbo.TherapyProgramItems` carries `Where` | `dbo.TherapyPrograms` `WHERE Id = 904` | YES — on the `dbo.TherapyProgramItems` child entry (concrete predicate picked in step 02 from a stable indexed column) | yes (same chain as #3) | Per-table `ExtractStrategy.Where` is appended to child-walk SQL, narrowing the children that get pulled in |
| 6 | `only-parent-cross-schema-chain` | `OnlyParentExtractStrategy` | `hospital.PatientTransfers` `WHERE TransferId = 25` | none | yes — `PatientTransfers` → `PatientPlacements` / `HospitalPlaces` / `Employees` (×2); `PatientPlacements` → `Hospitals` / `Employees` (×2); `HospitalPlaces` → `Hospitals` / `Places` | **Cross-schema walk through 3 schemas** (`hospital` → `dbo`, `hospital` → `organization`); non-`Id` PK column (`TransferId`); every table involved has a globally-unique bare name — avoids the engine's bare-name-resolution limitation |

**Coverage check:**

- [x] `OnlyOneTable` — scenario 1
- [x] `OnlyParent` — scenarios 2, 6
- [x] `OnlyChildren` — scenarios 3, 5
- [x] `FKDependency` — scenario 4
- [x] At least one `Where` filter — scenario 5
- [x] At least one 3+-table chain — scenarios 2, 3, 4, 5, 6

#### Tables the scenario set touches (names step 02's SchemaSurvey.md will detail)

`dbo.Employees`, `dbo.Patients`, `dbo.Payments`, `dbo.PrescriptionIds` (downstream from Payments only), `dbo.TherapyPrograms`, `dbo.TherapyProgramItems`, `dbo.Services`, `dbo.PatientInsurances`, `dbo.Diagnosis`, `hospital.PatientTransfers`, `hospital.PatientPlacements`, `hospital.HospitalPlaces`, `hospital.Hospitals`, `organization.Places`, plus their immediate parents reached via outgoing FKs.

#### FK dependency capture (for DB rebuild)

The scenario set depends on **every FK** whose parent or referenced table is in the set above. Concrete enumeration is captured at step 02 acceptance via a `sys.foreign_keys` snapshot; the snapshot query:

```sql
SELECT
    fk.name,
    OBJECT_SCHEMA_NAME(fk.parent_object_id)     + '.' + OBJECT_NAME(fk.parent_object_id)     AS parent,
    c1.name AS parent_col,
    OBJECT_SCHEMA_NAME(fk.referenced_object_id) + '.' + OBJECT_NAME(fk.referenced_object_id) AS referenced,
    c2.name AS referenced_col
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c1 ON c1.object_id = fkc.parent_object_id     AND c1.column_id = fkc.parent_column_id
JOIN sys.columns c2 ON c2.object_id = fkc.referenced_object_id AND c2.column_id = fkc.referenced_column_id
WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id)     + '.' + OBJECT_NAME(fk.parent_object_id) IN
      ('dbo.Employees','dbo.Patients','dbo.Payments','dbo.TherapyPrograms','dbo.TherapyProgramItems',
       'dbo.Appointments','clinic.Appointments')
   OR OBJECT_SCHEMA_NAME(fk.referenced_object_id) + '.' + OBJECT_NAME(fk.referenced_object_id) IN
      ('dbo.Employees','dbo.Patients','dbo.Payments','dbo.TherapyPrograms','dbo.TherapyProgramItems',
       'dbo.Appointments','clinic.Appointments')
ORDER BY parent, referenced;
```

#### Open items deferred to step 02

- ~~Resolve concrete IDs for scenarios 4 and 6.~~ **Done in step 02:** TherapyProgramItems `Id = 1039`, clinic.Appointments `AppointmentId = 69273`. Captured in [SchemaSurvey.md](../Tests/CharacterisationTests/SchemaSurvey.md).
- ~~Pick a stable predicate for scenario 5's per-table `Where`.~~ **Done in step 02:** `IsCanceled = 0` on `dbo.TherapyProgramItems`. Captured in SchemaSurvey.md.
- For scenario 2, verify all 8 outgoing FKs from `dbo.Payments` chain to bounded result sets — verified in SchemaSurvey.md (all 8 parent tables enumerated; `OnlyParent` strategy keeps each at 1 row per FK).
- For scenario 3, `dbo.TherapyProgramItems WHERE Program_Id = 904` = **2 rows** (verified at step 02 acceptance via direct query). Grandchildren counts will be verified at step 03 (golden-capture time).

#### Step 01 acceptance — status

- [x] Scenario list appended (above), dated.
- [x] All four `ExtractStrategy` variants covered.
- [x] At least one `Where`-filter scenario.
- [x] At least one 3+-level FK chain.
- [x] Each scenario names schema-qualified tables existing in `budzdorov_Core` and a deterministic seed `WHERE` (with two seed IDs deferred to step 02 resolution — explicitly listed under Open items).
- [x] FK list capture mechanism specified (snapshot query above; full snapshot lands at step 02 acceptance).
- [x] **Reviewer (human) sign-off** — confirmed 2026-05-02.

### 2026-05-02 — Step 02 completion

**Step 02 task type:** CONFIG + light CODE (one connectivity test). Followed methodology: implement → verify build → integration test.

**Files added/changed:**

- [Tests/appsettings.test.json](../Tests/appsettings.test.json) — committed test config with `ConnectionStrings:Source` pointing at `budzdorov_Core` on `129.212.168.210,1433`. Disposable dev DB; convention mirrors the CLI's `appsettings.json` (also committed with real connection strings).
- [Tests/Tests.csproj](../Tests/Tests.csproj) — added `Microsoft.Extensions.Configuration` 3.1.0 + `Microsoft.Extensions.Configuration.Json` 3.1.0 (matches CLI versions). Added `<None Update="appsettings.test.json"><CopyToOutputDirectory>Always</CopyToOutputDirectory></None>`.
- [Tests/CharacterisationTests/ConnectivityTests.cs](../Tests/CharacterisationTests/ConnectivityTests.cs) — `[OneTimeSetUp]` loads config; `[Test]` opens `SqlConnection`, runs `SELECT 1`, asserts `1`.
- [Tests/CharacterisationTests/SchemaSurvey.md](../Tests/CharacterisationTests/SchemaSurvey.md) — per-scenario tables, seed IDs, walked-table enumeration, hazard notes, regen instructions.

**Verification:**

- `dotnet build "SQL Buldozer.sln"` — succeeds (warnings are pre-existing: SqlClient CVE — to be addressed by `net10-upgrade`; FParsec arch mismatch; UTF7 obsolete in DSLExecutor.cs).
- `dotnet test "SQL Buldozer.sln" --filter "FullyQualifiedName~ConnectivityTests"` — `Passed: 1` in 648ms.

**FK confirmation — `sys.foreign_keys` snapshot scoped to scenario tables:**

- Total FKs in `budzdorov_Core` today: **489** (across 14 schemas).
- FKs whose parent OR referenced table is one of `dbo.Employees`, `dbo.Patients`, `dbo.Payments`, `dbo.TherapyPrograms`, `dbo.TherapyProgramItems`, `dbo.Appointments`, `clinic.Appointments`: **160**.
- The required FKs for the scenario set's walks are present:
  - `FK_dbo.TherapyProgramItems_dbo.TherapyPrograms_Program_Id` (scenario 3, 4, 5)
  - `FK_dbo.TherapyProgramItems_dbo.Services_MedicalService_Id` (scenario 4)
  - `FK_dbo.TherapyProgramItems_dbo.PatientInsurances_PatientInsuranceId` (scenario 4)
  - `FK_clinic_Appointments_dbo_Appointments` / `Employees` / `dbo_Patients` / `dbo_Services` (scenario 6)
  - `FK_dbo.Payments_dbo.{CashOffices, Employees×2, PatientInsurances, Patients, PrescriptionIds, TherapyCycles, TherapyPrograms}` (scenario 2 — all 8 outgoing FKs)
- Re-run query lives in step 01 of `## Notes`. Re-execute after any DB rebuild and diff the result.

**Step 02 acceptance:**

- [x] `Tests/appsettings.test.json` exists, copied to test output, consumable from `[OneTimeSetUp]`.
- [x] Connectivity NUnit test passes (`Passed: 1`).
- [x] `SchemaSurvey.md` lists, per scenario, exact tables and seed-row WHERE clauses.
- [x] FKs required by the scenario set exist in `budzdorov_Core` (verified via the scoped snapshot above).
- [x] No synthetic fixture SQL committed; no schema setup/teardown — DB is read-only from harness POV.

### 2026-05-02 — Step 03 completion

**Step 03 task type:** CONFIG + CODE (with TDD on `SqlNormalizer` only — pure function; the runner itself is integration-shaped, smoke-tested end-to-end).

**Engine-invocation seam decision (recorded per step 03 spec):** **Option B — programmatic seam**, approved by human. Rationale: the harness binds to engine *interfaces* (`IDependencyBuilder`, `ISourceSchema`, `ISqlBuilder`), not to a container. Survives the upcoming MEF → MS.DI migration unchanged. Mitigation for the "wiring bug escape" risk: `ParserTests.cs` already exercises the F#/C# bridge; a future smoke test through the full CLI composition can be added post-MS.DI.

**Files added:**

- [Tests/CharacterisationTests/Harness/TestDbConfig.cs](../Tests/CharacterisationTests/Harness/TestDbConfig.cs) — single source of truth for `ConnectionStrings:Source`. Fails fast with a pointer to step-02 docs.
- [Tests/CharacterisationTests/Harness/TestUnitOfWorkFactory.cs](../Tests/CharacterisationTests/Harness/TestUnitOfWorkFactory.cs) — minimal `IUnitOfWorkFactory` for tests; wraps `new UnitOfWork(connectionString)`. Avoids depending on the CLI project's `UnitOfWorkFactory` (which is wired for CLI args, MEF, and connection-string-resolver concerns the tests don't need).
- [Tests/CharacterisationTests/Harness/SqlNormalizer.cs](../Tests/CharacterisationTests/Harness/SqlNormalizer.cs) — pure function: CRLF→LF, trailing-whitespace per line stripped, leading/trailing blank lines trimmed, single trailing newline.
- [Tests/CharacterisationTests/Harness/SqlNormalizerTests.cs](../Tests/CharacterisationTests/Harness/SqlNormalizerTests.cs) — 5 NUnit tests covering null/empty input, CRLF normalisation, trailing-whitespace stripping, single-trailing-newline, idempotence.
- [Tests/CharacterisationTests/Harness/CharacterisationRunner.cs](../Tests/CharacterisationTests/Harness/CharacterisationRunner.cs) — wires `MetaDataInitializer` → `ObjectMetaDataProvider` → `MSSQLSourceSchema` (init'd) → `DependencyBuilder` → `MSSqlBuilder`. `RunScenarioAsync(ISourceForScript)` runs walker + builder, returns normalised SQL. All loggers are `NullLogger<T>` (engine logs aren't part of the surface being pinned).
- [Tests/CharacterisationTests/CharacterisationRunnerSmokeTests.cs](../Tests/CharacterisationTests/CharacterisationRunnerSmokeTests.cs) — one `[Test]` exercising scenario 1 (`OnlyOneTable` on `Employees Id=1`) end-to-end. Asserts non-empty SQL containing `Employees`. Not a golden test — that's step 04.

**Files refactored:**

- [Tests/CharacterisationTests/ConnectivityTests.cs](../Tests/CharacterisationTests/ConnectivityTests.cs) — uses `TestDbConfig.ResolveSourceConnectionString()` instead of inline config loading. Single source of truth.

**Test config update (caught during smoke):**

- `Tests/appsettings.test.json` — added `MultipleActiveResultSets=true` to `ConnectionStrings:Source`. Required because `MSSQLSourceSchema.GetMetaData` runs `GetSchemaAsync("Tables")` and a `sqlPKColumns` reader concurrently on the same `SqlConnection` via `Task.WhenAll`. The CLI's existing `appsettings.json` already has MARS on all four committed connection strings — I'd missed it on the test config.

**Verification:**

- `dotnet build "SQL Buldozer.sln"` — 0 errors (12 pre-existing warnings unchanged).
- `dotnet test "SQL Buldozer.sln"` — **11/11 passed** in 3.92s:
  - `Scenario_OnlyOneTable_Employee_Returns_NonEmpty_Sql` (93ms; full engine wiring against `budzdorov_Core`)
  - 5 × `SqlNormalizerTests`
  - `Connects_To_TestDb_And_Selects_One`
  - 4 × `ParserTests` (pre-existing, still green — important: harness work didn't regress the existing F# DSL test path)

**Engine finding flagged for step 04 / step 05 (not blocking step 03):**

The engine resolves table names without schema qualification:

- `MSSQLSourceSchema.Tables` is populated via `SqlConnection.GetSchema("Tables")` rows — `["table_name"]` is the bare table name, schema lives separately.
- `MSSQLSourceSchema.GetTableMetaData(tableName)` does `Tables.First(_ => _.TableName.Equals(tableName, ...))` — bare-name match.
- `_dependentTables` holds `OBJECT_NAME(fk.parent_object_id)` / `OBJECT_NAME(fk.referenced_object_id)` — bare names.
- `DependencyBuilder.GetPTables` runs `select * from {tableName}` literally — SQL Server resolves via the connected user's default schema (`dbo` for `sa`).

**Implications:**

- **Scenarios 1-5 are unaffected** — they touch tables whose names are unique across schemas in `budzdorov_Core` (`Employees`, `Patients`, `Payments`, `TherapyPrograms`, `TherapyProgramItems`).
- **Scenario 6 (`clinic.Appointments AppointmentId = 69273`) hits an engine limitation:** `Appointments` exists in *both* `clinic` and `dbo`. The engine's metadata `.First()` lookup is non-deterministic between the two; `_dependentTables` includes FKs from both schemas; the SELECT against `Appointments` resolves to whichever schema is the user's default (likely `dbo`, *not* `clinic.Appointments`). Pinning this behaviour as-is would lock in a rather dishonest golden.

**Recommendation when step 04 starts:** drop scenario 6, OR replace it with a non-colliding chain (e.g., `clinic.Programs` chain — `clinic.Programs` is uniquely named). Worth a brief decision before step 04 runs. For step 03 itself, this finding doesn't block anything — the smoke test uses scenario 1.

### 2026-05-02 — Scenario 6 replaced (option β chosen)

**Decision:** finish characterisation-tests with a non-colliding S6 chain; engine fix becomes a separate feature (`engine-schema-aware-resolution`) once these tests are in place to act as the regression net.

**S6 was going to be `clinic.Programs` chain** but probing surfaced that `Programs` also exists in `templates` (collision). Replaced with `hospital.PatientTransfers WHERE TransferId = 25`:

- Bare name `PatientTransfers` is globally unique in `budzdorov_Core` (1 occurrence).
- Seed row count: 1.
- Outgoing FKs: 4 → `hospital.PatientPlacements`, `hospital.HospitalPlaces`, `dbo.Employees` (×2 — `CreatedBy`, `DeletedBy`).
- Grand-parent chain reaches `hospital.Hospitals`, `organization.Places`, more `dbo.Employees`. Three-schema chain (`hospital` → `dbo`, `hospital` → `organization`).
- Every reachable table (`PatientPlacements`, `HospitalPlaces`, `Hospitals`, `Places`, `Employees`) has a globally-unique bare name. Engine's bare-name resolution lands on the right tables.

The scenario table above and the "tables touched" list have been updated. Ready for step 04.

### 2026-05-02 — Step 04 + 05 completion

**Steps 04 and 05 effectively merged in execution.** Step 04's golden capture required the scenario registry and the assert/record test fixture, which step 05 also lists as deliverables. Built once, served both steps. No separate work item remained for step 05 beyond a top-level `_README.md`.

**Files added:**

- [Tests/CharacterisationTests/Scenarios.cs](../Tests/CharacterisationTests/Scenarios.cs) — scenario registry. Disabling a scenario = remove a row from `All`. (Step 05 acceptance: one-line change.)
- [Tests/CharacterisationTests/Harness/GoldenStore.cs](../Tests/CharacterisationTests/Harness/GoldenStore.cs) — read/write/mode logic. Walks up from `TestDirectory` to find `Tests.csproj` so goldens are written to source, not bin.
- [Tests/CharacterisationTests/CharacterisationTests.cs](../Tests/CharacterisationTests/CharacterisationTests.cs) — `[TestCaseSource]`-driven fixture. One `Scenario_Matches_Golden(Scenario)` test, parameterised over `Scenarios.All`.
- [Tests/CharacterisationTests/Goldens/_README.md](../Tests/CharacterisationTests/Goldens/_README.md) — regen workflow + fragility caveat.
- [Tests/CharacterisationTests/_README.md](../Tests/CharacterisationTests/_README.md) — top-level pointer (step 05 deliverable).
- 5 × `Tests/CharacterisationTests/Goldens/{name}.sql` — committed pinned outputs.

**Files updated:**

- `Tests/CharacterisationTests/Harness/SqlNormalizer.cs` — adds line-anchored `dd.MM.yyyy HH:mm:ss` regex masking → `__GENERATED_TIMESTAMP__`. The engine emits a header timestamp on its own line (`/* … 02.05.2026 15:04:05 … */`); without masking, every run produces a different golden. Inline data-value timestamps like `31.05.2021 20:37:31 +03:00` inside `values(...)` are deliberately *not* masked — they're real row content.
- `Tests/CharacterisationTests/Harness/SqlNormalizerTests.cs` — 2 new tests: `Header_Style_Timestamp_On_Its_Own_Line_Is_Masked`, `Inline_Data_Timestamp_Inside_Values_Is_Preserved`.

**Scenario 6 deferred.** Recording revealed a deeper engine limitation than the schema-collision finding from step 03: the walker emits `SELECT * FROM {bareName}` literally, which SQL Server resolves via the user's default schema (`dbo` for `sa`). A seed in a non-default schema (`hospital.PatientTransfers`) hits `Invalid object name 'PatientTransfers'`. Cross-schema seeds therefore can't work without the engine fix. Documented in `Scenarios.cs` as a comment; coverage will land in the upcoming `engine-schema-aware-resolution` feature.

**Verification:**

- `dotnet test "SQL Buldozer.sln"` (assert mode, no env vars) — **18/18 passed** in 6.72s. Breakdown: 5 golden tests, 1 connectivity, 1 runner smoke, 7 normaliser, 4 existing parser.
- All 5 goldens: non-empty (1313-3024 bytes), end with `\n`, contain `__GENERATED_TIMESTAMP__` placeholder.
- Record-mode toggle: `BULDOZER_GOLDEN_MODE=record` regenerates all; combined with `BULDOZER_GOLDEN_SCENARIO={name}` regenerates one.
- Existing tests (`ParserTests`) unaffected.

**Step 04 acceptance:**

- [x] One committed golden per scenario (5 of 5 — S6 explicitly deferred).
- [x] Each golden non-empty, ends with `\n`.
- [x] Recording mode regenerates active scenario only (env-var filter).
- [x] `Goldens/_README.md` documents regen workflow in fewer than 20 lines.

**Step 05 acceptance:**

- [x] `dotnet test "SQL Buldozer.sln"` exits 0 with characterisation suite included.
- [x] Disabling a scenario is one-line edit in `Scenarios.All`.
- [x] A single golden mutation produces exactly one failing test with clear actual-vs-expected diff. (Architectural — the failure for `only-one-table-employee` during the timestamp-masking debugging cycle demonstrated this in practice; the assertion message includes the regen command.)
- [x] No existing test breaks (parser tests still 4-of-4 green).

**Feature `characterisation-tests` is complete.** Regression net is in place. Ready to start `net10-upgrade` and (after that) `msdi-migration` and `engine-schema-aware-resolution` with this safety net protecting them.



