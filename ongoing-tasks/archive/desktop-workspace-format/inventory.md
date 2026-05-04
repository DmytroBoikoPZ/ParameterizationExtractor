# desktop-workspace-format — engine-config inventory

> Working document for step 01. Captures the as-is shape of every engine-config POCO that the JSON path must round-trip. Used to author `docs/methodology/workspace-format.md` and ADR-009.

## 1. Engine-config POCOs (all in `ParameterizationExtractor.Logic`)

| Type | File | Namespace | XML root | Notes |
|------|------|-----------|----------|-------|
| `Package` | `Configs/Package.cs:11` | `Quipu.ParameterizationExtractor.Logic.Configs` | `<Package>` | `List<SourceForScript> Scripts`. No class-level XML attribute. |
| `SourceForScript` | `Configs/SourceForScript.cs:12` | `Quipu.ParameterizationExtractor.Logic.Configs` | (element only) | `Order` / `ScriptName` / `Comments` are `[XmlAttribute]`; `RootRecords` / `TablesToProcess` are elements. `[XmlIgnore]` on the interface-typed accessors. |
| `RecordsToExtract` | `Model/RootRecord.cs:53` | `Quipu.ParameterizationExtractor.Logic.Model` | (element only) | All three fields are `[XmlAttribute]`: `TableName`, `Where`, `ProcessingOrder`. |
| `TableToExtract` | `Model/RootRecord.cs:12` | `Quipu.ParameterizationExtractor.Logic.Model` | (element only) | `TableName` / `UniqueColumns` are `[XmlAttribute]`; `ExtractStrategy` / `SqlBuildStrategy` are elements. |
| `ExtractStrategy` | `Model/ExtractStrategy.cs:16` | `Quipu.ParameterizationExtractor.Logic.Model` | (element only) | **Concrete base class** (not abstract). `[XmlInclude]` enumerates 4 subclasses: `FKDependencyExtractStrategy`, `OnlyParentExtractStrategy`, `OnlyChildrenExtractStrategy`, `OnlyOneTableExtractStrategy`. XML uses `xsi:type="..."` to discriminate. Fields: `ProcessChildren` / `ProcessParents` / `Where` are `[XmlAttribute]`; `DependencyToExclude` is an element. |
| `FKDependencyExtractStrategy` | `Model/ExtractStrategy.cs:45` | same | n/a | No new fields. Defaults: `ProcessChildren=true, ProcessParents=true`. |
| `OnlyParentExtractStrategy` | `Model/ExtractStrategy.cs:53` | same | n/a | Defaults: `ProcessChildren=false, ProcessParents=true`. |
| `OnlyChildrenExtractStrategy` | `Model/ExtractStrategy.cs:59` | same | n/a | Defaults: `ProcessChildren=true, ProcessParents=false`. |
| `OnlyOneTableExtractStrategy` | `Model/ExtractStrategy.cs:65` | same | n/a | Defaults: `ProcessChildren=false, ProcessParents=false`. |
| `SqlBuildStrategy` | `Model/SqlBuildStrategy.cs:12` | `Quipu.ParameterizationExtractor.Logic.Model` | (element only) | All fields `[XmlAttribute]`: `ThrowExecptionIfNotExists` (typo preserved), `NoInserts`, `AsIsInserts`, `IdentityInsert`, `FieldsToExclude`, `DeleteExistingRecords`. **Not** polymorphic — single class, no subtypes. |
| `GlobalExtractConfiguration` | `Model/GlobalExtractConfiguration.cs:13` | `Quipu.ParameterizationExtractor.Logic.Model` | (root for `ExtractConfig.xml`) | Fields: `FieldsToExclude` / `UniqueColums` (typo preserved) are elements; `DefaultExtractStrategy` / `DefaultSqlBuildStrategy` / `ResultingScriptOptions` are elements. `[XmlIgnore]` on the interface-typed accessors. |
| `UniqueColumnsCollection` | `Model/UniqueColumnsCollection.cs:10` | `Quipu.ParameterizationExtractor.Logic.Model` | (element only) | `TableName` / `UniqueColumns` are `[XmlAttribute]`. |
| `ResultingScriptOptions` | `Model/ResultingScriptOptions.cs:11` | **`ParameterizationExtractor.Logic.Model`** (no `Quipu.` prefix — pre-existing inconsistency) | (element only) | `TargetDatabase` / `Rollback` (default `true`) are `[XmlAttribute]`. |

## 2. Polymorphism

- **Polymorphic types:** only `ExtractStrategy` (4 subclasses).
- **`SqlBuildStrategy`:** single class, no subtypes — no polymorphism needed for JSON.
- **Base-class concreteness:** `ExtractStrategy` is **NOT** `abstract`. `[JsonPolymorphic]` works on concrete bases — discriminator-less JSON deserializes to the base type. ADR-009 picks: keep base concrete, but require `$kind` on input. Step 02 configures `JsonSerializerOptions` accordingly (use `[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false)]` and validate that all incoming objects carry `$kind` via either schema discipline or a small custom converter).

## 3. Pre-existing typos to preserve

| Typo | Lives in | Why preserved |
|------|----------|---------------|
| `ThrowExecptionIfNotExists` (should be `ThrowExceptionIf...`) | `SqlBuildStrategy` | Pinned by characterisation goldens via `ClearingPackage.xml` attribute names. Renaming would break XML round-trip and require a separate "fix-typo" feature. |
| `UniqueColums` (should be `UniqueColumns`) | `GlobalExtractConfiguration` | Same reason — pinned by `ExtractConfig.xml`. Note the *other* `UniqueColumns` (on `TableToExtract` and `UniqueColumnsCollection`) is spelled correctly. |

JSON property names match the C# names verbatim (camelCased): `throwExecptionIfNotExists`, `uniqueColums`. Future "spelling-fix" feature can add aliases via `[JsonPropertyName]`; out of scope here.

## 4. Collection patterns

- All list properties use `List<T>`, not `IList<T>` — `System.Text.Json` deserializes cleanly with the existing parameterless constructors.
- `[XmlAttribute] List<string>` (e.g. `FieldsToExclude`, `UniqueColumns`) is XML-serialized as space-separated values in a single attribute. **In JSON these become real arrays** — a structural improvement, not a breaking change. Goldens are not affected because they test engine output, not config round-trip.

## 5. Newtonsoft.Json usage in the repo

`grep "using Newtonsoft\|Newtonsoft.Json"` over `*.cs` returns **zero hits** in production code. Only references are in this feature's own scaffold and an archived checklist (`net10-upgrade-checklist.md`'s historical note).

→ `System.Text.Json` is unblocked; no co-existence concerns.

## 6. Things the JSON shape must NOT cover (out of scope)

- Runtime types: `PRecord`, `PField`, `PTableMetadata`, `PFieldMetadata`, `PTableDependency`, `PDependentTable` — these are engine-internal traversal state, never serialized, never part of any config file.
- Interfaces: `IPackage`, `ISourceForScript`, `IExtractConfiguration`, `IAmDSLFriendly`, `IDSLConnector` — interface shapes are unaffected; JSON only deserializes concrete POCO types.
- DSL types (F# AST in `ParameterizationExtractor.DSL`) — frozen (ADR-005), not addressed by JSON.
