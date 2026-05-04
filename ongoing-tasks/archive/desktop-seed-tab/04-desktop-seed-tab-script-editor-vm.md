# 04 — desktop-seed-tab — ScriptEditorViewModel (single-script editor with preview + save-on-blur)

## Goal

Land `ScriptEditorViewModel` — the per-script VM that hosts one editable script (name, root, query) plus the preview command (cancellable, dispatcher-marshalled) plus the debounced save-on-blur logic. **Pure VM** — no parent VM, no view, no `MainWindowViewModel` changes yet. Steps 05 and 06 land the container and the wiring. Splitting the seed tab into VM-by-VM-then-view keeps each step under control.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- `IDatabaseExplorer` (step 01), `IUiDispatcher` (step 02), `Controls/SqlEditor` + `Controls/TablePicker` + `Controls/RowsPreviewGrid` (step 03).
- Engine `SourceForScript` (read first to confirm the field names — `ScriptName`, `Query`, root reference). The VM mutates this instance directly on save-back.
- [`Controls/ConnectionEditor/ConnectionEditorViewModel.cs`](../../ParameterizationExtractor.Desktop/Controls/ConnectionEditor/ConnectionEditorViewModel.cs) — pattern exemplar for `[RelayCommand(IncludeCancelCommand = true)]` + status-state machine.
- `Tests/Desktop/Fakes/{FakeDatabaseExplorer.cs, FakeUiDispatcher.cs}` (steps 01/02/03).

## What to Build

### `Views/Seed/ScriptEditorViewModel.cs`

`internal sealed partial class ScriptEditorViewModel : ObservableObject`. Constructor:

```csharp
public ScriptEditorViewModel(
    SourceForScript source,
    IDatabaseExplorer explorer,
    IUiDispatcher ui,
    Func<Task> saveCallback,
    ILogger<ScriptEditorViewModel> log,
    TimeSpan? saveDebounce = null)
```

- `_source`: the `SourceForScript` instance from `Workspace.Package.Scripts[i]`. The VM owns mutation of this object on save-back.
- `_saveDebounce`: defaults to `TimeSpan.FromMilliseconds(500)`. Tests pass `TimeSpan.Zero` to fire saves synchronously.
- `_saveCts`: per-instance `CancellationTokenSource`, swapped on every editable-property change.
- `_connectionString`: nullable; set via `Initialize(string?)` from the parent.

**Editable observable properties** (each setter triggers `OnAnyEditableChanged`):
- `[ObservableProperty] private string _scriptName = "";`
- `[ObservableProperty] private string _rootSchema = "";`
- `[ObservableProperty] private string _rootTable = "";`
- `[ObservableProperty] private string _seedQuery = "";`

**Transient observable properties** (NOT triggers — never persisted):
- `[ObservableProperty] private PreviewResult? _previewResult;`
- `[ObservableProperty] private string _previewStatus = "";`

