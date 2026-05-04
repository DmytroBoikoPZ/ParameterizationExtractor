# Engine — Schema-Aware Resolution — Architecture Overview

> First feature to extend the core engine model since the bootstrap-era. Adds a `Schema` field across `PTable` / `TableToExtract`; teaches the lookup, FK resolution, and SQL emission paths to use `(Schema, Name)` tuples while preserving 100% backward compat for empty-Schema config.

---

## 1. Pipeline / Integration

```
Operator config (XML or JSON)                       (existing inputs)
  ┌────────────────────────────┐
  │ TableToExtract { Name }    │   today: name only
  │ TableToExtract {           │   ────────────────────────────────►  AFTER
  │   Schema?, Name            │   tomorrow: optional schema field
  │ }                          │
  └─────────────┬──────────────┘
                │
                ▼
  MSSQLSourceSchema.GetMetaData(connection)        (existing, modified)
                │
                │   joins sys.tables × sys.schemas
                │   produces PTable instances WITH concrete Schema
                ▼
  ResolveTable(configEntry, discoveredTables)      (NEW helper)
                │
                │   if configEntry.Schema is non-empty:
                │       look up by (Schema, Name) — exact match
                │   else (bare name):
                │       count matches by Name across all schemas
                │         0 → not-found error
                │         1 → resolves to that PTable (preserves today's behaviour)
                │         2+ → throws ambiguity error with the matching schemas listed
                ▼
  DependencyBuilder.PrepareAsync                   (existing, modified)
                │
                │   FK target lookup uses (Schema, Name) tuples
                ▼
  SqlBuilder + T4 template                         (existing, modified)
                │
                │   emits [Schema].[Table] if Schema non-empty
                │   emits [Table]           if Schema empty (today's behaviour)
                ▼
  .sql output                                      (existing format, schema-prefixed where applicable)
```

| Stage | What changes | Where |
|-------|--------------|-------|
| Model | `PTable`, `TableToExtract` gain `Schema` (string) | `Logic/Model/{PTable.cs, TableToExtract.cs}` |
| XML serialization | `[XmlAttribute("schema")]` on the modified types | same files |
| JSON serialization | `schema` camelCase property; default null → "" | same files (already opt-in via shared `JsonOptions.Default`) |
| Equality / hashing | `(Schema, Name)` tuple semantics | `PTable.Equals` / `GetHashCode` |
| Metadata read | `sys.schemas` join | `Logic/MSSQL/MSSQLSourceSchema.cs`, `Logic/MSSQL/MetaDataInitializer.cs` |
| Lookup | New `ResolveTable(schema, name)` helper | `Logic/MSSQL/MSSQLSourceSchema.cs` (or `Logic/Helpers/`) |
| Dependency walk | `(Schema, Name)` tuples for FK targets | `Logic/MSSQL/DependencyBuilder.cs` |
| SQL emission | `[Schema].[Table]` when Schema non-empty | `Logic/Templates/DefaultTemplate.tt` (regen `.cs`) |

---

## 2. Backward-compat policy (locked)

**Empty `Schema` is the existing-config marker.** Every legacy `.xml` and every existing `.bws` will round-trip through XML / JSON serialization with `Schema = ""`. The `ResolveTable` helper treats empty-Schema config entries via the bare-name-matching policy below — this preserves today's behaviour exactly when the discovered metadata only has the table in one schema (the overwhelming common case).

**Bare-name matching policy** (`ResolveTable("", name, discoveredTables)`):

