# Workspace JSON format

> Definition of the JSON shape the engine consumes (alongside XML) and the `.bws` workspace wrapper the desktop reads/writes. Authored alongside [ADR-009](../../adr/009-workspace-format.md). Matches the engine POCOs as inventoried in [`ongoing-tasks/desktop-workspace-format/inventory.md`](../../ongoing-tasks/desktop-workspace-format/inventory.md).
>
> Formats covered:
> 1. **Engine input — `Package` JSON** — drop-in replacement for `*.xml` package files.
> 2. **Engine input — `GlobalExtractConfiguration` JSON** — drop-in replacement for `ExtractConfig.xml`.
> 3. **Workspace wrapper — `*.bws`** — desktop-only envelope embedding both engine subtrees + workspace metadata + source-connection metadata.

The engine never reads a `.bws` file directly. Only its embedded `global` and `package` subtrees are handed to the engine readers.

---

## 1. Library and conventions

| Aspect | Choice | Rationale |
|--------|--------|-----------|
| JSON library | `System.Text.Json` 10.0.0 | First-party, source-generator support, native polymorphism, no Newtonsoft.Json present in repo. Locked in [ADR-009](../../adr/009-workspace-format.md). |
| Property naming | camelCase via `JsonNamingPolicy.CamelCase` | Modern JSON convention. C# POCOs stay PascalCase internally. |
| Case-insensitive read | `PropertyNameCaseInsensitive = true` | Defensive; tolerates hand-edited workspaces. |
| Polymorphic discriminator | property `$kind` (not `$type`) | Avoids accidental Newtonsoft compatibility expectations; clearly recognisable. |
| Discriminator values | short user-facing names: `FKDependency`, `OnlyOneTable`, `OnlyChildren`, `OnlyParent` | Matches the inspector mockup (`docs/design/desktop-ui/06-node-inspector.md`). No `*ExtractStrategy` suffix in JSON. |
| Schema version | `$version: 1` on the wrapper. Unknown versions throw on load. | v1 is the only version. Migration story is a future concern. |
| Pre-existing typos | preserved verbatim (`throwExecptionIfNotExists`, `uniqueColums`) | Keeps round-trip equivalence with XML simple. A future "spelling-fix" feature can add `[JsonPropertyName]` aliases. |

---

## 2. Polymorphic types

Only `ExtractStrategy` is polymorphic. Annotation on the engine POCO (added in step 02):

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind",
                 IgnoreUnrecognizedTypeDiscriminators = false,
                 UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(FKDependencyExtractStrategy), "FKDependency")]
