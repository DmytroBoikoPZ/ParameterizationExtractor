# Schema Survey — Characterisation Tests

> Per-scenario lookup of which tables, seed rows, and FKs each test exercises against `budzdorov_Core`. Live as of 2026-05-02 (post-FK-population by DBA).
>
> Step 02 deliverable. Authoritative source for: which `WHERE` clause to use, which IDs to seed with, and what to expect downstream when implementing the harness in step 03.

## Test target

- **Server:** `129.212.168.210,1433` (SQL Server 2025 / Ubuntu 24.04 Linux)
- **Database:** `budzdorov_Core`
- **Connection string:** resolved via `Tests/appsettings.test.json` → `ConnectionStrings:Source`
- **Total FKs in DB:** 489 (all schemas)

---

## Scenarios

### 1. `only-one-table-employee`

| Field | Value |
|---|---|
| Strategy | `OnlyOneTableExtractStrategy` (`ProcessChildren=false`, `ProcessParents=false`) |
| Seed root record | `dbo.Employees` `WHERE Id = 1` |
| Seed row count | 1 |
| Per-table `Where` | none |
| Tables walked | just `dbo.Employees` |
| FK chain depth | 0 (single row, no walk) |

**Why this scenario:** baseline. Pins T4 SQL emission for one row, no graph traversal. If this golden breaks, the breakage is in the SQL builder, not the walker.

---

### 2. `only-parent-from-payment`

| Field | Value |
|---|---|
| Strategy | `OnlyParentExtractStrategy` (`ProcessChildren=false`, `ProcessParents=true`) |
| Seed root record | `dbo.Payments` `WHERE Id = 2488889` |
| Seed row count | 1 |
| Per-table `Where` | none |
| Tables walked (direct parents of `dbo.Payments`) | `dbo.CashOffices`, `dbo.Employees` (×2 — `Accountant_Id`, `CashierAccepted_Id`), `dbo.PatientInsurances`, `dbo.Patients`, `dbo.PrescriptionIds`, `dbo.TherapyCycles`, `dbo.TherapyPrograms` |
| FK chain depth | 3+ (e.g. `Payments → TherapyPrograms → Patient_Id → Patients`; `Payments → TherapyPrograms → Diagnosis_Id → Diagnosis → IcdRubrics`) |

**Why this scenario:** multi-FK upward walk. Each FK is N:1 PK lookup (single row), so the walked set is bounded — but the chain is deep because `TherapyPrograms` itself has 7 outgoing FKs and several of those have further parents. Pins per-FK PK lookup, parent-insert ordering, dedup at join points (e.g., the same `Employee` reachable via two different FKs from `Payments`).

**Hazard note:** `Payments` references `dbo.PrescriptionIds` (39M-row hub). Strategy is `OnlyParent` so we walk *up* into one PK (one row); no children of `PrescriptionIds` are visited. Still — verify at golden-capture time that the extracted `PrescriptionIds` row is exactly one.

---

### 3. `only-children-from-therapy-program`

| Field | Value |
|---|---|
| Strategy | `OnlyChildrenExtractStrategy` (`ProcessChildren=true`, `ProcessParents=false`) |
| Seed root record | `dbo.TherapyPrograms` `WHERE Id = 904` |
| Seed row count | 1 |
| Per-table `Where` | none |
| Tables walked (direct children of `dbo.TherapyPrograms`) | `dbo.Payments`, `dbo.Prescriptions`, `dbo.Ratings`, `dbo.ReceptionProtocols`, `dbo.Reminders`, `dbo.TherapyProgramAttachDoctorRecords`, `dbo.TherapyProgramDispensaryData`, `dbo.TherapyProgramItems`, `dbo.TherapyProgramLogRecords` |
| FK chain depth | 2+ (e.g. `TherapyPrograms → TherapyProgramItems(2 rows) → TherapyProgramItemLogRecords`) |

**Verified counts:** `dbo.TherapyProgramItems WHERE Program_Id = 904` returns **2 rows**. Other child counts are unmeasured here; step 03 verifies they stay bounded before goldens are captured.

**Why this scenario:** downward walk, per-table strategy default carry-through, child-of-child traversal. If a child explodes (e.g., `Prescriptions` for this therapy program has thousands of rows), we'll discover it at golden-capture time and either narrow the seed or pick a different therapy program.

---

### 4. `fk-dep-therapy-item-bidirectional`

