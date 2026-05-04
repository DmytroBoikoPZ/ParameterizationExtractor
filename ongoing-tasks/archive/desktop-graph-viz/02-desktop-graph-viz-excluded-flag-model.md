# 02 — desktop-graph-viz — Engine model: `Excluded` flag on `TableToExtract`

## Goal

Add `bool Excluded` to `TableToExtract` (default `false`). XML and JSON round-trip; JSON omits the field when value is the default. **Engine still emits the table** — wiring lands in step 03. This step is purely additive metadata so the round-trip can be tested in isolation. Mirrors the `Schema` flag rollout from `engine-schema-aware-resolution`.

## Track

`engine` (.NET / C#).

## What Exists

- [`Logic/Model/RootRecord.cs`](../../ParameterizationExtractor.Logic/Model/RootRecord.cs) — `TableToExtract` definition. Already carries the optional `Schema` field with the same `[XmlAttribute] / [JsonPropertyName] / [JsonIgnore(WhenWritingNull)]` shape (pattern exemplar).
- [`Tests/EngineModelTests/`](../../Tests/EngineModelTests/) — pattern exemplar for round-trip tests.
- `Logic/Configs/SourceForScript.cs` — recently gained the optional `Query` field with the `WhenWritingNull` shape (also a pattern exemplar).

## What to Build

### `Logic/Model/RootRecord.cs` — `TableToExtract`

```csharp
/// <summary>
/// When true, the engine skips this table during the FK walk: no rows are
/// extracted and no SQL is emitted for it. UI v1 (desktop-graph-tab + later)
/// flips it; v1 of desktop-graph-viz reads the flag for the ✗ rendering only.
/// Default false — back-compat preserved for legacy XML / DSL workspaces.
/// </summary>
[XmlAttribute("excluded"), DefaultValue(false)]
[JsonPropertyName("excluded"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
public bool Excluded { get; set; }
```

`WhenWritingDefault` (not `WhenWritingNull`) because `bool` defaults to `false`, not `null`.

### Tests

Per `prompts/execution.md` — CONTRACT: type + serialization tests, no RED phase.

New file `Tests/EngineModelTests/TableToExtractExcludedFlagTests.cs`:

1. `Excluded_DefaultIsFalse` — fresh `TableToExtract`; assert `false`.
2. `JsonRoundTrip_DefaultExcluded_OmitsFromOutput` — serialize a default instance; assert the JSON has no `"excluded"` key.
3. `JsonRoundTrip_ExcludedTrue_RoundTrips` — serialize/deserialize an `Excluded = true` instance; assert it survives.
4. `XmlRoundTrip_ExcludedTrue_PersistsAttribute` — round-trip via `XmlSerializer`; assert the `excluded="true"` attribute appears.
5. `XmlRoundTrip_ExcludedDefault_AttributeOmittedOrFalse` — default round-trip; document the actual XML behaviour (XmlSerializer emits the attribute as `false` because the type is value, even with `[DefaultValue]` — pin whatever ships).
6. `LegacyJsonWithoutField_DeserializesAsFalse` — feed JSON missing the `excluded` key; assert `Excluded == false`.
7. `LegacyXmlWithoutAttribute_DeserializesAsFalse` — same for XML.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-01 N + 7 = N + 7, all green.
- All existing characterisation tests still pass byte-identically — the field is additive and defaults to `false`, so no engine behaviour change.

## Acceptance Criteria

- [ ] `TableToExtract.Excluded` exists with the documented attributes.
- [ ] All 7 round-trip tests pass.
- [ ] All existing 220 tests still pass byte-identically.
- [ ] Grep confirms `Excluded` is used only in `TableToExtract` (no engine wiring yet — that's step 03).

## References

- ADR: [`adr/011-engine-schema-awareness.md`](../../adr/011-engine-schema-awareness.md) (pattern exemplar for additive engine-model rollout).
- Pattern exemplars: `TableToExtract.Schema` (engine-schema-aware-resolution), `SourceForScript.Query` (desktop-seed-tab).
- Depends on: [01 — ADR-012 + AutomaticGraphLayout NuGet](./01-desktop-graph-viz-adr-and-nuget.md) (only for ADR sequencing — not technically blocking).
