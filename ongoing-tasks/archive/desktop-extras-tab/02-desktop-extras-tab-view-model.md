# 02 — desktop-extras-tab — `ExtrasViewModel` + `StandaloneTableViewModel` + tests

## Goal

Land the VM layer: `ExtrasViewModel` (Singleton) + `StandaloneTableViewModel` (Transient, one per row). Filters `TablesToProcess` to entries NOT in `GraphViewModel.AllNodes`. Add / Remove / Edit; debounced save-on-change mirroring `NodeInspectorViewModel`. **No view yet** — step 03.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- `desktop-graph-tab` baseline: [`GraphViewModel.AllNodes`](../../ParameterizationExtractor.Desktop/Views/Graph/GraphViewModel.cs) (Singleton; emits `INotifyCollectionChanged` via `ObservableCollection`); `NodeInspectorViewModel` save-on-change pattern with `_saveCts` + `SaveDebouncedAsync`; `StrategyKind` enum (now under `Controls/StrategyPicker/` per step 01).
- [`Logic.Configs.SourceForScript`](../../ParameterizationExtractor.Logic/Configs/SourceForScript.cs) and `Logic.Model.TableToExtract` — the engine model edited in-place.
- [`Logic.Schema.IDatabaseExplorer`](../../ParameterizationExtractor.Logic/Schema/IDatabaseExplorer.cs) — `ListTablesAsync` returns `IReadOnlyList<TableRef>` for the Add-table dialog (used in step 03; the VM exposes the list).
- [`Desktop/Services/Threading/IUiDispatcher`](../../ParameterizationExtractor.Desktop/Services/Threading/IUiDispatcher.cs) — for marshalling `INotifyCollectionChanged` callbacks back onto the UI thread.

## What to Build

### `Desktop/Views/Extras/StandaloneTableViewModel.cs` (new)

`internal sealed partial class StandaloneTableViewModel : ObservableObject`. One per row. Transient.

Constructor:
```csharp
public StandaloneTableViewModel(
    TableToExtract source,
    Func<Task> saveCallback,
    Func<StandaloneTableViewModel, Task> removeCallback,
    ILogger<StandaloneTableViewModel> log,
    TimeSpan? saveDebounce = null);
```

Observable properties (mirrors `NodeInspectorViewModel`):
```csharp
[ObservableProperty] private StrategyKind _strategyChoice;
[ObservableProperty] private string _where = string.Empty;
[ObservableProperty] private bool _excluded;
[ObservableProperty] private bool _isExpanded;   // row expanded into edit form
```

Read-only:
```csharp
public string Schema { get; }
public string Name { get; }
public string Display => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
public string StrategyChip => StrategyChoice.ToString();
public TableToExtract Source { get; }
```

Save-on-change wiring identical to `NodeInspectorViewModel`'s `OnAnyEditableChanged` + `SaveDebouncedAsync` (500ms default; cancellable per `_saveCts`). `_loading` guard for the constructor's hydration so the initial property-set doesn't trip a save.

`[RelayCommand] private async Task RemoveAsync() => await _removeCallback(this);` — delegates to the parent VM.

`[RelayCommand] private void ToggleExpand() => IsExpanded = !IsExpanded;`

### `Desktop/Views/Extras/ExtrasViewModel.cs` (new)

`internal sealed partial class ExtrasViewModel : ObservableObject`. Singleton. DI registration in `App.xaml.cs` (or `DesktopHost.cs`) via `services.AddSingleton<ExtrasViewModel>()`.

Constructor:
```csharp
public ExtrasViewModel(
    GraphViewModel graph,
    IDatabaseExplorer explorer,
    IDialogService dialog,
    IUiDispatcher ui,
    ILoggerFactory loggerFactory);
```

Holds:
```csharp
public ObservableCollection<StandaloneTableViewModel> StandaloneTables { get; } = new();
public ObservableCollection<TableRef> Tables { get; } = new();   // available pick set, mirrors SeedViewModel.Tables

[ObservableProperty] private SourceForScript? _anchorScript;

private WorkspaceModel? _workspace;
private string? _connectionString;
private Func<Task>? _saveCallback;

[ObservableProperty] private string _emptyHint = string.Empty;
```

