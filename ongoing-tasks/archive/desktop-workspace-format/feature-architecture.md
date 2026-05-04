# Desktop Workspace Format — Architecture Overview

> Engine learns JSON; desktop gains the workspace wrapper. Engine output is unchanged for existing XML; new tests prove parity. No UI in this feature — services and POCOs only.

---

## 1. Pipeline / Integration

```
Today (CLI):
  ConfigSerializer.GetPackage(path) ──┬── *.xml → XmlSerializer<Package>
                                       └── *.bc  → DSLConnector.Parse → Package

After this feature:
  ConfigSerializer.GetPackage(path) ──┬── *.xml  → XmlSerializer<Package>      (unchanged)
                                       ├── *.bc   → DSLConnector.Parse           (unchanged, F# DSL frozen)
                                       └── *.json → JsonPackageReader<Package>  (NEW; lives in Logic)

  ConfigSerializer.GetGlobalConfig() ──┬── ExtractConfig.xml (default)         (unchanged)
                                        └── *.json (when called with explicit path)  (NEW)

  Desktop:
    IWorkspaceStore.LoadAsync(path) ─── *.bws → System.Text.Json → WorkspaceModel
                                                                  ├── Source (connection metadata, sans password)
                                                                  ├── Global (engine GlobalExtractConfiguration)
                                                                  └── Package (engine Package)
```

The Desktop's `IWorkspaceStore` delegates the engine-input subtrees (`Global`, `Package`) to the new JSON readers in `Logic`. The wrapper itself (workspace metadata + connection metadata) is desktop-only.

| Stage | What changes | Where |
|-------|--------------|-------|
| Engine input | gains JSON path beside XML | `Logic.Configs.Json.JsonPackageReader`, `Logic.Configs.Json.JsonGlobalConfigReader` |
| Strategy polymorphism | `xsi:type` (XML) → `[JsonPolymorphic]` discriminator (JSON) | `Logic.Model.ExtractStrategy` (attribute additions only — no shape change) |
| CLI deserializer adapter | extension dispatch in `ConfigSerializer.GetPackage` | `ParameterizationExtractor/Configs/ConfigSerializer.cs` |
| Workspace wrapper | new POCO + reader/writer | `ParameterizationExtractor.Desktop/Services/Workspace/` |

---

## 2. Component Diagram

```
+-------------------+    embeds       +-------------+    deserializes by
|  WorkspaceModel   |───────────────→ |  Package    |───── extension ────┬── XmlSerializer (existing)
|  (Desktop only)   |                 |             |                    └── JsonPackageReader (NEW)
|                   |                 |  (Logic)    |
|  +Name            |                 +-------------+
|  +Source (conn    |
|   metadata, sans  |    embeds       +---------------------------+
|   password)       |───────────────→ | GlobalExtractConfiguration|─── XmlSerializer (existing)
|  +Global   ───────|─────────┐       | (Logic)                   |─── JsonGlobalConfigReader (NEW)
|  +Package  ───────|────────┐│       +---------------------------+
+-------------------+        ││
                             ││
                    Logic POCOs (unchanged shape; gain JSON-discriminator attributes only)
```

Reference graph delta: none. `Desktop → Common, Logic` already permits the wrapper to embed `Logic` types. `Logic → Common` is unchanged.

---

## 3. Data Flow

### 3.1 Inbound

**Engine (CLI continues to work):** `ConfigSerializer.GetPackage("foo.xml")` returns `IPackage` via `XmlSerializer`. New: `ConfigSerializer.GetPackage("foo.json")` dispatches to `JsonPackageReader`. Same return type, same downstream behaviour.