[JsonDerivedType(typeof(OnlyOneTableExtractStrategy), "OnlyOneTable")]
[JsonDerivedType(typeof(OnlyChildrenExtractStrategy), "OnlyChildren")]
[JsonDerivedType(typeof(OnlyParentExtractStrategy),  "OnlyParent")]
public class ExtractStrategy { … }
```

`SqlBuildStrategy` is a single concrete class — no `$kind`.

---

## 3. Engine input — `Package` JSON

XML reference: [`ParameterizationExtractor/ClearingPackage.xml`](../../ParameterizationExtractor/ClearingPackage.xml).

```jsonc
{
  "scripts": [
    {
      "order": 1,
      "scriptName": "Patients",
      "query": "SELECT * FROM dbo.Patient WHERE LastName = 'Demo'",
      "comments": null,

      "rootRecords": [
        { "tableName": "Patient",  "where": "LastName = 'Demo'", "processingOrder": 1 },
        { "schema": "audit", "tableName": "Log", "where": "RecordedAt > '2025-01-01'", "processingOrder": 2 }
      ],

      "tablesToProcess": [
        {
          "tableName": "Patient",
          "uniqueColumns": ["LastName", "FirstName"],
          "extractStrategy": {
            "$kind": "FKDependency",
            "processChildren": true,
            "processParents": true,
            "where": null,
            "dependencyToExclude": []
          },
          "sqlBuildStrategy": {
            "throwExecptionIfNotExists": true,
            "noInserts": false,
            "asIsInserts": false,
            "identityInsert": false,
            "fieldsToExclude": [],
            "deleteExistingRecords": false
          }
        },
        {
          "tableName": "LookupCountry",
          "uniqueColumns": [],
          "extractStrategy": {
            "$kind": "OnlyOneTable",
            "processChildren": false,
            "processParents": false,
            "where": "IsActive = 1",
            "dependencyToExclude": []
          },
          "sqlBuildStrategy": {
            "throwExecptionIfNotExists": false,
            "noInserts": false,
            "asIsInserts": false,
            "identityInsert": false,
            "fieldsToExclude": [],
            "deleteExistingRecords": false
          }
        }
      ]
    }
  ]
}
```

Field-by-field correspondence to XML:

| JSON path | XML path | Type | Required |
|-----------|----------|------|----------|
| `scripts[].order` | `<SourceForScript Order="">` | int | yes |
| `scripts[].scriptName` | `<SourceForScript ScriptName="">` | string | yes |
| `scripts[].query` | _(JSON-only)_ | string? | no — desktop's Seed-tab seed query (full SELECT). Engine ignores it currently; round-trips so the operator's typed query survives close/reopen. Added by `desktop-seed-tab`. |
| `scripts[].comments` | `<SourceForScript Comments="">` | string? | no |
| `scripts[].rootRecords[].schema` | `<RecordsToExtract Schema="">` | string? | no (omit / empty → bare-name resolution per ADR-011) |
| `scripts[].rootRecords[].tableName` | `<RecordsToExtract TableName="">` | string | yes |
| `scripts[].rootRecords[].where` | `<RecordsToExtract Where="">` | string | yes |
| `scripts[].rootRecords[].processingOrder` | `<RecordsToExtract ProcessingOrder="">` | int | yes |
| `scripts[].tablesToProcess[].schema` | `<TableToExtract Schema="">` | string? | no (omit / empty → bare-name resolution per ADR-011) |
| `scripts[].tablesToProcess[].tableName` | `<TableToExtract TableName="">` | string | yes |
| `scripts[].tablesToProcess[].excluded` | `<TableToExtract Excluded="">` | bool | no — when `true`, the engine skips this table during the FK walk and emits no SQL. Defaults to `false`; back-compat preserved (legacy XML / DSL workspaces deserialise as `false`). Added by `desktop-graph-viz`. |
| `scripts[].tablesToProcess[].uniqueColumns` | `<TableToExtract UniqueColumns="">` (space-separated in XML) | `string[]` | yes |
| `scripts[].tablesToProcess[].extractStrategy` | `<ExtractStrategy xsi:type="…">` | polymorphic | yes |
| `scripts[].tablesToProcess[].sqlBuildStrategy` | `<SqlBuildStrategy>` | object | yes |
| `*.extractStrategy.$kind` | `xsi:type="<X>ExtractStrategy"` | discriminator | yes |
| `*.extractStrategy.processChildren` / `processParents` / `where` | same `<ExtractStrategy>` attributes | bool / bool / string? | yes |
| `*.extractStrategy.dependencyToExclude` | `<ExtractStrategy>` element child | `string[]` | no |
| `*.sqlBuildStrategy.throwExecptionIfNotExists` (sic) | same `<SqlBuildStrategy>` attribute | bool | yes |
| (other `sqlBuildStrategy` fields) | same `<SqlBuildStrategy>` attributes | bool / `string[]` | yes |

---

## 4. Engine input — `GlobalExtractConfiguration` JSON

XML reference: `ExtractConfig.xml` in the working CLI directory (operator-supplied).

```jsonc
{
  "fieldsToExclude": [ "RowVersion", "ModifiedAt" ],
  "uniqueColums": [
    {
      "tableName": "Patient",
      "uniqueColumns": [ "LastName", "FirstName", "DateOfBirth" ]
    }
  ],
  "defaultExtractStrategy": {
    "$kind": "FKDependency",
    "processChildren": true,
    "processParents": true,
    "where": null,
    "dependencyToExclude": []
  },
  "defaultSqlBuildStrategy": {
    "throwExecptionIfNotExists": false,
    "noInserts": false,
    "asIsInserts": false,
    "identityInsert": false,
    "fieldsToExclude": [],
    "deleteExistingRecords": false
  },
  "resultingScriptOptions": {
    "targetDatabase": "",
    "rollback": true
  }
}
```

Note: `uniqueColums` is the typo preserved from the C# class.

---

## 5. Workspace wrapper — `*.bws`

A `.bws` file is a JSON document with a fixed envelope:

```jsonc
{
  "$version": 1,

  "name": "patient-clearing",

  "source": {
    "server": "129.212.168.210,1433",
    "database": "budzdorov_Core",
    "auth": "sql",                  // "windows" | "sql"
    "user": "sa",
    "passwordEncrypted": null       // placeholder; populated by desktop-connection-management
  },

  "global": { /* GlobalExtractConfiguration JSON, per § 4 */ },

  "package": { /* Package JSON, per § 3 */ }
}
```

| Field | Type | Required | Owned by |
|-------|------|----------|----------|
| `$version` | int | yes | this feature (only `1` accepted) |
| `name` | string | yes | this feature |
| `source.server` | string | yes (when source present) | this feature |
| `source.database` | string | yes | this feature |
| `source.auth` | enum (`"windows"` / `"sql"`) | yes | this feature |
| `source.user` | string? | yes when `auth = "sql"` | this feature |
| `source.passwordEncrypted` | string? (DPAPI base64) | optional, always `null` until `desktop-connection-management` lands | future feature |
| `global` | object | yes | engine readers (§ 4) |
| `package` | object | yes | engine readers (§ 3) |

The wrapper is owned by `ParameterizationExtractor.Desktop`'s `IWorkspaceStore`. Each subtree (`global`, `package`) deserializes through the engine's reader so any future shape evolution stays in one place.

---

## 6. Reader contract (CLI / engine side)

Two reader entry points, mirroring the existing XML serializers:

```csharp
namespace Quipu.ParameterizationExtractor.Logic.Configs.Json
{
    public static class JsonPackageReader
    {
        public static Package Read(Stream stream);
        public static Package Read(string path);
    }

    public static class JsonGlobalConfigReader
    {
        public static GlobalExtractConfiguration Read(Stream stream);
        public static GlobalExtractConfiguration Read(string path);
    }
}
```

Both use a shared `JsonSerializerOptions` with the conventions in § 1.

The CLI's `ConfigSerializer.GetPackage(path)` adds a `.json` branch alongside `.xml` and `.bc`.

---

## 7. Sample workspace

See [`examples/sample.bws`](../../examples/sample.bws). Translates [`ParameterizationExtractor/ClearingPackage.xml`](../../ParameterizationExtractor/ClearingPackage.xml) field-by-field into the wrapper format.

---

## 8. What this format does NOT cover

- **Password encryption** — slot only. DPAPI flow lives in `desktop-connection-management`.
- **Schema migration** — `$version` is forward-looking; only `1` is accepted today. v2+ migration is a future concern.
- **CLI accepting `.bws`** — out of scope for the workspace-format feature. The CLI keeps its `--package` flow over `.xml` / `.bc`.
- **Engine connection-from-workspace** — engine receives connections through `IUnitOfWorkFactory` separately. The `source` block is desktop-side only.
- **DSL** (`*.bc`) — frozen ([ADR-005](../../adr/005-freeze-fsharp-dsl.md)). Not represented in JSON.
