# 02 — desktop-workspace-format — Engine JSON consumer

## Goal

Implement the JSON readers in `Logic` so the engine can deserialize a `Package` and a `GlobalExtractConfiguration` from JSON, with full equivalence to the existing XML path. CLI's `ConfigSerializer` extends to dispatch `.json` → JSON reader. **Pure engine + CLI work; no desktop changes.**

## Track

`backend` (.NET / C#).

## What Exists

- After step 01: ADR-009, design doc, inventory. Concrete naming + discriminator strategy locked.
- [`ParameterizationExtractor.Logic/Configs/Package.cs`](../../ParameterizationExtractor.Logic/Configs/Package.cs), [`SourceForScript.cs`](../../ParameterizationExtractor.Logic/Configs/SourceForScript.cs), [`Model/GlobalExtractConfiguration.cs`](../../ParameterizationExtractor.Logic/Model/GlobalExtractConfiguration.cs), `Model/ExtractStrategy.cs`, `Model/SqlBuildStrategy.cs`, `Model/ResultingScriptOptions.cs`, `Model/UniqueColumnsCollection.cs`, `Model/RootRecord.cs`, `Model/PTable.cs` — engine POCOs to attribute.
- [`ParameterizationExtractor/Configs/ConfigSerializer.cs`](../../ParameterizationExtractor/Configs/ConfigSerializer.cs) — CLI adapter; the dispatch point that learns `.json`.
- [`ParameterizationExtractor.Logic/Interfaces/ICanSerializeConfigs.cs`](../../ParameterizationExtractor.Logic/Interfaces/ICanSerializeConfigs.cs) — interface. Likely no shape change needed.
- Pattern exemplar: existing XML deserialization in `ConfigSerializer.GetPackage` and `ConfigSerializer.GetGlobalConfig` (the `xml`-branch is what the `json`-branch mirrors).

## What to Build

### Engine module additions (`ParameterizationExtractor.Logic`)

- New folder `Logic/Configs/Json/` (or whatever step 01 settles on).
- New `JsonPackageReader` — static or instance class with `Package Read(Stream)` / `Package Read(string path)`. Uses `System.Text.Json.JsonSerializer.Deserialize<Package>(...)`. Configures `JsonSerializerOptions` with:
  - `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
  - `PropertyNameCaseInsensitive = true` (defensive)
  - Source-generator vs reflection — author's call; reflection is fine for v1, source generators are a follow-up.
- New `JsonGlobalConfigReader` — same shape, returns `GlobalExtractConfiguration`.
- (Optional symmetry) writers `JsonPackageWriter` / `JsonGlobalConfigWriter`. Required for step 04's workspace round-trip; if simpler to inline there, defer.
- **Attribute additions on POCOs** — minimum needed for `[JsonPolymorphic]` to fire on `ExtractStrategy` (and `SqlBuildStrategy` if step 01 finds variants). **No shape changes.** XML attributes (`[XmlElement]`, `[XmlIgnore]`) are unaffected.
- **PackageReference** added to [`ParameterizationExtractor.Logic.csproj`](../../ParameterizationExtractor.Logic/ParameterizationExtractor.Logic.csproj): `System.Text.Json` 10.0.0. (Logic targets `netstandard2.0`; the package supports it.)

### CLI adapter ([`ParameterizationExtractor/Configs/ConfigSerializer.cs`](../../ParameterizationExtractor/Configs/ConfigSerializer.cs))

- Extend `GetPackage(path)`: case `.json` → `JsonPackageReader.Read(path)`. Same pattern as existing `.xml` and `.bc` branches.
- Extend `GetGlobalConfig()` — current signature has no path argument (it reads `ExtractConfig.xml` hardcoded at line 17). **Do NOT change CLI behaviour.** If a JSON-input path for global config is needed for tests, expose a new overload `GetGlobalConfig(string path)` that dispatches by extension; keep parameterless `GetGlobalConfig()` reading `ExtractConfig.xml` as today. The new overload is for the desktop's use, not the CLI's.

### Tests (TDD)

Per [`prompts/execution.md`](../../prompts/execution.md) CODE classification — RED → GREEN → REFACTOR.

New test fixture under `Tests/EngineJsonTests/` (or extend an existing folder):

1. **`JsonPackageReader_ReadsMinimalPackage`** — single `SourceForScript`, single `RootRecord`, single `TableToExtract` with `FKDependencyExtractStrategy`. Asserts every field round-trips.
2. **`JsonPackageReader_ReadsAllExtractStrategyVariants`** — one test per concrete subtype: `FKDependency`, `OnlyOneTable`, `OnlyChildren`, `OnlyParent`. Uses the discriminator from ADR-009. Asserts the deserialized strategy is the right concrete type and its properties match.
3. **`JsonPackageReader_RejectsUnknownStrategyKind`** — `$kind: "Unknown"` throws (or returns a defined error). Tripwire test for the closed-set polymorphism.
4. **`JsonGlobalConfigReader_ReadsCompleteConfig`** — every field on `GlobalExtractConfiguration`, including default-strategy polymorphism.
5. **Equivalence test:** load the same logical package from XML and JSON; assert the resulting object graphs are equal (structural equality — likely `FluentAssertions` `BeEquivalentTo`).
6. **`ConfigSerializer_DispatchesByExtension`** — `.xml` → XmlSerializer path, `.json` → JsonPackageReader path, `.bc` → DSL path (unchanged), unknown → `NotSupportedException`.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors. New warnings only if pre-existing (FParsec architecture).
- `dotnet test "SQL Buldozer.sln"` — pre-existing 34 + new ~6 = 40+ tests, all pass.
- All 5 CharacterisationTests still pass — XML path unchanged (parity is step 03's domain; this step just verifies XML didn't regress).

## Acceptance Criteria

- [ ] `JsonPackageReader` and `JsonGlobalConfigReader` exist in `Logic/Configs/Json/`, deserialize matching examples from step 01's design doc, and produce object graphs equal to the XML path's output for the same input.
- [ ] `ExtractStrategy` (and any other polymorphic types found in step 01) carry `[JsonPolymorphic]` + `[JsonDerivedType]` attributes per the discriminator strategy locked in ADR-009.
- [ ] `ConfigSerializer.GetPackage(path)` dispatches `.json` to the new reader without breaking `.xml` or `.bc` branches.
- [ ] All 4 concrete `ExtractStrategy` variants round-trip through JSON correctly (separate test per variant).
- [ ] An XML→object→assert + JSON→object→assert equivalence test exists and passes.
- [ ] `dotnet test` — pre-existing 34 still pass; new tests added for engine JSON; all green.
- [ ] No XML-path output regression — characterisation goldens unchanged.
- [ ] No new tripwire violations (no `Console.WriteLine`, no `IConfiguration[..]`, no `Thread.Sleep`, no `try/catch` masking exceptions in deserialization).
- [ ] `Logic.csproj` adds `System.Text.Json 10.0.0`. No other new package references.
- [ ] `Logic` does not gain a reference to any non-engine assembly.

## References

- ADR: [`adr/009-workspace-format.md`](../../adr/009-workspace-format.md) (created in step 01)
- Methodology: [`docs/methodology/dotnet-cli.md`](../../docs/methodology/dotnet-cli.md), [`docs/methodology/workspace-format.md`](../../docs/methodology/workspace-format.md) (created in step 01)
- Pattern exemplars: existing XML branches in `ConfigSerializer.GetPackage` / `GetGlobalConfig`
- Layering: [`adr/002-module-layering.md`](../../adr/002-module-layering.md)
- Depends on: step 01 must be `[x]` first (design + ADR locked).