**Desktop:** `IWorkspaceStore.LoadAsync("workspace.bws")` reads the wrapper, returns a `WorkspaceModel` containing the workspace name, the source-connection metadata, and the embedded `Package` + `GlobalExtractConfiguration` (already typed as the engine's POCOs).

### 3.2 Processing

The engine itself doesn't process workspace metadata — it only sees `Package` and `GlobalExtractConfiguration`, regardless of whether the calling code loaded them from XML, JSON, DSL, or the desktop wrapper.

### 3.3 Outbound

**Desktop:** `IWorkspaceStore.SaveAsync(WorkspaceModel, path)` writes the wrapper as JSON via `System.Text.Json`.

---

## 4. Data Stores Summary

| Store | Technology | Format | Owner |
|-------|------------|--------|-------|
| Existing extraction packages | filesystem `*.xml`, `*.bc` | XML / F# DSL | CLI (read) |
| New extraction packages | filesystem `*.json` | JSON via `System.Text.Json` | CLI (read), engine (deserialize) |
| Workspace files | filesystem `*.bws` | JSON wrapper | Desktop (`IWorkspaceStore`) |

---

## 5. JSON shape decisions

### 5.1 Library

**`System.Text.Json`.** Microsoft-published, .NET-native, source-generator support, native polymorphism via `[JsonPolymorphic]` (.NET 7+ attribute, supported on `netstandard2.0` with the package reference). Single library across engine + desktop.

Rejected: `Newtonsoft.Json` — bigger surface area, more idiosyncratic polymorphism, no compelling feature for this workload. Locked in ADR-009.

### 5.2 Polymorphic `ExtractStrategy`

XML uses `xsi:type="OnlyOneTableExtractStrategy"`. JSON equivalent — the leading candidate is `[JsonPolymorphic]` + `[JsonDerivedType]` on the `ExtractStrategy` base type:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(FKDependencyExtractStrategy),  "FKDependency")]
[JsonDerivedType(typeof(OnlyOneTableExtractStrategy),  "OnlyOneTable")]
[JsonDerivedType(typeof(OnlyChildrenExtractStrategy),  "OnlyChildren")]
[JsonDerivedType(typeof(OnlyParentExtractStrategy),    "OnlyParent")]
public abstract class ExtractStrategy { ... }
```

Discriminator property: `$kind`. Discriminator values: short kebab-free names matching the enum-ish concept the user sees in the UI (`FKDependency`, `OnlyOneTable`, etc. — same names exposed in the node-inspector mockup [M6](../../docs/design/desktop-ui/06-node-inspector.md)).

Same treatment for any other polymorphic types in the model surface (e.g. `SqlBuildStrategy` if it has variants — confirmed during step 01 inventory).

### 5.3 Workspace wrapper schema (target — refined in step 01)

```jsonc
{
  "$version": 1,
  "name": "patient-clearing",
  "source": {
    "server": "129.212.168.210,1433",
    "database": "budzdorov_Core",
    "auth": "sql",                     // "windows" | "sql"
    "user": "sa",
    "passwordEncrypted": null          // placeholder; desktop-connection-management owns this
  },
  "global": {                          // engine GlobalExtractConfiguration POCO, JSON-shaped
    "fieldsToExclude": [...],
    "uniqueColumns": [...],
    "defaultExtractStrategy": { "$kind": "FKDependency", "processChildren": true, "processParents": true, "where": null },
    "defaultSqlBuildStrategy": { ... },
    "resultingScriptOptions": { ... }
  },
  "package": {                         // engine Package POCO, JSON-shaped
    "scripts": [
      {
        "order": 1,
        "scriptName": "Patients",
        "rootRecords": [
          { "tableName": "Patient", "where": "LastName = 'Demo'", "processingOrder": 1 }
        ],
        "tablesToProcess": [
          {
            "tableName": "Patient",
            "uniqueColumns": "...",
            "extractStrategy": { "$kind": "FKDependency", "processChildren": true, "processParents": true, "where": null },
            "sqlBuildStrategy": { "throwExceptionIfNotExists": true, "noInserts": false, "asIsInserts": false, "identityInsert": false, "fieldsToExclude": "" }
          }
          // ... more tables
        ]
      }
    ]
  }
}
```

The wrapper schema and the per-property naming (PascalCase vs camelCase) are firmed up in step 01.

### 5.4 Engine input vs workspace wrapper

**Engine input subtrees (`global`, `package`)** are shaped to round-trip the engine's POCOs directly via `System.Text.Json`. Standalone `*.json` files of those shapes are valid engine input via the new readers.

**Workspace wrapper** is desktop-only. The engine never deserializes a `.bws` file — only its embedded subtrees. This keeps the engine's responsibility narrow: convert JSON → POCO. The desktop's `IWorkspaceStore` owns the wrapper.

---

## 5.5 Characterisation parity — harness shape

Step 03 proves XML ≡ JSON behaviourally. Locked approach: **parametric runner.**

- Single `CharacterisationRunner` extended with a `PackageLoader` dimension (`Programmatic` | `Json`).
- `[TestCaseSource]` yields Cartesian product `(scenario × loader)`. 5 pinned scenarios × 2 loaders = 10 characterisation cases over the same 5 goldens.
- Both loaders feed the **identical** downstream pipeline — engine invocation, normaliser, golden diff. One assertion path; no risk of XML and JSON paths drifting in their normalisation or reporting.
- Test names embed the loader (e.g. `Characterisation(only-one-table-employee, Json)`) so failures are diagnosable at-a-glance.

Rejected: a parallel `JsonCharacterisationRunner` (additive) — would duplicate the normaliser / diff / golden-lookup code and let the two paths drift.

---

## 6. Extension Points

| Seam | Status after this feature |
|------|---------------------------|
| `IWorkspaceStore` (was pre-spec) | **Implemented.** Reads/writes `.bws`. |
| `JsonPackageReader` / `JsonGlobalConfigReader` | New, in `Logic.Configs.Json/`. Other consumers (CLI, future tools) can call them directly. |
| Workspace versioning (`$version`) | Schema slot present; migration is a future concern (no v2 yet). |

---

## 7. Security & Isolation

- **Connection metadata** in the workspace contains no password in this feature (placeholder field). DPAPI flow lands in `desktop-connection-management`.
- **JSON deserialization** is type-bound (no `[JsonExtensionData]` catch-all, no `JsonObject` runtime evaluation). Untrusted workspace files cannot inject arbitrary types because polymorphic discrimination is closed-set via `[JsonDerivedType]`.
- **No new SQL paths, no new network, no new processes.**

---

## 8. Risks & Open Questions

- **Newtonsoft-style polymorphism in legacy data:** if the codebase has any pre-existing `Newtonsoft.Json` usage that the JSON reader could collide with, step 01 must surface it. Likely none (current XML-only path).
- **Naming convention drift:** the engine's POCOs use PascalCase property names; default `System.Text.Json` deserialization is case-insensitive but serialization defaults to camelCase. Pick one — likely camelCase for JSON output (modern convention) — and verify XML→JSON round-trip semantics aren't affected (they're separate paths).
- **`ExtractStrategy` base type:** if the existing C# class isn't `abstract`, `[JsonPolymorphic]` won't fire — the base must be abstract or use the type-discriminator on the property level. Step 01 confirms the model shape and proposes any minimum changes. **Any change to engine model classes (even attribute-only) must keep XML output identical** — characterisation parity is the gate.
- **Sample `.bws` realism:** the sample should be derived from `ClearingPackage.xml` plus a synthesised `Source` block, not invented. Step 04 builds it from the existing sample to guarantee fidelity.
- **`$kind` vs `$type`:** picking `$kind` to avoid confusion with Newtonsoft's `$type` magic property. Lockable in ADR-009.

---

## 9. What This Feature Does *Not* Build

- No UI views — Workspace tab, Connection editor, Save dialog all live in later UI features.
- No `IDialogService` / `IUiDispatcher` — neither is needed; the workspace store is constructor-injected and synchronous-ish (file IO via `Task`).
- No CLI flag for `.bws` or `.json` packages — CLI keeps its current XML/DSL-only `--package` flow.
- No password encryption — placeholder field only.
- No engine behaviour change. JSON path must produce byte-equal goldens to XML.
