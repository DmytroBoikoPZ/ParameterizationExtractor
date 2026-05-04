# 04 — engine-schema-aware-resolution — T4 template emits [Schema].[Table] when Schema non-empty

## Goal

`Templates/DefaultTemplate.tt` emits identifiers via a single `Qualify(PTable)` helper that produces `[Schema].[Table]` when `Schema` is non-empty and `[Table]` when empty (today's behaviour preserved). The emission Schema reads from the **config** `TableToExtract`'s Schema (not the discovered `PTable`'s Schema) — pinned by feature-architecture § 3's invariant. All existing characterisation goldens continue to match byte-for-byte.

## Track

`engine`.

## What Exists

- [`Logic/Templates/DefaultTemplate.tt`](../../ParameterizationExtractor.Logic/Templates/DefaultTemplate.tt) — the T4 template that writes INSERT/UPDATE/DELETE/IDENTITY_INSERT statements.
- [`Logic/Templates/DefaultTemplate.cs`](../../ParameterizationExtractor.Logic/Templates/DefaultTemplate.cs) — generated `.cs` (regenerated when `.tt` changes).
- `PTable.Schema` (step 01), `TableToExtract.Schema` (step 01), `ResolveTable` (step 02), schema-aware FK resolution (step 03).
- Existing 5 characterisation goldens — all-empty Schema in their configs.

## What to Build

### `Logic/Templates/DefaultTemplate.tt` — `Qualify` helper + call sites

Add at the top of the template (after the imports, before the body):

```csharp
<#+
private string Qualify(PTable t) =>
    string.IsNullOrEmpty(t.Schema) ? $"[{t.Name}]" : $"[{t.Schema}].[{t.Name}]";
#>
```

(Or — depending on existing template style — use a `<#+ string Qualify(...) { ... } #>` class-feature block.)

Replace every emission of a table identifier:

- `INSERT INTO [<#=table.Name#>]` → `INSERT INTO <#=Qualify(table)#>`
- `UPDATE [<#=table.Name#>] SET` → `UPDATE <#=Qualify(table)#> SET`
- `DELETE FROM [<#=table.Name#>]` → `DELETE FROM <#=Qualify(table)#>`
- `SET IDENTITY_INSERT [<#=table.Name#>]` → `SET IDENTITY_INSERT <#=Qualify(table)#>`
- `IF NOT EXISTS (SELECT 1 FROM [<#=table.Name#>] ...)` → same

The `use [<#=Database#>]` line is database-level; unchanged.

Audit the template after editing: every `[<#=...Name#>]` involving a table should be replaced with `<#=Qualify(...)#>`. Document the touchpoint count in Notes.

### **Invariant lock** — emission reads from config, not discovered

Per feature-architecture § 3: when the engine emits, the `PTable` it has in hand is either:
- The discovered `PTable` from `MSSQLSourceSchema` (always has Schema populated), OR
- The config `TableToExtract` (operator-supplied Schema; empty for legacy fixtures).

**Decision: emission's `Qualify` is called with the *config* `TableToExtract`** (so legacy XML with `Schema=""` keeps emitting `[Table]`). If the existing template currently passes the discovered `PTable` to emission, the call sites must switch to passing the config `TableToExtract`. Audit this carefully — likely a single helper or a few obvious lines.

### `Logic/Templates/DefaultTemplate.cs` — regenerate

After `.tt` edits, regenerate `.cs`:
- Visual Studio: right-click `.tt` → "Run Custom Tool".
- CLI / dotnet: open the file in VS or use `T4` CLI tool.
- If neither is available locally, hand-edit the `.cs` to mirror the `.tt` changes (template engine produces predictable output).

Verify: `dotnet build` succeeds + the template's unit-/characterisation-test surface produces identical output for empty-Schema scenarios.

### Tests

Per `prompts/execution.md` — CODE: TDD cycle. Most coverage is via existing characterisation; this step adds one targeted test.

New test in existing `Tests/CharacterisationTests/Harness/SqlNormalizerTests.cs` is NOT the right home (it tests the normaliser, not emission). Instead:

New file `Tests/EngineSchemaAwareResolution/SqlEmissionQualifyTests.cs`:

1. `Qualify_EmptySchema_ProducesBareName` — pure unit test of the helper if it's extractable, OR an integration test that runs the engine over a single-table package with empty Schema, asserts `INSERT INTO [Patient]` is in the output. Pick whichever matches the existing template's call-shape.
2. `Qualify_NonEmptySchema_ProducesQualifiedName` — same shape with `Schema = "audit"` → `INSERT INTO [audit].[Log]`.

If `Qualify` is a private helper inside the generated `.cs` (likely), it's not directly testable as a unit. Test via the integration path (engine produces SQL string; assert substring presence/absence).

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test "SQL Buldozer.sln"` — pre-step-03 155 + new 2 = **157 tests**, all green.
- **Critical:** all 10 existing characterisation cases (5 scenarios × 2 loaders) still match their goldens byte-for-byte. Bare-name back-compat is the headline AC.

## Acceptance Criteria

- [ ] `DefaultTemplate.tt` has a `Qualify(PTable)` helper.
- [ ] Every table-identifier emission in the template uses `Qualify`. Audit grep result documented in Notes.
- [ ] Emission's `Qualify` reads from the *config* `TableToExtract` (not the discovered `PTable`) — call sites verified.
- [ ] `DefaultTemplate.cs` regenerated and committed alongside the `.tt` edit.
- [ ] All 10 existing characterisation cases pass byte-identically.
- [ ] Both new emission tests pass.
- [ ] `dotnet build` 0 errors; `dotnet test` 157/157 pass.

## References

- ADR: [`adr/011-engine-schema-awareness.md`](../../adr/011-engine-schema-awareness.md), [`adr/004-t4-sql-generation.md`](../../adr/004-t4-sql-generation.md).
- Pattern: existing template emission shape.
- Depends on: [03 — dependency walking](./03-engine-schema-aware-resolution-dependency-walk.md).
