# 02 — desktop-seed-tab-root-inference — `ScriptEditorViewModel` auto-populate + mismatch hint + tests

## Goal

Wire `SeedQueryParser` into `ScriptEditorViewModel` so the SQL editor drives the Root picker. Add the `RootMismatchHint` observable. **No view changes yet** — step 03.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`Desktop/Views/Seed/ScriptEditorViewModel.cs`](../../ParameterizationExtractor.Desktop/Views/Seed/ScriptEditorViewModel.cs) — current `partial void OnSeedQueryChanged(string value)` already triggers save-on-blur via `OnAnyEditableChanged`. Extend it.
- `SeedQueryParser` (step 01).
- [`Tests/Desktop/ScriptEditorViewModelTests.cs`](../../Tests/Desktop/ScriptEditorViewModelTests.cs) — pattern exemplar for harness + multiple-scenario tests.

## What to Build

### `ScriptEditorViewModel.cs`

Add observable property:

```csharp
[ObservableProperty]
private string _rootMismatchHint = string.Empty;
```

Extend `OnSeedQueryChanged`:

```csharp
partial void OnSeedQueryChanged(string value)
{
    InferRootFromSeedQuery(value);
    OnAnyEditableChanged();
}

private void InferRootFromSeedQuery(string sql)
{
    var parsed = SeedQueryParser.TryExtract(sql);
    if (parsed is null)
    {
        // No FROM detected — keep whatever the operator has, but a stale mismatch hint
        // is no longer relevant.
        if (string.IsNullOrEmpty(RootSchema) && string.IsNullOrEmpty(RootTable))
            RootMismatchHint = string.Empty;
        return;
    }

    if (string.IsNullOrEmpty(RootTable))
    {
        // Auto-populate when picker is empty.
        _suppressMismatchOnNextRootChange = true;
        try
        {
            RootSchema = parsed.Schema;
            RootTable  = parsed.Name;
        }
        finally
        {
            _suppressMismatchOnNextRootChange = false;
        }
        RootMismatchHint = string.Empty;
        return;
    }

    var matches =
        string.Equals(RootSchema ?? string.Empty, parsed.Schema, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(RootTable ?? string.Empty, parsed.Name, StringComparison.OrdinalIgnoreCase);

    RootMismatchHint = matches
        ? string.Empty
        : $"SQL targets {Format(parsed.Schema, parsed.Name)}; picked root is {Format(RootSchema, RootTable)}";
}

private static string Format(string schema, string name) =>
    string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";
```

Extend the existing `partial void OnRootSchemaChanged` / `OnRootTableChanged` callbacks (or add a single `OnAnyRootChanged` helper called from both) so a manual picker change clears `RootMismatchHint`:

```csharp
partial void OnRootSchemaChanged(string value)
{
    if (!_suppressMismatchOnNextRootChange)
        RootMismatchHint = string.Empty;
    OnAnyEditableChanged();
}

partial void OnRootTableChanged(string value)
{
    if (!_suppressMismatchOnNextRootChange)
        RootMismatchHint = string.Empty;
    OnAnyEditableChanged();
}

private bool _suppressMismatchOnNextRootChange;
```

The `_suppressMismatchOnNextRootChange` flag avoids clearing the hint as a side-effect of auto-population (which is the parser's own action, not the operator's).

### `DismissRootMismatchHintCommand`

Optional but tiny — `[RelayCommand] private void DismissRootMismatchHint() => RootMismatchHint = string.Empty;` — bound to the X button on the inline hint banner in step 03.

### Tests

Extend `Tests/Desktop/ScriptEditorViewModelTests.cs`:

1. `OnSeedQueryChanged_PickerEmpty_AutoPopulatesFromSql` — fresh VM; set `SeedQuery = "SELECT * FROM dbo.Patient"`; assert `RootSchema == "dbo"`, `RootTable == "Patient"`, `RootMismatchHint` empty.
2. `OnSeedQueryChanged_PickerMatchesParsed_NoHint` — preset picker to `(dbo, Patient)`; set matching SQL; hint stays empty.
3. `OnSeedQueryChanged_PickerDiffersFromParsed_SetsHint` — preset picker to `(dbo, Visit)`; SQL targets `dbo.Patient`; hint mentions both.
4. `OnSeedQueryChanged_NoFromInSql_HintCleared` — start with hint set; change SQL to `SELECT 1`; hint clears (when picker empty).
5. `OnRootSchemaChanged_AfterMismatchHint_ClearsHint` — operator picks new root via picker → hint clears.
6. `AutoPopulate_DoesNotIncrementSaveCounter_BeyondSqlChange` — set SQL; assert exactly ONE save callback fires (not two — the auto-populated picker change should NOT trigger an extra save). Implementation note: auto-populate sets RootSchema/RootTable through a flag-guarded path; OnAnyEditableChanged still fires for SeedQuery once.

  **Decision point:** if implementing the auto-populate triggers extra save callbacks, the simplest fix is to wrap the assignments in a `_suppressSaveDuringAutoPopulate` flag similar to `_loading`. Document the choice.

7. `DismissRootMismatchHint_ClearsHint`.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-01 N + 7 (or 6 if dismiss command is dropped), all green.

## Acceptance Criteria

- [ ] `ScriptEditorViewModel` has `RootMismatchHint` observable property.
- [ ] `OnSeedQueryChanged` calls `SeedQueryParser.TryExtract` + auto-populates / sets / clears the hint per the matrix.
- [ ] Manual picker change clears the hint without a flag-flip cycle.
- [ ] All new tests pass; existing 13 `ScriptEditorViewModelTests` continue to pass byte-identically.
- [ ] No new tripwires introduced (no `using System.Windows*` in the VM; no `MessageBox.Show`; no `Dispatcher.Invoke`).

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 1, § 3.
- Depends on: [01 — `SeedQueryParser`](./01-desktop-seed-tab-root-inference-parser.md).