**Computed:**
- `public bool IsRunningPreview => RunPreviewCommand.IsRunning;` — the source-gen `IRelayCommand.IsRunning` is observable; wire INPC manually if needed (CTK.MVVM gen surfaces it).
- `public string RootDisplay => string.IsNullOrEmpty(RootSchema) ? RootTable : $"{RootSchema}.{RootTable}";`
- `public TableRef? RootRef { get => string.IsNullOrEmpty(RootTable) ? null : new TableRef(RootSchema, RootTable); set { RootSchema = value?.Schema ?? ""; RootTable = value?.Name ?? ""; } }` — used by the TablePicker binding (step 06's view).

**Public methods:**
- `public void Initialize(string? connectionString) => _connectionString = connectionString;`
- `public void HydrateFromSource()` — copy `_source` fields into VM observable properties without triggering save (set a `_loading` flag; `OnAnyEditableChanged` checks it). Called once at construction.

**Commands:**

`[RelayCommand(IncludeCancelCommand = true)] private async Task RunPreviewAsync(CancellationToken ct)`:
1. `if (string.IsNullOrEmpty(_connectionString)) { PreviewStatus = "No connection — open a workspace first"; return; }`
2. `PreviewStatus = "Loading…"; PreviewResult = null;`
3. `try { var result = await _explorer.PreviewQueryAsync(_connectionString, SeedQuery, 200, ct).ConfigureAwait(false); await _ui.InvokeAsync(() => { PreviewResult = result; PreviewStatus = $"{result.Rows.Count} rows" + (result.Truncated ? " (truncated)" : ""); }); }`
4. `catch (OperationCanceledException) { await _ui.InvokeAsync(() => PreviewStatus = "Cancelled"); }`
5. `catch (DatabaseExplorerException ex) { await _ui.InvokeAsync(() => PreviewStatus = $"Failed: {ex.Message}"); }`
6. `_log.LogInformation("Preview run for {Script} ended {Status}", ScriptName, PreviewStatus);`

The `ConfigureAwait(false)` on the engine call lets the continuation run on a thread-pool thread (proves the dispatcher matters); `_ui.InvokeAsync` brings the observable mutation back. `FakeUiDispatcher` runs inline in tests.

**Save-on-blur:**

```csharp
private bool _loading;
private CancellationTokenSource? _saveCts;

private void OnAnyEditableChanged()
{
    if (_loading) return;
    _saveCts?.Cancel();
    _saveCts = new CancellationTokenSource();
    var ct = _saveCts.Token;
    _ = SaveDebouncedAsync(ct);
}

private async Task SaveDebouncedAsync(CancellationToken ct)
{
    try
    {
        if (_saveDebounce > TimeSpan.Zero)
            await Task.Delay(_saveDebounce, ct).ConfigureAwait(true);

        if (ct.IsCancellationRequested) return;

        WriteThroughToSource();
        await _saveCallback().ConfigureAwait(true);
    }
    catch (OperationCanceledException) { /* expected */ }
}

private void WriteThroughToSource()
{
    _source.ScriptName = ScriptName;
    _source.Query = SeedQuery;
    // RootSchema + RootTable mapping depends on SourceForScript's actual root field — verify against the model
    // and update accordingly (likely something like _source.RootSchema = RootSchema; _source.RootTable = RootTable;
    // OR a single _source.RootRecordsToExtract.{Schema,Name} navigation).
}
```

CTK.MVVM partial-method hooks (one per editable property):

```csharp
partial void OnScriptNameChanged(string value) => OnAnyEditableChanged();
partial void OnRootSchemaChanged(string value) => OnAnyEditableChanged();
partial void OnRootTableChanged(string value) => OnAnyEditableChanged();
partial void OnSeedQueryChanged(string value) => OnAnyEditableChanged();
```

`HydrateFromSource` sets `_loading = true; ScriptName = _source.ScriptName; ...; _loading = false;` so the initial copy doesn't trigger a save.

VMs never reference WPF types — verified by grep on the file (no `using System.Windows*`).

### **Important model touchpoint**

`SourceForScript` may not currently have separate `RootSchema` + `RootTable` fields — it likely has a richer `RecordsToExtract` shape. Two options:

- (a) **Augment `SourceForScript`** with explicit `RootSchema` + `RootTable` properties that set/get against the existing root-record structure. Engine model change ride-in.
- (b) **VM-side translation only** — `WriteThroughToSource` parses the existing root structure to set fields; `HydrateFromSource` reverses.

**Decision:** pick (b) only if the existing model already has a single canonical "root table reference" field (likely). Pick (a) if the model is awkward (the RootRef navigation is multi-step). **Verify in implementation; document the choice in Notes.** Keep the engine model change minimal if needed.

### Tests

Per `prompts/execution.md` — CODE: TDD cycle.

New file `Tests/Desktop/ScriptEditorViewModelTests.cs`:

Setup helper: build a fresh `ScriptEditorViewModel` with `FakeDatabaseExplorer`, `FakeUiDispatcher`, and a captured save-callback `Func<Task>` that increments a counter. Default `saveDebounce = TimeSpan.Zero` so saves fire synchronously.

1. `Default_State_PreviewStatusEmpty` — fresh VM after `HydrateFromSource` → `PreviewStatus == ""`, `PreviewResult == null`.
2. `HydrateFromSource_PopulatesFieldsWithoutTriggeringSave` — preset `_source` fields; new VM; assert no save callback invoked.
3. `RunPreview_NoConnectionString_SetsStatus` — `Initialize(null)`; execute command; status mentions "No connection".
4. `RunPreview_Success_PopulatesResultAndStatus` — Enqueue `PreviewResult(["a","b"], [["1","2"]], false)`; assert `PreviewResult` set + `PreviewStatus == "1 rows"`.
5. `RunPreview_Truncated_StatusMentionsTruncated`.
6. `RunPreview_Failure_SetsStatusToFailedMessage` — Enqueue `DatabaseExplorerException("login failed")`; status starts with `"Failed:"` and contains `"login failed"`.
7. `RunPreview_Cancellation_SetsStatusToCancelled` — Enqueue `OperationCanceledException`.
8. `RunPreview_PassesConfiguredConnectionString` — `Initialize("Server=h;Database=d")`; execute; assert `_explorer.ConnectionStringsCalled[0]` matches.
9. `OnScriptNameChanged_DebouncedSave_FiresOnce` — set `saveDebounce = TimeSpan.Zero`; mutate `ScriptName`; assert save counter == 1.
10. `MultipleRapidEdits_DebouncedToSingleSave` — set `saveDebounce = TimeSpan.FromMilliseconds(50)`; mutate three times in succession; `await Task.Delay(120)`; assert save counter == 1.
11. `EditAfterSave_TriggersAnotherSave` — mutate, await save, mutate again, await save → counter == 2.
12. `WriteThroughToSource_PersistsAllEditableFields` — mutate all four editable properties; await save; assert `_source.ScriptName / Query / [root fields]` match.
13. `RootRef_GetterAndSetter_RoundTripThroughSchemaAndTable` — set `RootRef = new TableRef("dbo","Patient")`; assert `RootSchema == "dbo"` and `RootTable == "Patient"`. Set both fields manually; assert `RootRef` returns equivalent.

DI smoke: not applicable (this VM is constructed manually by `SeedViewModel.Hydrate` in step 05; not registered).

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-03 N + 13 = N + 13, all green.

## Acceptance Criteria

- [ ] `Views/Seed/ScriptEditorViewModel.cs` exists; `internal sealed partial`; no `using System.Windows*`.
- [ ] Constructor takes `(SourceForScript, IDatabaseExplorer, IUiDispatcher, Func<Task>, ILogger<...>, TimeSpan?)`.
- [ ] Editable properties (`ScriptName`, `RootSchema`, `RootTable`, `SeedQuery`) trigger debounced save-on-blur.
- [ ] Transient properties (`PreviewResult`, `PreviewStatus`) do NOT trigger save.
- [ ] `[RelayCommand(IncludeCancelCommand = true)]` on `RunPreviewAsync`; engine call uses `ConfigureAwait(false)` and observable mutation goes through `_ui.InvokeAsync`.
- [ ] Three exception paths covered: `OperationCanceledException` → "Cancelled"; `DatabaseExplorerException` → `"Failed: …"`; success → row count.
- [ ] `RootRef` get/set round-trips through `RootSchema` + `RootTable`.
- [ ] `HydrateFromSource` does NOT trigger a save (`_loading` guard works).
- [ ] All 13 tests pass.
- [ ] No `MessageBox.Show` / `IConfiguration[..]` / `Console.WriteLine` / `Dispatcher.Invoke` / `TODO` / `FIXME`. `IUiDispatcher` is the only dispatcher access.
- [ ] Decision documented in Notes: VM-side root translation (option b) vs engine model change (option a).
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § Service abstractions, § Threading.
- Pattern exemplars: `Controls/ConnectionEditor/ConnectionEditorViewModel.cs` (cancel pattern), `Tests/Desktop/Fakes/FakeUiDispatcher.cs` (test marshalling).
- Depends on: [01](./01-desktop-seed-tab-database-explorer.md), [02](./02-desktop-seed-tab-ui-dispatcher.md), [03](./03-desktop-seed-tab-reusable-controls.md).
