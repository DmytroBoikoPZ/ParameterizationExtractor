# 01 — desktop-workspace-format — Inventory + JSON shape design + ADR-009

## Goal

Produce a complete, reviewable design for the JSON shape of `Package` and `GlobalExtractConfiguration`, plus the `.bws` workspace wrapper. Author **ADR-009** locking the load-bearing decisions (System.Text.Json, polymorphic-discriminator strategy, XML+JSON coexistence, file-extension semantics). **No code, no test changes.** The output is an inventory document, a sample `.bws` file mocked up by hand, and the ADR — all reviewed by the user before step 02 begins.

## Track

`cross-cutting` (docs / design / ADR).

## What Exists

- **XML serializer (CLI-side adapter):** [`ParameterizationExtractor/Configs/ConfigSerializer.cs`](../../ParameterizationExtractor/Configs/ConfigSerializer.cs) — implements `ICanSerializeConfigs`, dispatches by file extension (`.xml` → XmlSerializer, `.bc` → DSL connector). The pattern this feature extends.
- **Sample package XML:** [`ParameterizationExtractor/ClearingPackage.xml`](../../ParameterizationExtractor/ClearingPackage.xml) — worked example with multiple `SourceForScript` entries, `RootRecords`, `TablesToProcess`, all `ExtractStrategy` variants and `SqlBuildStrategy` configurations. Treat this as the **fixture** for the JSON shape.
- **Engine POCOs to be JSON-shaped:**
  - [`ParameterizationExtractor.Logic/Configs/Package.cs`](../../ParameterizationExtractor.Logic/Configs/Package.cs) — `Package : IPackage, IAmDSLFriendly`. Holds `List<SourceForScript> Scripts`.
  - [`ParameterizationExtractor.Logic/Configs/SourceForScript.cs`](../../ParameterizationExtractor.Logic/Configs/SourceForScript.cs)
  - [`ParameterizationExtractor.Logic/Model/GlobalExtractConfiguration.cs`](../../ParameterizationExtractor.Logic/Model/GlobalExtractConfiguration.cs) — `GlobalExtractConfiguration : IExtractConfiguration`. Holds `FieldsToExclude`, `UniqueColums` (sic), `DefaultExtractStrategy`, `DefaultSqlBuildStrategy`, `ResultingScriptOptions`.
  - [`ParameterizationExtractor.Logic/Model/ExtractStrategy.cs`](../../ParameterizationExtractor.Logic/Model/ExtractStrategy.cs) — base type for the polymorphic strategies (`FKDependency`, `OnlyOneTable`, `OnlyChildren`, `OnlyParent`).
  - [`ParameterizationExtractor.Logic/Model/SqlBuildStrategy.cs`](../../ParameterizationExtractor.Logic/Model/SqlBuildStrategy.cs)
  - [`ParameterizationExtractor.Logic/Model/ResultingScriptOptions.cs`](../../ParameterizationExtractor.Logic/Model/ResultingScriptOptions.cs)
  - [`ParameterizationExtractor.Logic/Model/UniqueColumnsCollection.cs`](../../ParameterizationExtractor.Logic/Model/UniqueColumnsCollection.cs)
  - [`ParameterizationExtractor.Logic/Model/RootRecord.cs`](../../ParameterizationExtractor.Logic/Model/RootRecord.cs)
  - [`ParameterizationExtractor.Logic/Model/PTable.cs`](../../ParameterizationExtractor.Logic/Model/PTable.cs)
- **Existing characterisation harness:** [`Tests/CharacterisationTests/`](../../Tests/CharacterisationTests/) — provides the goldens that step 03 will assert against the JSON path.
- **Mockups already aligned with this work:**
  - [`docs/design/desktop-ui/06-node-inspector.md`](../../docs/design/desktop-ui/06-node-inspector.md) — strategy names (`FKDependency` etc.) match the discriminator values.
  - [`docs/design/desktop-ui/02-new-workspace.md`](../../docs/design/desktop-ui/02-new-workspace.md) — connection block shape.
  - [`docs/design/desktop-ui/readme.md`](../../docs/design/desktop-ui/readme.md) — locked decisions (extension `.bws`, single-document, in-workspace connection, password-DPAPI-deferred).

## What to Build