Methods:
```csharp
internal void Bind(Func<Task> saveCallback);

public void Hydrate(WorkspaceModel? workspace, string? plaintextPassword);
public void SetAnchor(SourceForScript? script);

[RelayCommand(CanExecute = nameof(CanAdd))]
private async Task AddStandaloneTableAsync();

private async Task RemoveStandaloneTableAsync(StandaloneTableViewModel row);
```

In ctor: subscribe to `_graph.AllNodes.CollectionChanged` and re-run `RecomputeStandalone()` on the UI thread when it fires.

`Hydrate`:
- Stores `_workspace` + builds `_connectionString`.
- Calls `RecomputeStandalone()`.
- Fires `_ = RefreshTablesAsync()` (mirrors `SeedViewModel.RefreshTablesAsync` — populates `Tables`).
- Clears all when workspace null.

`SetAnchor(script)`:
- Sets `AnchorScript = script`; calls `RecomputeStandalone()`.

`RecomputeStandalone()` (private):
```
script = AnchorScript ?? Workspace.Package.Scripts.FirstOrDefault()
all = script?.TablesToProcess ?? []
reachableKeys = _graph.AllNodes
    .Select(n => (n.Schema, n.Name))
    .ToHashSet(SchemaNameComparer.Instance)

visible = reachableKeys.Count == 0
    ? all                                        // graceful: graph not loaded yet → show all
    : all.Where(t => !reachableKeys.Contains((t.Schema ?? "", t.TableName)))

StandaloneTables.Clear()
foreach v in visible:
    StandaloneTables.Add(new StandaloneTableViewModel(v, SaveAsync, RemoveStandaloneTableAsync, _log, _saveDebounce))

EmptyHint = visible.Any() ? "" :
    "No standalone tables yet — everything in this script's TablesToProcess is reachable from the seed via FKs."
```

`AddStandaloneTableAsync` (step 03 wires the dialog; this step provides the command + the engine-model insertion path):
```
var pick = await _dialog.ShowAddStandaloneTableDialogAsync(Tables)
if pick is null: return
script = AnchorScript ?? FirstScript ; if null, no-op
entry = new TableToExtract(pick.Name, new OnlyOneTableExtractStrategy(), new SqlBuildStrategy()) { Schema = pick.Schema }
script.TablesToProcess.Add(entry)
RecomputeStandalone()        // brings the new row into view
await _saveCallback()
```

Default strategy is `OnlyOneTable` (matches the M7 mockup — by far the common standalone choice).

`RemoveStandaloneTableAsync(row)`:
```
script = AnchorScript ?? FirstScript ; if null return
script.TablesToProcess.Remove(row.Source)
StandaloneTables.Remove(row)
EmptyHint = StandaloneTables.Count == 0 ? "..." : ""
await _saveCallback()
```

`CanAdd` returns `_workspace is not null && (AnchorScript ?? FirstScript) is not null`.

### `IDialogService` extension

Add to [`IDialogService`](../../ParameterizationExtractor.Desktop/Services/Dialogs/IDialogService.cs):
```csharp
Task<TableRef?> ShowAddStandaloneTableDialogAsync(IReadOnlyList<TableRef> available);
```

The fake [`FakeDialogService`](../../Tests/Desktop/Fakes/FakeDialogService.cs) gets the corresponding stub returning a queued result. Step 03 adds the WPF impl; step 02 only needs the contract + fake for tests.

### Tests

New file `Tests/Desktop/ExtrasViewModelTests.cs`. Harness mirrors `GraphViewModelTests` shape (uses `FakeGraphBuilder`, `FakeDatabaseExplorer`, `FakeUiDispatcher`, `FakeDialogService`).

