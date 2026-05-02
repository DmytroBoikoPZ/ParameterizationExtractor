# Characterisation Tests — Architecture Overview

> Internal-test feature. No production data flow change. The "architecture" here is the test harness shape, where the test config + goldens live, and what the test target is.

---

## 1. Pipeline / Integration

The feature adds a new test path inside the existing `Tests/` NUnit project. It exercises the existing engine without modifying it, against the real dev SQL Server. No fixture creation — the DB is treated as a stable, read-only test target.

```
Tests/CharacterisationTests/
        │
        ▼
[Arrange]   load Tests/appsettings.test.json → resolve connection string
        │
        ▼
[Act]       run the engine (Logic) against scenario-specific ExtractConfig inputs
            engine connects to budzdorov_Core, walks per declared relations, emits SQL
        │
        ▼
[Assert]    compare emitted SQL string to committed golden file (after normalisation)
```

| Stage | What happens | Where |
|-------|--------------|-------|
| 1. Setup | Resolve connection string from `Tests/appsettings.test.json` | `Tests/CharacterisationTests/Harness/FixtureDatabase.cs` |
| 2. Execute | Drive the engine via the same composition the CLI uses; pass scenario's `ExtractConfig` | `Tests/CharacterisationTests/Scenarios/*.cs` |
| 3. Capture | Stringify the generated SQL output | (in-test, normalised — line endings, trailing whitespace, transient tokens) |
| 4. Compare | `Assert.That(actual, Is.EqualTo(File.ReadAllText(goldenPath)))` | `Tests/CharacterisationTests/Goldens/*.sql` |

---

## 2. Component Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│  Tests/ (NUnit)                                                     │
│                                                                     │
│  ┌──────────────────────────┐        ┌───────────────────────────┐  │
│  │ CharacterisationTests/   │───────►│  Logic engine (existing)  │  │
│  │   - Configs/   (NEW)     │        │  + DSL.Connector / DSL    │  │
│  │   - Scenarios/ (NEW)     │        └─────────────┬─────────────┘  │
│  │   - Goldens/   (NEW)     │                      │                 │
│  │   - Harness/   (NEW)     │                      │ SqlClient       │
│  │   - appsettings.test.json│                      ▼                 │
│  └──────────────────────────┘     ┌──────────────────────────────┐  │
│                                   │  budzdorov_Core (SQL Server  │  │
│                                   │  2025 / Ubuntu 24.04;        │  │
│                                   │  129.212.168.210,1433)       │  │
│                                   │  read-only from test POV     │  │
│                                   └──────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

All components except the four `Tests/CharacterisationTests/*` directories already exist.

---

## 3. Data Flow

### 3.1 Inbound / Ingestion

`Tests/appsettings.test.json` is committed (mirroring the CLI's `appsettings.json` pattern). Holds the dev-DB connection string. `[OneTimeSetUp]` resolves the string via `Microsoft.Extensions.Configuration` and hands it to the engine.

No fixture-application step — the DB schema and data are the existing dev DB. Tests do **not** mutate it.

### 3.2 Processing / Event Handling

Each scenario provides its own `ExtractConfig`-shaped input (in-process construction, or a small `.xml` per scenario in `CharacterisationTests/Configs/`). The engine reads relations from `sys.foreign_keys` on `budzdorov_Core` — the FKs the scenarios depend on are added to the DB by the DBA before step 01 finishes. The harness passes the config to the same engine entry point the CLI uses and captures the emitted SQL string.

### 3.3 Query / Serving

N/A — outputs are compared to golden files in-test.

---

## 4. Data Stores Summary

| Store | Technology | Collections / Tables / Indices | Purpose |
|-------|------------|-------------------------------|---------|
| Test DB | SQL Server 2025 (Linux) at `129.212.168.210,1433`, database `budzdorov_Core` | Real ClinicV2 schema, ~370 tables across 17 schemas; FKs added out-of-band for scenario coverage | Source DB for engine extraction |
| Goldens | Filesystem (`.sql` files) | One per scenario | Pinned expected output |
| Test config | Filesystem (`Tests/appsettings.test.json`) | `ConnectionStrings:Source` | Connection string source |

---

## 5. Extension Points

Adding a new scenario = one config snippet + one scenario class + one golden file. If the new scenario walks a relation that doesn't yet have an FK in `budzdorov_Core`, that FK has to be added to the DB first — track it in step 01's FK list.

Strategy variants the scenario set must collectively reach: `FKDependency`, `OnlyChildren`, `OnlyParent`, `OnlyOneTable`, plus at least one `Where`-filtered case.

---

## 6. Security & Isolation

- Test DB credentials are committed in `Tests/appsettings.test.json`. The DB is intentionally disposable — leakage is acceptable. Mirrors the CLI's existing `appsettings.json` convention.
- Tests are read-only against the DB. Any mutation in the engine code path against this DB during a test run is a bug — flag it.
- Test infrastructure must never point at a production DB. The connection string in `appsettings.test.json` is the dev DB by convention; replacing it with a production DB requires explicit ADR/decision.