1. Filter discovered tables to those with `t.Name == name` (case as-is).
2. If the count is 0 → throw `TableNotFoundException(name)`.
3. If the count is 1 → return that table (this preserves today's behaviour for single-schema operators).
4. If the count is ≥ 2 → throw `AmbiguousTableException(name, schemas: ["dbo", "audit", ...])` with a message that names the discovered schemas. The operator's resolution is to specify `Schema="..."` in their config.

**Schema-qualified matching** (`ResolveTable(schema, name, discoveredTables)` with `schema != ""`):

1. Filter discovered tables by `(t.Schema, t.Name) == (schema, name)`.
2. 0 → `TableNotFoundException(schema, name)`.
3. 1 → that table. (Cannot be ≥2; `(schema, name)` is unique in `sys.tables`.)

This policy is enforced by tests in step 02 (`MSSQLSourceSchemaResolveTests`).

---

## 3. SQL emission rules

**T4 template (`DefaultTemplate.tt`)** emits identifiers via a single helper `Qualify(PTable)`:

```
Qualify(PTable t):
  if string.IsNullOrEmpty(t.Schema): return $"[{t.Name}]"
  else:                              return $"[{t.Schema}].[{t.Name}]"
```

Applied to:
- `INSERT INTO ...`
- `UPDATE ... SET ...`
- `DELETE FROM ...` (if `DeleterNeeded`)
- `SET IDENTITY_INSERT ... ON/OFF`
- `IF NOT EXISTS (SELECT 1 FROM ...)` guards

The `use {Database}` line is unaffected (database-level, not table-level).

**Backward-compat for goldens** — the existing 5 characterisation goldens were recorded when `Schema` was nonexistent, so they emit `[Table]`. After this feature lands, those scenarios must continue to emit `[Table]` (because the discovered tables in the test DB are in `dbo`, but the operator's XML config has no Schema attribute, so `Schema` stays empty in the in-memory `PTable` after deserialization). Step 05 verifies this byte-equality.

> **Subtle invariant:** the discovered `PTable` from `MSSQLSourceSchema` *does* have `Schema = "dbo"` (we always populate it from `sys.schemas`). But the `TableToExtract` from the *operator's config* has `Schema = ""`. After `ResolveTable` matches the two, the engine works with the *config* `TableToExtract` for emission decisions (Where filters, FieldsToExclude, etc.) and the *discovered* `PTable` for FK metadata. **Decision: emission's `Qualify` reads the config `TableToExtract`'s Schema** — so empty-Schema config keeps emitting bare names. New configs that specify Schema explicitly emit qualified names.

This is the key decision. Step 04's tests pin it.

---

## 4. Component diagram

```
+------------------------------------------+
|  ParameterizationExtractor.Logic         |
|                                          |
|  Model/                                  |
|   ├── PTable             (+ Schema)      |
|   └── TableToExtract     (+ Schema)      |
|                                          |
|  MSSQL/                                  |
|   ├── MSSQLSourceSchema                  |
|   │     ├── GetMetaData (now joins      |
|   │     │     sys.schemas)              |
|   │     └── ResolveTable (NEW helper)   |
|   ├── MetaDataInitializer                |
|   │     (modified SQL)                   |
|   └── DependencyBuilder                  |
|         (FK lookups via (Schema, Name))  |
|                                          |
|  Templates/                              |
|   └── DefaultTemplate.tt                 |
|        (Qualify helper, + regen .cs)     |
|                                          |
|  Configs/Json/                           |
|   └── (auto — JsonOptions.Default        |
|        + camelCase covers `schema`)      |
+------------------------------------------+
```

No new project references. `Microsoft.Data.SqlClient` already in Logic; no new dep.

---

## 5. Data flow — XML/JSON deserialization

**XML** (existing fixtures):
```xml
<TableToExtract Name="Patient" />               ← Schema = ""  (legacy; bare name)
<TableToExtract Schema="audit" Name="Log" />    ← Schema = "audit" (new)
```

**JSON** (existing `.bws` files + new):
```json
{ "name": "Patient" }                            ← Schema = ""
{ "schema": "audit", "name": "Log" }             ← Schema = "audit"
```

Round-trip: writing back an empty-Schema `TableToExtract` omits the attribute / property (do not emit `schema=""` — keep the wire shape unchanged for legacy configs). XML uses `[DefaultValue("")]`; JSON uses `[JsonIgnore(Condition = WhenWritingDefault)]`.

Step 01 pins this with serialization tests.

---

## 6. Equality / hashing

`PTable.Equals(other)`:
```csharp
return string.Equals(Schema, other.Schema, StringComparison.OrdinalIgnoreCase)
    && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
```

Same for `GetHashCode`. **Case-insensitive**: SQL Server identifiers are case-insensitive by default; we mirror that. (If a future feature needs case-sensitive collation support, it gets a separate ADR.)

---

## 7. Cross-schema characterisation scenario

The previously deferred test lands as a 6th scenario in `Tests/CharacterisationTests/`:

- `cross-schema.xml` config seeds from a table in `dbo` that has an FK target in a non-default schema (e.g. `audit.ChangeLog`).
- Test DB setup: `[OneTimeSetUp]` checks for the `audit` schema; if absent, attempts `CREATE SCHEMA audit`. If the operator lacks `CREATE SCHEMA` permission, the test is **skipped with `Assert.Ignore`** (not failed) so the rest of the suite keeps running. Pinned by a sub-test that reads the suite's "schema available?" flag.
- Golden recorded with `__GENERATED_TIMESTAMP__` masking (existing normaliser).
- Both loaders (Programmatic, Json) exercise the scenario.

Tear-down does not drop the schema — it's idempotent across runs.

---

## 8. Risks & open questions

- **Identity-insert behaviour across schemas.** `SET IDENTITY_INSERT [audit].[Log] ON` is the same statement shape; should work. Pinned by the cross-schema golden.
- **`MSSQLSourceSchema.GetMetaData` SQL.** The existing query likely already mentions `sys.schemas` in some form; verify in step 02 and adjust minimally (the change is *populating the Schema field* on the resulting `PTable`, not necessarily the JOIN itself).
- **Tests for `ResolveTable` ambiguity.** Need a way to inject "two schemas have the same table name" without polluting the test DB. Decision: pure unit test against an in-memory list of `PTable` instances (no DB needed for the helper itself). The DB-backed tests cover the integration path.
- **`PTable.Equals` change might affect `HashSet<PTable>` / `Dictionary<PTable, ...>` callers** elsewhere in the codebase. Need to grep before / after; if callers exist, verify they still behave correctly with the new tuple semantics.
- **Operator confusion** when bare names suddenly throw `AmbiguousTableException` (because they added an `audit` schema with a colliding name). Mitigation: clear error message naming the discovered schemas.

---

## 9. Security / isolation

No new trust boundaries. No new IO. The metadata query already runs as the operator's SQL user; schema visibility is governed by SQL Server's existing permission model (operator only sees schemas they have `VIEW DEFINITION` on). No new attack surface.

---

## 10. What this feature does NOT build

- No multi-database support (still single-DB per package).
- No schema-rename migration tool.
- No CLI default-schema knob.
- No automatic XML/`.bws` rewrite to add Schema where it can be inferred. Operator-driven only.
- No `[Schema].[Table]` *parsing* of `Name` attribute.