1. `Default_State_NoTables_HintEmpty`.
2. `Hydrate_NullWorkspace_ClearsAll`.
3. `Hydrate_GraphHasNoMatches_AllTablesAreStandalone` — graph reachable={Patient}; `TablesToProcess`=[Patient, LookupCountry, LookupCurrency]; standalone=[LookupCountry, LookupCurrency].
4. `Hydrate_GraphReachableMatchesAll_NoStandaloneTables` — graph reachable={Patient, Visit}; `TablesToProcess`=[Patient, Visit]; standalone=[]; `EmptyHint` non-empty.
5. `Hydrate_GraphNotLoadedYet_FallsBackToShowAll` — graph reachable={}; `TablesToProcess`=[Lookup1]; standalone=[Lookup1] (graceful).
6. `OnGraphAllNodesChanged_RecomputesFilter` — start with empty graph (Lookup1 visible). Push a node into `AllNodes` matching `Lookup1`. After `INotifyCollectionChanged`, `StandaloneTables` no longer contains `Lookup1`.
7. `SetAnchor_TwoScripts_FiltersByAnchorScriptOnly` — script s1 has Lookup1; s2 has Lookup2. SetAnchor(s2) → standalone=[Lookup2].
8. `AddStandaloneTableAsync_DialogReturnsTable_AppendsToScriptAndSaves` — fake dialog returns `("dbo","NewLookup")`; new `TableToExtract` appears in `script.TablesToProcess` with `OnlyOneTable` strategy + `Schema="dbo"`; save fired.
9. `AddStandaloneTableAsync_DialogReturnsNull_NoOp`.
10. `RemoveStandaloneTableAsync_RemovesFromScriptAndSaves`.
11. `Hydrate_PopulatesTablesViaExplorer` — `Tables` collection has the explorer's results (mirrors SeedViewModel pattern).

New file `Tests/Desktop/StandaloneTableViewModelTests.cs`. Mirrors `NodeInspectorViewModelTests` for the save-on-change debounce semantics.

12. `Construction_HydratesFromSource_NoSave`.
13. `OnStrategyChoiceChanged_ReplacesEngineStrategy_PreservesWhere`.
14. `OnWhereChanged_MutatesEngineWhere_FiresSave`.
15. `OnExcludedChanged_MutatesEngineExcluded_FiresSave`.
16. `RemoveAsync_DelegatesToParentCallback`.
17. `MultipleRapidEdits_DebouncedToSingleSave` (use 50ms debounce).

`HostCompositionTests` extension: assert `ExtrasViewModel` resolves from the host (DI smoke).

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-01 N + (11 + 6 + 1) ≈ N + 18, all green.

## Acceptance Criteria

- [ ] `Desktop/Views/Extras/{ExtrasViewModel.cs, StandaloneTableViewModel.cs}` exist; `internal sealed partial`; CTK source-generators throughout (no hand-rolled INotify).
- [ ] `ExtrasViewModel` registered as Singleton in DI; resolves successfully in `HostCompositionTests`.
- [ ] Filter computed from `_graph.AllNodes`; subscribes to `INotifyCollectionChanged` for re-filter on graph hydrate.
- [ ] Graceful fallback: when `_graph.AllNodes` is empty, ALL `TablesToProcess` shown (not zero).
- [ ] Add / Remove edit the engine model in-place; debounced save fires through `_saveCallback`.
- [ ] `IDialogService.ShowAddStandaloneTableDialogAsync` contract added; `FakeDialogService` stubbed.
- [ ] All ~17 new tests pass; existing suites untouched.
- [ ] No tripwires: no WPF types in VMs; no `Dispatcher.Invoke` direct (use `IUiDispatcher`); no `MessageBox.Show`.

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 2, § 3.
- Pattern exemplars: `SeedViewModel` (Tables collection refresh, anchor signal source), `GraphViewModel` (`Bind`, `Hydrate`, `SetAnchor`), `NodeInspectorViewModel` (save-on-change debounce + `[NotifyCanExecuteChangedFor]`).
- Depends on: [01 — Extract controls](./01-desktop-extras-tab-extract-controls.md) for `StrategyKind`'s new namespace.
