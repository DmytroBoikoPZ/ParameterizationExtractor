# 011 — Engine schema-awareness — `Schema` field on table-naming model + lookup policy

## Status

Accepted

## Context

Today's engine resolves table names by sending bare `[TableName]` to SQL Server and letting the connection's default-schema fallback do the work. The bootstrap-era characterisation tests pinned the same-schema scenario; cross-schema FK references were deferred. The newly-scaffolded `desktop-seed-tab` needs `Schema.TableName` from `IDatabaseExplorer.ListTablesAsync` so the operator can pick a root table from any schema. The deferral has caught up.

The `WorkspaceSource.PasswordEncrypted` field on the workspace JSON ([ADR-009](./009-workspace-format.md)) was a similar early reservation; this ADR is the analogous formalisation for the table-naming surface.

The decision needs to be locked **before** `MSSQLSourceSchema.GetMetaData` starts populating `Schema` — once the lookup helper exists with one policy, downstream features will accumulate around it.

## Decision

We will:

- **Add `Schema` (string, default `""`) to every engine model class that names a table.** Concretely: `TableToExtract` (config-side), `RecordsToExtract` (config-side seed selector), `PTableMetadata` (discovered metadata), `PDependentTable` (FK relation — both `ParentSchema` and `ReferencedSchema`). Empty Schema is the legacy / "don't care" marker.
- **Empty `Schema` in operator config means "fall back to bare-name resolution"**. The `MSSQLSourceSchema.ResolveTable(schema, name)` helper implements the policy:
  - 0 matches → `TableNotFoundException`.
  - 1 match → that table (preserves today's behaviour for single-schema setups).
  - 2+ matches → throws `AmbiguousTableException(name, candidateSchemas)` with a clear message naming the discovered schemas. The operator's resolution is to add `Schema="..."` to their config.
- **`MSSQLSourceSchema.GetMetaData` always populates Schema** on every discovered `PTableMetadata` from the `INFORMATION_SCHEMA.TABLES.TABLE_SCHEMA` column (or equivalent `sys.schemas` join). Discovered entries always have a concrete Schema even when the operator wrote bare names.
- **`PTableMetadata.Equals` / `GetHashCode` use `(Schema, TableName)` tuple semantics, OrdinalIgnoreCase**. SQL Server identifiers are case-insensitive by default; we mirror that. Case-sensitive collation support gets a separate ADR if it ever matters.
- **T4 SQL emission** reads the Schema from the **config `TableToExtract`** (not the discovered `PTableMetadata`). Empty Schema → bare `[Table]` (today's behaviour preserved for legacy XML); non-empty → `[Schema].[Table]`.
- **Wire compat:** XML uses `[XmlAttribute("schema")] [DefaultValue("")]`; JSON uses `[JsonIgnore(Condition = WhenWritingDefault)]`. Empty Schema does not appear on disk — every existing fixture round-trips byte-identically.

Alternatives considered and rejected:

- **Empty Schema → silent default to `dbo`.** Simpler, but produces silently wrong SQL when the operator's workspace has multiple schemas with same-named tables and `dbo` happens to have one of them — the engine would emit `INSERT INTO [dbo].[X]` against a table the operator never selected. The throw forces a one-time fix; silent-wrong-output is the failure mode we cannot accept for a tool that emits SQL applied to other environments.
- **Empty Schema → query `SCHEMA_NAME()` to discover the user's default.** Closer to today's runtime behaviour but requires an extra round-trip on `Init()` and still leaves the multi-schema-with-collision case undefined.
- **Composite parsing — `Name="dbo.Patient"`.** Looks convenient but conflates parsing with semantics. A literal table named `dbo.Patient` (legal in SQL) becomes ambiguous. Explicit `Schema=` attribute is unambiguous. Pinned by an XML round-trip test.
- **`record` conversion of `PTableMetadata` to get value semantics for free.** Out of scope; `PTableMetadata` is `HashSet<PFieldMetadata>` and the conversion would ripple. Hand-rolled `Equals`/`GetHashCode` is the smaller change.

## Consequences

- **Easier:**
  - Cross-schema source databases work without operator workarounds.
  - The `desktop-seed-tab` table picker can show `Schema.TableName` consistently.
  - Future tooling (graph viz, extras) inherits the same `(Schema, Name)` identity; less drift.
  - The `ResolveTable` helper consolidates name-lookup logic into one place — no more grep-and-pray "did I cover all the lookup sites".

- **Harder:**
  - Two engine inputs (XML, JSON) both gain a Schema slot to maintain. Round-trip tests pin compat.
  - T4 emission has a per-call decision (`Qualify(t)`) — small but everywhere. Minor maintenance burden.
  - Operators with a multi-schema source DB and same-named tables now see `AmbiguousTableException` on first run with their existing bare-name config. **Mitigation:** the exception message names the candidate schemas; the fix is one attribute.
  - `PTableMetadata.Equals` change might affect any existing `HashSet<PTableMetadata>` / `Dictionary<PTableMetadata, _>` callers. Audit grep at impl time confirmed no existing callers rely on reference equality (today's lookups are name-string-based, not Equals-based).

- **Open follow-ups:**
  - **CLI `--default-schema` flag** — operator can override via the SQL connection string already; no separate knob unless feedback demands it.
  - **Schema-rename detection** — operator must update their config if a schema is renamed. No auto-detect.
  - **Fixture-rewrite tool** — no automatic rewrite of existing `.xml` / `.bws` to add `Schema=`. Operator-driven only.
