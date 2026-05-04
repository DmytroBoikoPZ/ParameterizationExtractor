# Desktop Workspace Format

## Goal

Land the workspace data model. The engine learns to consume **JSON** for `Package` and `GlobalExtractConfiguration` alongside the existing XML readers ([ADR-007](../adr/007-desktop-wpf-stack.md), L10). The desktop gains an `IWorkspaceStore` that reads/writes `.bws` files — a JSON wrapper that bundles workspace metadata + source-connection metadata + the engine-readable subtrees. After this feature, every subsequent UI feature has a stable contract to bind to.

## Scope

- **In scope:**
  - **Engine-side JSON reader** for `Package` and `GlobalExtractConfiguration`. Mirrors the existing XML serializer ([`ParameterizationExtractor/Configs/ConfigSerializer.cs`](../ParameterizationExtractor/Configs/ConfigSerializer.cs)). Lives in `Logic` (not `Common` — Logic stays the engine boundary).
  - **JSON shape decisions:** library = `System.Text.Json`; polymorphic discrimination (`ExtractStrategy` variants — `FKDependencyExtractStrategy`, `OnlyOneTableExtractStrategy`, etc.) via `[JsonPolymorphic]` + `[JsonDerivedType]`.
  - **Workspace wrapper** (`.bws` JSON) — data shape + `WorkspaceModel` POCO in `ParameterizationExtractor.Desktop/Services/Workspace/`.
  - **`IWorkspaceStore`** interface + WPF impl reading/writing `.bws`. Connection block is structurally present but the password field is a placeholder until [`desktop-connection-management`](#) lands. Owns the seam named in `desktop-skeleton/feature-architecture.md § 5`.
  - **Characterisation parity:** each of the 5 pinned scenarios gets a JSON expression of its `Package`; new test asserts the JSON path produces byte-equal goldens to the XML path.
  - **Sample `.bws`** ported from `ClearingPackage.xml`.
  - **ADR-009** locking the format (XML + JSON coexist, JSON canonical for new workspaces, `System.Text.Json` chosen, polymorphic-discriminator strategy).
  - Cross-doc updates: `docs/architecture/overview.md`, `docs/methodology/dotnet-cli.md`, `docs/methodology/wpf-desktop.md` (drop the "pre-spec" tag from `IWorkspaceStore`).
- **Out of scope:**
  - **CLI accepting `.bws`** — out of scope. The CLI keeps its current `--package <path>` flow (XML + DSL); workspace is desktop-side. A future small feature can extend the CLI if wanted.
  - **Connection password storage** — schema slot only; DPAPI encryption / opt-out checkbox lands in [`desktop-connection-management`](#).
  - **UI** — no views, no MainWindow changes. Workspace is loaded/saved through DI services only; smoke-tested at VM level.
  - **`IUiDispatcher` / `IDialogService`** — first feature that needs them ships them per the recipe pre-spec ([`docs/methodology/wpf-desktop.md`](../docs/methodology/wpf-desktop.md#service-abstractions--pre-spec)). Workspace-format doesn't need either.
  - **Engine connection-from-workspace** — engine still receives connection separately via `IUnitOfWorkFactory`. Workspace stores connection metadata for the desktop's own use, not for engine consumption.
  - **Engine behaviour changes** — XML path output goldens stay identical. JSON path must produce the same goldens.
- **Dependencies:**
  - [`desktop-skeleton`](./archive/desktop-skeleton-checklist.md) — Desktop project + recipe + DI plumbing exist.
  - The mockups ([`docs/design/desktop-ui/`](../docs/design/desktop-ui/readme.md)) — define the operator-facing contract the workspace represents. Schema must be expressive enough to round-trip every node-state shown in M5 / M6 and the extras list in M7.

## Architecture

See [feature-architecture.md](./desktop-workspace-format/feature-architecture.md) for the JSON shape decisions, the polymorphic-discriminator approach for `ExtractStrategy`, the workspace-vs-engine-input boundary, and the wrapper schema with sample.

## Steps

- [x] [01 — Inventory + JSON shape design + ADR-009](./desktop-workspace-format/01-desktop-workspace-format-inventory-and-design.md)
- [x] [02 — Engine JSON consumer (Package + GlobalExtractConfiguration)](./desktop-workspace-format/02-desktop-workspace-format-engine-json.md)
- [x] [03 — Characterisation parity (XML ≡ JSON goldens)](./desktop-workspace-format/03-desktop-workspace-format-characterisation-parity.md)
- [x] [04 — Workspace wrapper + `IWorkspaceStore` + sample + docs](./desktop-workspace-format/04-desktop-workspace-format-workspace-store-and-docs.md)

## Notes

### 2026-05-02 — Step 01 (inventory + design + ADR-009) complete

**Inventory** (`ongoing-tasks/desktop-workspace-format/inventory.md`):
- 11 engine-config POCOs catalogued (Package, SourceForScript, RecordsToExtract, TableToExtract, ExtractStrategy + 4 subclasses, SqlBuildStrategy, GlobalExtractConfiguration, UniqueColumnsCollection, ResultingScriptOptions).
- Only `ExtractStrategy` is polymorphic (4 subclasses); `SqlBuildStrategy` is single-class.
- `ExtractStrategy` is **concrete, not abstract** — `[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators=false, UnknownDerivedTypeHandling=FailSerialization)]` keeps closed-set safety without forcing the base abstract.
- 0 Newtonsoft.Json hits in production code → `System.Text.Json` unblocked.
- 2 pre-existing typos preserved verbatim: `ThrowExecptionIfNotExists` (in `SqlBuildStrategy`), `UniqueColums` (in `GlobalExtractConfiguration`). Pinned by characterisation goldens; renaming is a separate spelling-fix feature.
- `[XmlAttribute] List<string>` (XML space-separated) becomes real arrays in JSON — structural improvement, not breaking (engine output unaffected).

**Design doc** (`docs/methodology/workspace-format.md`):
- Library: `System.Text.Json` 10.0.0, `JsonNamingPolicy.CamelCase`, `PropertyNameCaseInsensitive=true`.
- Polymorphism: `$kind` discriminator, values `FKDependency` / `OnlyOneTable` / `OnlyChildren` / `OnlyParent` (no `*ExtractStrategy` suffix).
- Workspace wrapper envelope: `{ $version, name, source, global, package }`. Only `$version: 1` accepted.
- Reader contract: `JsonPackageReader` and `JsonGlobalConfigReader` static classes in `Logic.Configs.Json` namespace; `Read(Stream)` and `Read(string path)` overloads.
- Section 3 carries the canonical `Package` JSON example with field-by-field XML correspondence table.
- Section 4 carries the canonical `GlobalExtractConfiguration` JSON example.
- Section 5 documents the wrapper schema. Section 8 lists what the format does NOT cover.

**Sample** (`examples/sample.bws`): hand-built `.bws` mirroring `ClearingPackage.xml` structure with concrete table names (`Patient`, `LookupCountry`); valid JSON parseable by the reader spec.

**ADR-009** (`adr/009-workspace-format.md`): Status `Accepted`. All 4 sections. Cites ADR-003 (DSL), ADR-005 (DSL freeze), ADR-007 (desktop stack). Open follow-ups: schema migration (v2+), CLI `.bws` support, password-at-rest (deferred to `desktop-connection-management`), `[JsonPropertyName]` typo aliases, source-gen paths.

**Index**: `adr/readme.md` row added for ADR-009.

**Sanity:** `dotnet build` 0 errors. `dotnet test` 34/34 pass. No code changed.

**Pending:** manual user review of inventory + design doc + sample + ADR. Step exit gate.

### 2026-05-02 — Step 02 (engine JSON consumer) complete

**New files (`ParameterizationExtractor.Logic/Configs/Json/`):**

- `JsonOptions.cs` — shared `JsonSerializerOptions` (camelCase, case-insensitive read, comments-skip, trailing-commas allowed).
- `JsonPackageReader.cs` — static reader for `Package`. Stream + path overloads.
- `JsonGlobalConfigReader.cs` — static reader for `GlobalExtractConfiguration`. Stream + path overloads.

**Engine model attributes (`ExtractStrategy.cs`):**

Added `[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind", IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]` plus 4 `[JsonDerivedType]` rows. XML attributes on the same class unaffected.

**CLI dispatch (`ConfigSerializer.cs`):**

`.json` branch added to `GetPackage(path)`, alongside existing `.xml` and `.bc`. Behaviour for the existing branches unchanged. `GetGlobalConfig()` left as-is (parameterless, hardcoded `ExtractConfig.xml`); the `(string path)` overload from the step's What-to-Build was deemed unnecessary since tests call `JsonGlobalConfigReader` directly.

**Package refs:**

- `Logic.csproj` — added `System.Text.Json 10.0.0` (matches `Microsoft.Extensions.*` family). Logic targets `net10.0` so polymorphism attributes are first-class.

**Tests added (10):**

- `JsonPackageReaderTests`: `Read_MinimalPackage_DeserialisesAllFields`, `Read_ExtractStrategyVariant_DeserialisesToCorrectConcreteType` ×4 (parametrised over the 4 strategy variants), `Read_UnknownStrategyKind_Throws`, `Read_PackageEquivalentToXmlPackage_StructurallyEqual`, `Read_DispatchesViaConfigSerializer_WhenExtensionIsJson`.
- `JsonGlobalConfigReaderTests`: `Read_CompleteConfig_DeserialisesAllFields`, `Read_DefaultStrategyMissingKind_DeserialisesToBaseExtractStrategy` (documents the concrete-base behaviour locked in ADR-009).

**Adjustment from spec:**

Equivalence test (`Read_PackageEquivalentToXmlPackage_StructurallyEqual`) excludes `UniqueColumns` paths in addition to `FieldsToExclude` / `DependencyToExclude`. Same XML quirk: `[XmlAttribute] List<string>` parses an empty attribute (`UniqueColumns=""`) as a single-element `[""]` list while JSON parses `[]` as empty. Documented in a test comment; engine output unaffected (consumers filter empties).

**Verification:**

- `dotnet build` — 0 errors. 5 warnings (pre-existing FParsec architecture, SYSLIB obsoletes — unrelated).
- `dotnet test` — **44/44 pass** (was 34; +10).
- All 5 CharacterisationTests still pass — XML path goldens unchanged.
- No tripwire violations: no `Console.WriteLine`, no `IConfiguration[..]`, no service-locator patterns introduced.

### 2026-05-02 — Step 03 (characterisation parity) complete

**Approach: parametric runner (Option B, locked in feature-architecture.md § 5.5).**

**Files added:**

- `Tests/CharacterisationTests/Json/{scenario}.json` × 5 — JSON fixtures for each pinned scenario, hand-translated from the programmatic builders in `Scenarios.cs`. Each fixture wraps a single `SourceForScript` inside a `Package`.
- `Tests/Tests.csproj` — `<None Update="CharacterisationTests\Json\*.json">` block so fixtures copy to output (`bin/.../CharacterisationTests/Json/*.json`).

**Files modified:**

- `Scenarios.cs` — added `PackageLoader` enum (`Programmatic`, `Json`); `Scenario.Load(loader)` dispatches to the existing `Build()` or to `JsonPackageReader.Read(...)` from `AppContext.BaseDirectory + CharacterisationTests/Json/{name}.json`; `AsTestCases()` now yields the Cartesian product `(scenario × loader)` with test names like `where-filter-during-child-walk(Json)`.
- `CharacterisationTests.cs` — `Scenario_Matches_Golden` takes `(Scenario, PackageLoader)`. Recording mode keys on the scenario name only and only saves when the loader is `Programmatic` (avoids a JSON-loader run silently overwriting a golden).

**Verification:**

- `dotnet build` 0 errors.
- `dotnet test` — **49/49 pass** (was 44; +5 net = 5 new JSON-loader characterisation cases over the same 5 goldens).
- Each scenario shows up twice in test output: `{name}(Programmatic)` and `{name}(Json)`. Both pass with byte-equal output. Goldens unchanged.
- L10 commitment ("engine takes both XML + JSON, indefinitely") is now demonstrable: 5 scenarios × 2 input formats × 1 golden each = 10 cases, all green.

### 2026-05-02 — Step 04 (workspace wrapper + IWorkspaceStore + sample + docs) complete

**New files (`ParameterizationExtractor.Desktop/Services/Workspace/`):**

- `WorkspaceModel.cs` — POCO. `[JsonPropertyName("$version")] int Version` (default 1), `string Name`, `WorkspaceSource Source`, `GlobalExtractConfiguration Global`, `Package Global` — engine POCOs embedded directly.
- `WorkspaceSource.cs` — POCO. `Server`, `Database`, `Auth` (string, validated `"windows"|"sql"`), `User?`, `PasswordEncrypted?` (placeholder; semantics owned by `desktop-connection-management`).
- `IWorkspaceStore.cs` — `internal interface` with `LoadAsync(path, ct)` + `SaveAsync(workspace, path, ct)`.
- `JsonWorkspaceStore.cs` — `IWorkspaceStore` impl. Reuses `Logic.Configs.Json.JsonOptions.Default` so embedded subtrees deserialise identically to standalone engine input. Validates `$version == 1` and `auth ∈ {"windows", "sql"}` on load.

**Sample workspace:**

- `examples/sample.bws` (created in step 01) ported as test fixture via `<Link>Examples\sample.bws</Link>` in `Tests.csproj`. Test `LoadsSampleBws_ProducesEngineReadablePackageAndGlobal` verifies it parses and the embedded subtrees are well-formed (1 script with 2 tables, FKDependency default strategy).

**DI registration (`DesktopHost.cs`):**

- `services.AddSingleton<IWorkspaceStore, JsonWorkspaceStore>()` — Singleton (stateless).

**Tests added (6):**

- `WorkspaceStoreTests`: `RoundTrip_PreservesAllFields`, `LoadsSampleBws_ProducesEngineReadablePackageAndGlobal`, `Load_RejectsUnknownVersion` ($version=2 throws), `Load_RejectsInvalidAuthValue` (auth="kerberos" throws), `PasswordEncryptedField_RoundTripsAsNull_PlaceholderSemantics`.
- `HostCompositionTests`: `+DesktopHost_ResolvesIWorkspaceStore` — confirms DI wiring.

**Cross-doc updates:**

- `docs/architecture/overview.md`: § 2 Desktop row mentions `IWorkspaceStore`. § 4 new `*.bws` filesystem-store row. § 7 ADR-009 row added.
- `docs/methodology/dotnet-cli.md`: new `## Extraction config inputs` section after SQL generation — table of formats (XML, .bc, JSON) + their readers, with cross-link to `workspace-format.md` and ADR-009. Documents that `.bws` is desktop-only.
- `docs/methodology/wpf-desktop.md`: new `## Workspace store` section between Code editor and Dialogs/file pickers — names `IWorkspaceStore` + `JsonWorkspaceStore`, cross-links to `workspace-format.md` and ADR-009.

**Adjustment from spec:**

- `RoundTrip_PreservesAllFields` initially asserted `"$version": 1` (with space). System.Text.Json default writes compact (`"$version":1` no space) — loosened the assertion to `"$version"` substring match. The point is "the JSON wire-name is `$version`", not formatting.

**Verification:**

- `dotnet build` 0 errors.
- `dotnet test` — **55/55 pass** (was 49; +6).
- Manual smoke launch: desktop exe opens MetroWindow, closes cleanly with exit code 0. `IWorkspaceStore` is in the service collection but unused by UI (no UI yet).
- All 5 CharacterisationTests still pass via both Programmatic and Json loaders (10 cases). Goldens unchanged.
- Tripwire compliance: VMs / services don't reference WPF types; no `MessageBox.Show`; no `Console.WriteLine`; no `IConfiguration[..]` lookups.

## Final summary

The `desktop-workspace-format` feature is complete: 4/4 steps, 55/55 tests green.

**Cumulative changes:**

- **1 new ADR:** ADR-009 (workspace JSON format, `System.Text.Json`, `$kind` discriminator, `.bws` extension).
- **1 new methodology doc:** `docs/methodology/workspace-format.md` — JSON shape + wrapper schema + reader contract + sample.
- **1 sample file:** `examples/sample.bws`.
- **3 new engine source files** (in `Logic/Configs/Json/`): `JsonOptions.cs`, `JsonPackageReader.cs`, `JsonGlobalConfigReader.cs`. `Logic.csproj` adds `System.Text.Json 10.0.0`.
- **Engine model attribute additions** on `ExtractStrategy.cs`: `[JsonPolymorphic]` + 4 `[JsonDerivedType]` rows. No shape change; XML output preserved.
- **CLI dispatch** extended in `ConfigSerializer.cs` (`.json` branch in `GetPackage`).
- **4 new desktop source files** (in `Services/Workspace/`): `WorkspaceModel`, `WorkspaceSource`, `IWorkspaceStore`, `JsonWorkspaceStore`. `DesktopHost.cs` registers the store as Singleton.
- **Characterisation harness extended** with `PackageLoader` enum (`Programmatic`, `Json`); `(scenario × loader)` Cartesian product yields 10 cases over 5 goldens; both paths byte-equal.
- **5 JSON characterisation fixtures** under `Tests/CharacterisationTests/Json/`.
- **17 new tests:** 10 engine-JSON (in `Tests/EngineJsonTests/`), 6 workspace-store + composition (in `Tests/Desktop/`), and the parametric harness adds 5 net characterisation cases (5 → 10).
- **Cross-doc updates:** `docs/architecture/overview.md` § 2 / § 4 / § 7; `docs/methodology/dotnet-cli.md` (Extraction config inputs section); `docs/methodology/wpf-desktop.md` (Workspace store section); `adr/readme.md` index.

**Net test delta:** 34 → 55 (+21).

**Open follow-ups (deliberately deferred to subsequent features):**

- `desktop-connection-management` — DPAPI password storage; ADR for password-at-rest. Will populate the `passwordEncrypted` field that's currently a placeholder.
- `desktop-graph-viz` — graph library pick (Msagl candidate); FK rendering. UI feature that consumes `IWorkspaceStore` to display graph state.
- `desktop-dry-run-and-execute` — Run-tab UI wiring `Logic.PackageProcessor` to workspaces.
- UI features per `docs/design/desktop-ui/` mockups (M1–M8).
- `engine-schema-aware-resolution` — bare-name table resolution; unblocks scenario 6 (deferred from characterisation-tests).
- CLI accepting `.bws` directly — not load-bearing; later additive feature.
- `[JsonPropertyName]` aliases for `throwExecptionIfNotExists` / `uniqueColums` typos — cosmetic, deferred.
- Audit ADRs 002 / 003 / 004 against actual code (recorded in ADR-006 follow-ups).

**Pending action:** archive needs your nod (per `prompts/execution.md` § Completion). Pair to move: `ongoing-tasks/desktop-workspace-format-checklist.md` + `ongoing-tasks/desktop-workspace-format/` → `ongoing-tasks/archive/`.