- **Inventory document** at `docs/methodology/workspace-format-inventory.md` (or under `ongoing-tasks/desktop-workspace-format/inventory.md` — author's call). Captures:
  - Every `[XmlElement]` / `[XmlAttribute]` / `[XmlIgnore]` on the engine POCOs above. Naming, defaults, optionality.
  - Every concrete `ExtractStrategy` / `SqlBuildStrategy` variant in the codebase (grep for subclasses).
  - Any existing `Newtonsoft.Json` usage anywhere in the repo (likely none, but confirm). If any, design must avoid collision.
  - Whether `ExtractStrategy` is `abstract` (required for `[JsonPolymorphic]`).
  - Whether the model uses `IList<T>` vs `List<T>` properties (`System.Text.Json` won't deserialize `IList<T>` setters by default — pin the pattern).
- **JSON-shape design document** at `docs/methodology/workspace-format.md`. Sections:
  - **Library + version:** `System.Text.Json` 10.0.0 (matches `Microsoft.Extensions.*` family). Where the source-generators are (or aren't) used.
  - **Property naming:** camelCase (modern convention; explicit `JsonNamingPolicy.CamelCase`).
  - **Polymorphic types:** `[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]` + one `[JsonDerivedType]` per concrete subtype. Discriminator values: short PascalCase names matching the user-visible strategy names (`FKDependency`, `OnlyOneTable`, `OnlyChildren`, `OnlyParent`). Same approach for any other polymorphic types found in inventory.
  - **Engine-input shape (canonical examples):** one full JSON example for `Package`, one for `GlobalExtractConfiguration`. Round-trip 1:1 with `ClearingPackage.xml` + `ExtractConfig.xml`.
  - **Workspace wrapper shape:** envelope with `$version`, `name`, `source`, `global`, `package`. Sample `examples/sample.bws` mocked up by hand from `ClearingPackage.xml` + a synthesised `source` block.
  - **What this format does NOT cover:** password storage, encryption, schema migration (`$version` is a placeholder; v1 is the only version).
- **`adr/009-workspace-format.md`** with the four canonical sections. Decision locks:
  - Workspace files use the `.bws` extension and contain the JSON wrapper described in the design doc.
  - Engine accepts both XML (existing) and JSON (new) for `Package` and `GlobalExtractConfiguration`. JSON canonical for new workspaces; XML preserved indefinitely (no flag day).
  - JSON library = `System.Text.Json` 10.0.0; polymorphism via `[JsonPolymorphic]` with discriminator `$kind`.
  - Property naming = camelCase via `JsonNamingPolicy.CamelCase`.
  - Workspace wrapper is **desktop-only**; the engine never reads a `.bws` file directly, only its embedded subtrees.
  - Open follow-ups: schema migration story (v2+), CLI accepting `.bws` directly, password-at-rest (deferred to `desktop-connection-management`).
- **`adr/readme.md`** index — row added for ADR-009.

## Acceptance Criteria

- [ ] `docs/methodology/workspace-format.md` exists and includes a complete JSON example for `Package` + `GlobalExtractConfiguration` whose every field maps onto a corresponding XML element / attribute in `ClearingPackage.xml` / `ExtractConfig.xml`. No invented fields.
- [ ] Inventory document exists, lists every `ExtractStrategy` and `SqlBuildStrategy` concrete subclass found by grep, with file:line.
- [ ] Inventory confirms whether `ExtractStrategy` is `abstract` and what the C# attribute layout looks like today.
- [ ] Sample `examples/sample.bws` exists, is valid JSON, and is a faithful translation of `ClearingPackage.xml` (every `<SourceForScript>`, `<TableToExtract>`, etc. has a JSON counterpart).
- [ ] `adr/009-workspace-format.md` exists with all four canonical sections, Status `Accepted` after user review. Cites ADR-005, ADR-007, ADR-008.
- [ ] ADR-009 lists schema-migration story, CLI-accepting-`.bws`, and password-at-rest as open follow-ups.
- [ ] `adr/readme.md` row for ADR-009 in the same shape as existing rows.
- [ ] **Manual user review** — design + sample + ADR. User confirms the JSON shape matches expectation; edits land in the same step. This step's exit gate is "user approves".
- [ ] No code changes; `dotnet build` 0 errors, `dotnet test` 34/34 (sanity).

## References

- ADRs: [`adr/005-freeze-fsharp-dsl.md`](../../adr/005-freeze-fsharp-dsl.md), [`adr/007-desktop-wpf-stack.md`](../../adr/007-desktop-wpf-stack.md), [`adr/008-desktop-ui-controls.md`](../../adr/008-desktop-ui-controls.md)
- Methodology siblings: [`docs/methodology/dotnet-cli.md`](../../docs/methodology/dotnet-cli.md), [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md)
- Mockups: [`docs/design/desktop-ui/`](../../docs/design/desktop-ui/readme.md)
- Pattern exemplar for ADR shape: [`adr/006-msdi-container.md`](../../adr/006-msdi-container.md)
- Depends on: nothing (first step of the feature).
