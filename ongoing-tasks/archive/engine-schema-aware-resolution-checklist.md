# Engine — Schema-Aware Resolution

## Goal

Engine resolves table names against the actual SQL schema (today: bare names via the SQL user's default schema). Adds a `Schema` field to `PTable` / `TableToExtract`; foreign-key resolution, dependency walking, and SQL emission all become schema-aware. Unblocks the cross-schema characterisation scenario deferred during the bootstrap-era characterisation feature, and is a precondition for `desktop-seed-tab`'s `Schema.TableName` picker.

Backward-compat is non-negotiable: every existing `.xml` / `.bws` workspace continues to load and produce identical SQL when its `Schema` is empty / null. Empty Schema means "fall back to SQL's default-schema resolution" (today's behaviour).

## Scope

- **In scope:**
  - **ADR-011 — engine schema-awareness semantics** — formalises: (a) `Schema` is an optional field that defaults to empty/null; (b) empty Schema means "use the SQL user's default schema" (preserves today's behaviour); (c) two `PTable` instances are equal iff `(Schema, Name)` are equal — empty-schema entries are *not* unified with default-schema-resolved entries (they remain "ambiguous, defer to SQL"); (d) `MSSQLSourceSchema.GetMetaData` always populates Schema from `sys.schemas` for **discovered** tables (so resolved entries always have a concrete Schema even when the operator wrote bare names). Records: status, context, decision, consequences (XML/JSON wire compat, T4 emission rules).
  - **Engine model — `Schema` field** — `PTable` and `TableToExtract` (and any other model class that names a table) gain `Schema` (string, default `""`). Equality comparers updated to use `(Schema, Name)` tuple. **No** `record` conversion; just additional property + updated `Equals`/`GetHashCode`.
  - **XML serialization** — optional `[XmlAttribute("schema")]` on the relevant model classes; missing attribute → empty string; existing fixtures continue to deserialise byte-identically.
  - **JSON serialization** — optional `schema` property (camelCase via `JsonOptions.Default`); missing → null → normalised to empty string by the constructor.
  - **`MSSQLSourceSchema.GetMetaData`** — joins `sys.tables`/`sys.foreign_keys` with `sys.schemas`; populates Schema on every discovered `PTable`. Bare-name lookups (operator wrote `"Patient"` but the engine discovered `dbo.Patient`) match via the lookup helper that treats empty-Schema config entries as "match any discovered schema if name is unique; throw if ambiguous" — pinned by tests.
  - **Dependency walking** — `DependencyBuilder` uses `(Schema, Name)` tuples for FK target resolution; cross-schema FKs that already exist in a schema-rich source DB resolve correctly.
  - **T4 SQL emission** — when `Schema` is non-empty, emits `[Schema].[Table]`; when empty, emits `[Table]` (today's behaviour). Identity-insert / use-database / DELETE statements all updated symmetrically.
  - **Lookup helper consolidation** — wherever code currently does `Tables.FirstOrDefault(t => t.Name == x)` to resolve a config-supplied name, it goes through a single `ResolveTable(Schema, Name)` helper on `MSSQLSourceSchema` (or wherever the lookup currently lives) that implements the bare-name-matching policy. Avoids drift between callers.
  - **Cross-schema characterisation scenario** — the deferred test from `characterisation-tests` lands here. Seed query reaches across `dbo` and a non-default schema (test DB needs a second schema; if the test DB doesn't have one, the test creates and drops it via `[OneTimeSetUp]`/`[OneTimeTearDown]` — guarded so the test still passes if the operator lacks `CREATE SCHEMA` rights, by skipping with a message). Golden SQL pinned.
  - **Characterisation parity for all existing scenarios** — every existing golden must continue to match byte-for-byte when the new `Schema` field is empty (the operator's existing XML/`.bws` files work unchanged). Verified by running the existing 5 scenarios × 2 loaders = 10 cases unchanged.
  - **Cross-doc updates** — `adr/readme.md` (index ADR-011); `docs/architecture/overview.md` § 3.2 mentions schema-aware traversal; `docs/methodology/dotnet-cli.md` notes the new `Schema` field on engine model types; `docs/roadmap.md` (move feature to Completed; promote `desktop-seed-tab` to Proposed next).

- **Out of scope:**
  - **Cross-database FK references** — still single-DB scope. A `PTable` belongs to one database.
  - **Schema-rename detection** — operator must update their config if a schema is renamed mid-flight; we don't auto-detect.
  - **Migrating existing fixtures** — no rewrites of existing `.xml` / `.bws` files in the repo. They keep `Schema` empty and continue to behave identically.
  - **CLI flag for default-schema override** — operator can already set the default via `User Id` in the connection string; we don't add a separate `--default-schema` knob.
  - **Schema-prefixed table names in a single attribute** (`Name="dbo.Patient"`) — explicitly rejected. Use the dedicated `Schema` attribute. Pinned by an XML round-trip test that asserts `Name="dbo.Patient"` is treated as a single-token name (not parsed) so behaviour is unsurprising.
  - **Ambiguity heuristics** — when a bare-name config entry could match two schemas in the discovered metadata, we throw (with a clear "specify Schema=..." message). No "guess based on default schema" magic.

- **Dependencies:**
  - [`characterisation-tests`](./archive/characterisation-tests-checklist.md) — the deferred cross-schema scenario lives there as a stub / TODO.
  - Test DB: needs a second schema for the cross-schema scenario. Setup via `[OneTimeSetUp]` if absent.
  - All downstream desktop features (`desktop-seed-tab` onwards) implicitly depend on this — schema-prefixed table picker is impossible without it.

## Architecture

See [feature-architecture.md](./engine-schema-aware-resolution/feature-architecture.md) for the model changes, lookup-policy details, and the SQL-emission rules.

## Steps

- [x] [01 — ADR-011 + Schema field on engine model + serialization](./engine-schema-aware-resolution/01-engine-schema-aware-resolution-model-and-adr.md)
- [x] [02 — MSSQLSourceSchema reads schema; bare-name lookup helper](./engine-schema-aware-resolution/02-engine-schema-aware-resolution-metadata-and-lookup.md)
- [x] [03 — Dependency walking + FK resolution use (Schema, Name) tuples](./engine-schema-aware-resolution/03-engine-schema-aware-resolution-dependency-walk.md)
- [x] [04 — T4 template emits [Schema].[Table] when Schema non-empty](./engine-schema-aware-resolution/04-engine-schema-aware-resolution-t4-emission.md)
- [x] [05 — Cross-schema characterisation scenario + parity verification](./engine-schema-aware-resolution/05-engine-schema-aware-resolution-characterisation.md)
- [x] [06 — Cross-doc updates](./engine-schema-aware-resolution/06-engine-schema-aware-resolution-cross-docs.md)

## Notes

### 2026-05-03 — Step 01 (ADR-011 + Schema field + serialization) complete

**New files:**

- `adr/011-engine-schema-awareness.md` — formalises throw-on-ambiguity policy. Records considered alternatives (silent default-to-dbo, query SCHEMA_NAME(), composite Name parsing) and why each was rejected.
- `Tests/EngineModelTests/PTableSchemaTests.cs` — 3 tests (default empty, equality tuple semantics, hash-code-matches-equality).
- `Tests/EngineModelTests/TableToExtractSerializationTests.cs` — 6 tests (XML legacy + round-trip empty + round-trip non-empty; JSON legacy + round-trip empty + round-trip non-empty).

**Modified files:**

- `adr/readme.md` — ADR-011 indexed.
- `ParameterizationExtractor.Logic/Model/RootRecord.cs` — `TableToExtract.Schema` + `RecordsToExtract.Schema` (`string`, default null). `[XmlAttribute("schema")] [DefaultValue("")]` for XML; `[JsonPropertyName("schema")] [JsonIgnore(Condition = WhenWritingNull)]` for JSON. `AsString()` updated to emit `"Schema.Table"` when Schema present.
- `ParameterizationExtractor.Logic/Model/PMetadata.cs` — `PTableMetadata.Schema` (string, default empty), `Equals`/`GetHashCode` overrides via `(Schema, TableName)` tuple, OrdinalIgnoreCase. `PDependentTable` gains `ParentSchema` + `ReferencedSchema` for step 03's FK resolution.

**Audit grep — Name-bearing model classes:**

Four classes name a table; all four touched in step 01:
- `TableToExtract.TableName` (config) — Schema added
- `RecordsToExtract.TableName` (config / seed selector) — Schema added
- `PTableMetadata.TableName` (discovered) — Schema added + Equals/GetHashCode override
- `PDependentTable.{Parent,Referenced}Table` (FK relation) — both Schema fields added

`PColumn`, `PRecord`, `PField` name columns/rows, not tables — skipped per spec.

**Decision deviation from spec:**

Schema property type is `string` defaulting to **null** (not `""`). Reason: `JsonIgnoreCondition.WhenWritingDefault` doesn't suppress emission when CLR default for `string` is `null`; setting `Schema = ""` always serializes. Switched to `WhenWritingNull` and treat null/empty as equivalent throughout. Callers already use `string.IsNullOrEmpty(Schema)` (the natural pattern); equality normalizes via `?? string.Empty`. Test #1 (`Default_Schema_IsEmptyString`) revised to `BeNullOrEmpty()`.

**Verification:**

- `dotnet build` — 0 errors (cleared docfx incremental cache once on first run; recurring flake).
- `dotnet test` — **144/144 pass** (was 135; +9, exactly per projection).
- Tripwire grep across new/modified files: 0 hits for `MessageBox.Show`, `IConfiguration[`, `Console.WriteLine`, `Thread.Sleep`, `.Result`, `.Wait()`, `Dispatcher.Invoke`, `TODO`, `FIXME`. No new `Microsoft.Data.SqlClient` references in non-Logic projects.
- All existing 135 tests still pass — additive changes only; no semantic break.

### 2026-05-03 — Step 02 (MSSQLSourceSchema reads schema + bare-name lookup helper) complete

**New files:**

- `Logic/MSSQL/AmbiguousTableException.cs` — `public sealed`. Message names candidate schemas alphabetised + quoted. Carries `TableName` + `CandidateSchemas` for caller introspection.
- `Logic/MSSQL/TableResolver.cs` — `public static class`. Pure function over `IEnumerable<PTableMetadata>` + `(schema, name)`. **Decision deviation:** extracted to a static helper instead of staying inline on `MSSQLSourceSchema` (spec said "keep on MSSQLSourceSchema for v1") — the static helper makes unit-testing trivial without constructing the heavy DI graph. `MSSQLSourceSchema.ResolveTable` delegates one-line.
- `Tests/EngineSchemaAwareResolution/TableResolverTests.cs` — 6 unit tests.
- `Tests/EngineSchemaAwareResolution/MSSQLSourceSchemaIntegrationTests.cs` — 4 integration tests against live test DB.

**Modified files:**

- `Logic/Interfaces/ISourceSchema.cs` — added `ResolveTable(string schema, string tableName)` to the interface.
- `Logic/Model/StubSourceSchema.cs` — added `ResolveTable` impl returning `null`.
- `Logic/MSSQL/MSSQLSourceSchema.cs` — added `ResolveTable`; `GetMetaData` reads `t["table_schema"]` from `GetSchema("Tables")` DataTable. Defensive guard via `t.Table.Columns.Contains("table_schema")` against any provider that omits the column.
- `Logic/MSSQL/ObjectMetaDataProvider.cs` — `sqlFKs` query gains `OBJECT_SCHEMA_NAME(...)` for both parent + referenced. Populator gains the same column-existence guard.

**Verification:**

- `dotnet build` — 0 errors (cleared docfx cache once on first run).
- `dotnet test` — **154/154 pass** (was 144; +10 = 6 unit + 4 integration; spec projected +8 — added 2 extras for FK schemas + bare-name round-trip on real DB).
- All existing 144 tests still pass — discovered metadata now carries Schema, but emission paths unchanged (steps 03-04).
- Tripwire grep across new files: 0 hits for forbidden patterns.

### 2026-05-03 — Step 03 (Dependency walking + FK resolution via (Schema, Name)) complete

**Modified files:**

- `Logic/Model/PTable.cs` — added `PRecord.Schema` getter (delegates to `_metaData.Schema`).
- `Logic/Helpers/ConfigHelper.cs` — `GetTableToExtract(discoveredSchema, tableName, template)` overload added. Empty config Schema matches any discovered schema (back-compat); explicit config Schema requires exact case-insensitive match. Bare-name overload preserved as a delegator.
- `Logic/MSSQL/DependencyBuilder.cs` — full rewrite of internal methods. `processTable`, `GetExtractStrategy`, `GetSqlBuildStrategy`, `insertTable`, `PrepareTableMetaData`, `GetPTable`, `GetPTables` all gain schema parameters. `GetRelatedTables` matches FKs via new `FkEndMatches(fkSchema, fkTable, record)` helper. SELECT statements emit `[Schema].[Table]` when discovered Schema non-empty; bare `[Table]` otherwise (preserves legacy behaviour). Helper: `QualifyForSelect(meta)`. External `GetPTable`/`GetPTables` signatures preserved as overloads.

**Audit grep — touchpoints classified:**

- `Tables.First/FirstOrDefault(t => t.TableName...)` in `MSSQLSourceSchema.GetTableMetaData` — left as-is (back-compat shim; no schema info at call site). New code goes through `ResolveTable`.
- `_schema.DependentTables.Where(_ => _.ParentTable.Equals(...))` in `DependencyBuilder` — replaced via `FkEndMatches`.
- `ConfigHelper.GetTableToExtract(tableName, template)` — overload preserved; new schema-aware overload preferred at all DependencyBuilder call sites.

**Decision deviations:**

- `FkEndMatches` policy: empty schemas (either side) degrade to bare-name match. Reason: `MSSQLSourceSchema` always populates discovered Schema after step 02, but other ISourceSchema impls (e.g. `StubSourceSchema`) may not. Keeps test doubles working.
- `PRecord.ToString()` (used by Equals for dedup) intentionally NOT modified to include Schema — would change existing dedup keys. Cross-schema PK collision is unlikely; step 05 will verify.

**Verification:**

- `dotnet build` — 0 errors.
- `dotnet test` — **157/157 pass** (was 154; +3 exactly per projection).
- All 10 existing characterisation cases pass byte-identically — bare-name back-compat verified end-to-end.
- Tripwire grep across modified files: 0 hits for forbidden patterns.

### 2026-05-03 — Step 04 (T4 template emits [Schema].[Table] when non-empty) complete

**Approach:** `EmissionSchema` is sourced from the operator's *config* `TableToExtract.Schema` (architecture § 3 invariant), NOT from discovered metadata. Empty EmissionSchema → bare `TableName` preserves byte-identical legacy goldens.

**Modified files:**

- `Logic/Model/PTable.cs` — added `PRecord.EmissionSchema` (default empty). Set by DependencyBuilder from config.
- `Logic/Helpers/SqlHelper.cs` — added 4 helpers: `Qualify(emissionSchema, tableName)`, `Qualify(PRecord)`, `QualifyForDeleter(emissionSchema, tableName)`, `QualifyForDeleter(PRecord)`. The string-overload primary is trivially testable; PRecord overload delegates.
- `Logic/MSSQL/DependencyBuilder.cs` — `GetPTable` / `GetPTables` set `record.EmissionSchema = ConfigHelper.GetTableToExtract(meta.Schema, name, template)?.Schema ?? string.Empty;`.
- `Logic/Templates/DefaultTemplate.cs` — bulk replace via sed: 34 emission sites switched from `record.TableName` / `parent.TableName` / `child.TableName` / `parent.PRecord.TableName` / `table.TableName` to corresponding `SqlHelper.Qualify(...)` calls. 2 `exec Deleter @TableName='...'` sites use `QualifyForDeleter` (Deleter SP expects unbracketed).
- `Logic/Templates/DefaultTemplate.tt` — synced to match `.cs`.

**New file:**

- `Tests/EngineSchemaAwareResolution/SqlEmissionQualifyTests.cs` — 5 tests (Qualify empty/null/non-empty + QualifyForDeleter empty/non-empty).

**Decision deviations:**

- Initial Qualify(PRecord) signature blocked testing (PRecord requires live IDataRecord). Refactored to `Qualify(string, string)` primary + PRecord overload. Cleaner regardless.
- Two helpers (Qualify + QualifyForDeleter) instead of one. Deleter SP needs unbracketed; SQL emission needs bracketed.

**Verification:**

- `dotnet build` — 0 errors.
- `dotnet test` — **162/162 pass** (was 157; +5; spec projected +2, added 3 extras).
- All 10 characterisation goldens byte-identical — back-compat verified. `Qualify("", x) == x` mathematically — no drift possible for legacy.
- Tripwire grep across modified files: 0 hits for forbidden patterns.

### 2026-05-03 — Step 05 (Cross-schema characterisation scenario) complete

**New / modified files:**

- `Tests/CharacterisationTests/CharacterisationTests.cs` — `[OneTimeSetUp]` extended with `EnsureCrossSchemaFixtureAsync(connStr)`. Creates `audit` schema + `audit.SchemaTestParent` + `dbo.SchemaTestChild` + FK + seed rows idempotently. Catches `SqlException` and sets `CrossSchemaAvailable = false` (logs to TestContext) so the rest of the suite still runs if perms missing. The `cross-schema` scenario test calls `Assert.Ignore` when unavailable.
- `Tests/CharacterisationTests/Scenarios.cs` — added `BuildCrossSchema()` and registered as the 6th scenario. Cleared the old "deferred" comment.
- `Tests/CharacterisationTests/Json/cross-schema.json` — JSON loader fixture; mirrors the programmatic build.
- `Tests/CharacterisationTests/Goldens/cross-schema.sql` — recorded via `BULDOZER_GOLDEN_MODE=record BULDOZER_GOLDEN_SCENARIO=cross-schema`.

**Recorded golden highlights:**

- `select * from [dbo].[SchemaTestChild] where Id = 100` (DependencyBuilder qualified SELECT — step 03 work).
- `select * from [audit].[SchemaTestParent] where [Id] = 1` (FK-walked target SELECT, qualified).
- `insert into [dbo].[SchemaTestChild] ...` and `insert into [audit].[SchemaTestParent] ...` (T4 qualified emission — step 04 work).
- Header timestamp normalised via existing `SqlNormalizer` regex.

**Verification:**

- Test DB: SA permissions confirmed → `CrossSchemaAvailable = true` → scenario runs (not skipped).
- `dotnet build` — 0 errors.
- `dotnet test` — **164/164 pass** (was 162; +2 for cross-schema × Programmatic + Json loaders; exactly per projection).
- All 10 existing characterisation cases byte-identical — bare-name back-compat preserved end-to-end.
- Recorded golden replays byte-identically on the second run (no drift).
- Tripwire grep across modified files: 0 hits for forbidden patterns.

**Out of band note:** The cross-schema fixture leaves `audit.SchemaTestParent` + `dbo.SchemaTestChild` in the test DB after the run (idempotent reuse on subsequent runs). No tear-down by design — concurrent test runs are safe.

### 2026-05-03 — Step 06 (cross-doc updates) complete

- `docs/architecture/overview.md` § 3.2 — engine pipeline now mentions `(Schema, Name)` tuple resolution + `[Schema].[Table]` emission rule. Also corrected lingering `System.Data.SqlClient` → `Microsoft.Data.SqlClient` (post `replace-stale-deps`).
- `docs/architecture/overview.md` § 7 — added ADR-011 entry with the bare-name policy summary.
- `docs/methodology/dotnet-cli.md` § SQL generation — extended with the schema-awareness paragraph + the recipe-level "don't compare table names without schema" rule.
- `docs/methodology/workspace-format.md` — sample now shows one root-record with `schema: "audit"` to illustrate the optional field; correspondence table gains rows for `schema` on both `rootRecords` and `tablesToProcess`.
- `docs/roadmap.md` — `engine-schema-aware-resolution` moved to ✅ Completed; `desktop-seed-tab` is Proposed next + Desktop UI track top (dependency note removed since it's satisfied).
- `CLAUDE.md` SHA256 verified `f0fe5df9b8fa4d99c25de32a13849e6814a07887098ac365a11b1513e8c52649` — unchanged from `desktop-connection-management`. Equal to `.github/copilot-instructions.md`. No new tripwire surfaced (recipe-level rule was sufficient per the schema-aware case).
- `dotnet build` 0 errors; `dotnet test` 164/164 (sanity).

## Final summary

The `engine-schema-aware-resolution` feature is complete: 6/6 steps, 164/164 tests green.

**Cumulative changes:**

- **2 new files** (Logic): `MSSQL/{AmbiguousTableException.cs, TableResolver.cs}`.
- **5 modified files** (Logic): `Model/{PTable.cs, PMetadata.cs, RootRecord.cs}` (Schema fields + EmissionSchema + Equals), `MSSQL/MSSQLSourceSchema.cs` + `MSSQL/ObjectMetaDataProvider.cs` (metadata reads schema; SQL queries gain `OBJECT_SCHEMA_NAME` / `sys.schemas` join), `MSSQL/DependencyBuilder.cs` (full rewrite for schema-aware tuple traversal), `Helpers/{ConfigHelper.cs, SqlHelper.cs}` (schema-aware lookups + Qualify helpers), `Templates/DefaultTemplate.cs` + `Templates/DefaultTemplate.tt` (sed bulk-replace; 34 emission sites switched to Qualify).
- **2 modified Interface/Stub files** (Logic): `Interfaces/ISourceSchema.cs` + `Model/StubSourceSchema.cs` (added `ResolveTable` to interface + stub impl).
- **1 new ADR**: `adr/011-engine-schema-awareness.md`; `adr/readme.md` index updated.
- **5 new test files** (Tests): `EngineModelTests/{PTableSchemaTests.cs, TableToExtractSerializationTests.cs}`; `EngineSchemaAwareResolution/{TableResolverTests.cs, MSSQLSourceSchemaIntegrationTests.cs, DependencyBuilderResolveTests.cs, SqlEmissionQualifyTests.cs}`.
- **1 new characterisation scenario** + **1 modified test fixture**: `Tests/CharacterisationTests/CharacterisationTests.cs` (`[OneTimeSetUp]` extended with idempotent cross-schema fixture creation), `Tests/CharacterisationTests/Scenarios.cs` (BuildCrossSchema added). New JSON fixture + recorded golden.
- **3 modified doc files**: `docs/architecture/overview.md`, `docs/methodology/dotnet-cli.md`, `docs/methodology/workspace-format.md`, `docs/roadmap.md` (4 actually).

**Net test delta:** 135 → 164 (+29: 9 model/serialization + 10 lookup/metadata + 3 dependency-walk + 5 emission + 2 cross-schema characterisation).

**Pattern continued:** schema-awareness threaded everywhere via tuple-aware lookups; back-compat preserved by treating empty Schema as "any" on the config side and "use SQL default" on the lookup side. T4 emission decision matrix locked: emission reads from config Schema (preserves byte-identical legacy goldens), discovered Schema is for FK resolution / metadata.

**Headline back-compat:** all 10 existing characterisation cases (5 scenarios × 2 loaders) byte-identical end-to-end. The recorded cross-schema golden replays cleanly. The bulk sed replace was mathematically safe because `Qualify("", x) == x`.

**Pending action:** archive needs your nod (per `prompts/execution.md` § Completion). Pair to move: `ongoing-tasks/engine-schema-aware-resolution-checklist.md` + `ongoing-tasks/engine-schema-aware-resolution/` → `ongoing-tasks/archive/`.

Free-form log of decisions, blockers, links to PRs. Dated entries preferred (YYYY-MM-DD).