| Field | Value |
|---|---|
| Strategy | `FKDependencyExtractStrategy` (`ProcessChildren=true`, `ProcessParents=true`) |
| Seed root record | `dbo.TherapyProgramItems` `WHERE Id = 1039` |
| Seed row count | 1 |
| Per-table `Where` | none |
| Tables walked (parents of `TherapyProgramItems`) | `dbo.PatientInsurances`, `dbo.Services` (`MedicalService_Id`), `dbo.TherapyPrograms` (`Program_Id`) |
| Tables walked (children of `TherapyProgramItems`) | `clinic.PrescriptionLinks` (`LegacyItemId`), `dbo.TherapyProgramItemLogRecords` (`TherapyProgramItem_Id`) |
| Cascading | yes — through `TherapyPrograms` we hit its 7 outgoing FKs (parents of parents), reaching `Patients`, `Diagnosis`, etc. |
| FK chain depth | 3+ (e.g. `TherapyProgramItems → TherapyPrograms → Patients`; `TherapyProgramItems → Services → ServiceCategories`) |

**Why this scenario:** both-direction walk, parents-before-children ordering in SQL emission, dedup when the same parent is reached from multiple children.

---

### 5. `where-filter-during-child-walk`

| Field | Value |
|---|---|
| Strategy | `OnlyChildrenExtractStrategy` on `dbo.TherapyPrograms`; `TablesToProcess` entry for `dbo.TherapyProgramItems` carries `Where = "IsCanceled = 0"` |
| Seed root record | `dbo.TherapyPrograms` `WHERE Id = 904` |
| Seed row count | 1 |
| Per-table `Where` | `IsCanceled = 0` on `dbo.TherapyProgramItems` |
| Tables walked | same as scenario 3, but `TherapyProgramItems` rows are filtered to non-cancelled |
| FK chain depth | same as scenario 3 |

**Why this scenario:** pins `DependencyBuilder.GetRelatedTables` line 148-149 — the per-table `ExtractStrategy.Where` is appended to the child-walk SQL (`{pkPredicate} AND {Where}`). If the Where injection logic changes (e.g., parenthesisation, AND/OR precedence), this scenario surfaces it.

**`IsCanceled` chosen because:** boolean column on `dbo.TherapyProgramItems` (visible in `sys.columns` output), clean semantics, deterministic across runs unless someone toggles `IsCanceled` on the test rows (rare on a dev DB).

---

### 6. `only-parent-cross-schema-chain`

| Field | Value |
|---|---|
| Strategy | `OnlyParentExtractStrategy` |
| Seed root record | `hospital.PatientTransfers` `WHERE TransferId = 25` |
| Seed row count | 1 |
| Per-table `Where` | none |
| Tables walked (direct parents of `hospital.PatientTransfers`) | `hospital.PatientPlacements` (`PlacementId → PlacementId`), `hospital.HospitalPlaces` (`HospitalPlaceId → HospitalPlaceId`), `dbo.Employees` (`CreatedBy → Id`), `dbo.Employees` (`DeletedBy → Id`) |
| Cascading | through `hospital.PatientPlacements` → `hospital.Hospitals` + `dbo.Employees` (×2). Through `hospital.HospitalPlaces` → `hospital.Hospitals` + `organization.Places`. |
| FK chain depth | 3+ across **three schemas** (`hospital`, `dbo`, `organization`) |

**Why this scenario:** cross-schema walk through three different schemas (`hospital` → `dbo`, `hospital` → `organization`); non-`Id` PK column (`TransferId`); exercises the `OnlyParent` strategy on a non-`dbo` seed.

**Engine compatibility note:** every table reached by this scenario has a globally-unique bare name (`PatientTransfers`, `PatientPlacements`, `HospitalPlaces`, `Hospitals`, `Places`, `Employees` — verified via `sys.tables` GROUP BY name HAVING COUNT(*)=1). The current engine's bare-name resolution lands on the correct table for each. A future schema-aware engine (separate feature) will produce a different golden — at which point this golden gets regenerated through the record-mode toggle.

---

## Open verification at step 03 (golden-capture time)

For each scenario:

1. Run the harness in record mode.
2. Confirm the produced SQL row count is small (target: under ~50 rows total per scenario for goldens that diff cleanly).
3. If a scenario explodes, narrow the seed (e.g., use a different `Program_Id` than 904 with fewer items) — but **do not** change the strategy or Where shape; that would change what the scenario pins.
4. Capture the actual golden into `Tests/CharacterisationTests/Goldens/{scenario-name}.sql`.

---

## Re-running this survey after a DB rebuild

The walker reads `sys.foreign_keys`. If the DB is rebuilt:

1. Re-add the 489 FKs (their definitions live in the DBA's setup script, not in this repo).
2. Re-run the FK-snapshot query in `## Notes` of [characterisation-tests-checklist.md](../../../ongoing-tasks/characterisation-tests-checklist.md).
3. Re-verify each scenario's seed row exists and walked-table sizes are unchanged. Update IDs if they shifted.
