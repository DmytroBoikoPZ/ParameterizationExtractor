# Desktop Extras Tab — Architecture Overview

> Standalone-tables half of M7. The Extras tab edits `TablesToProcess` entries that the FK walker won't reach from the seed — lookup tables, reference data, anything operators want extracted independently. Reuses the same engine model the Graph tab edits; what differs is the **filter**.

---

## 1. Pipeline / Integration

```
Workspace open                    Graph hydrate                 Operator opens Extras tab
       │                                │                                  │
       ▼                                ▼                                  ▼
MainWindowViewModel              IGraphBuilder.BuildAsync          Extras list = TablesToProcess
.OnWorkspaceChanged              → ReachableGraph                  filtered to entries NOT in
                                 → GraphViewModel.AllNodes         GraphViewModel.AllNodes
       │                                │                                  │
       ▼                                ▼                                  │
Seed.Hydrate                     Graph.Hydrate                             │
Graph.Hydrate                    (already covered)                         │
Extras.Hydrate ← NEW                                                       │
       │                                │                                  │
       └────────────── Seed.SelectedScript change ──────────────────────────┘
                       (PropertyChanged → Graph.SetAnchor → Extras.SetAnchor)
                       single-script context across all three tabs
```

| Stage | What happens | Where |
|-------|--------------|-------|
| 1. Workspace open | `OnWorkspaceChanged` fans out to Seed/Graph/Extras hydrate | `MainWindowViewModel.OnWorkspaceChanged` |
| 2. Anchor selection | `Seed.SelectedScript` → `Graph.SetAnchor` (existing) + `Extras.SetAnchor` (new) | `MainWindowViewModel` ctor PropertyChanged subscription |
| 3. Filter compute | `ExtrasViewModel.RecomputeStandalone()` runs whenever `Workspace.Package.Scripts` mutates, anchor changes, or `GraphViewModel.AllNodes` raises `INotifyCollectionChanged` | `ExtrasViewModel` |
| 4. Edit | Operator opens row → Strategy/Where/Excluded edits flow through `StandaloneTableViewModel.OnAnyEditableChanged` → debounced `_saveCallback` (= `MainWindowViewModel.SaveWorkspaceAsync`) | `StandaloneTableViewModel` (one per row) |
| 5. Add | "+ Add table" → `IDialogService.ShowAddStandaloneTableDialogAsync` returns `TableRef?` → new `TableToExtract` appended to current script's `TablesToProcess` → save | `ExtrasViewModel.AddStandaloneTableAsync` |
| 6. Remove | Per-row Remove → `TablesToProcess.RemoveAt` → save → row vanishes | `StandaloneTableViewModel.RemoveCommand` (delegates to ExtrasVM) |

---

## 2. Component Diagram

```
+--------------------------------------------------------------------------+
|  ParameterizationExtractor.Desktop                                       |
|                                                                          |
|  Views/Extras/                              (NEW)                        |
|   ├── ExtrasView.xaml                                                    |
|   ├── ExtrasView.xaml.cs (InitializeComponent only)                      |
|   ├── ExtrasViewModel.cs                                                 |
|   │     + StandaloneTables : ObservableCollection<StandaloneTableVm>     |
|   │     + AddStandaloneTableCommand                                      |
|   │     + AnchorScript : SourceForScript?                                |
|   │     + Bind(Func<Task> saveCallback)                                  |
|   │     + Hydrate(WorkspaceModel, plaintext)                             |
|   │     + SetAnchor(SourceForScript?)                                    |
|   └── StandaloneTableViewModel.cs                                        |
|         + Schema / Name / StrategyChoice / Where / Excluded              |
|         + RemoveCommand                                                  |
|                                                                          |
|  Controls/WhereFilterEditor/                (NEW — extracted)            |
|   ├── WhereFilterEditorView.xaml(.cs)                                    |
|   └── exposes Text DP only (single-line SqlEditor wrapper)               |
|                                                                          |
|  Controls/StrategyPicker/                   (NEW — extracted)            |
|   ├── StrategyPickerView.xaml(.cs)                                       |
|   └── exposes StrategyChoice DP (StrategyKind, two-way)                  |
|                                                                          |
|  Controls/TablePicker/                      (existing — reused for Add)  |
|                                                                          |
|  Services/Dialogs/IDialogService            (extended)                   |
|   └── new method: ShowAddStandaloneTableDialogAsync(IList<TableRef>)     |
|                                                                          |
|  MainWindowViewModel                        (extended)                   |
|   + ExtrasViewModel Extras                                               |
|   + Extras.Bind(SaveWorkspaceAsync) on ctor                              |
|   + Extras.Hydrate / SetAnchor wired into the existing fan-out           |
|                                                                          |
|  Views/Graph/NodeInspectorView.xaml         (refactored)                 |
|   - inline RadioButton group → <strategy:StrategyPickerView/>            |
|   - inline <editor:SqlEditorView IsSingleLine=True/> → <where:.../>      |
|   - behaviour identical; only XAML composition changes                   |
|                                                                          |
+--------------------------------------------------------------------------+

No engine work. No new project references.
```

---

## 3. Data Flow

### 3.1 Inbound / Hydration

```
WorkspaceModel
  └── Package.Scripts[*]
        └── TablesToProcess[*]   ← THE list Extras edits

ExtrasViewModel.Hydrate(workspace, plaintext)
  ├── _workspace = workspace
  ├── _connectionString = WorkspaceConnectionStringBuilder.Build(...)
  └── RecomputeStandalone()
        ├── script = AnchorScript ?? FirstScript
        ├── all = script.TablesToProcess
        ├── reachableKeys = GraphViewModel.AllNodes.Select(n => (n.Schema, n.Name)).ToHashSet()
        │     • case-insensitive (Schema, Name) tuples
        │     • empty when graph hasn't hydrated yet
        ├── visible = all.Where(t => !reachableKeys.Contains((t.Schema, t.Name)))
        │     • when reachableKeys is empty (graph not loaded), ALL entries shown
        │       (graceful degradation: better than empty + confusing)
        └── StandaloneTables.Clear() + repopulate from `visible` as StandaloneTableViewModel
```

### 3.2 Filter trigger

`RecomputeStandalone()` runs when ANY of:

- `Hydrate()` invoked
- `SetAnchor()` invoked (operator changed Seed.SelectedScript)
- `GraphViewModel.AllNodes.CollectionChanged` fires (graph hydrated or refreshed)
- `AddStandaloneTable` / `RemoveStandaloneTable` mutated the underlying list

Subscription to `GraphViewModel.AllNodes.CollectionChanged` happens in `ExtrasViewModel` ctor (Singleton; same lifetime as `GraphViewModel`).

### 3.3 Outbound / Save

```
StandaloneTableViewModel field edit
  └── OnAnyEditableChanged
        ├── mutate Source.ExtractStrategy / .Where / .Excluded in-place (mirrors NodeInspectorViewModel)
        ├── _saveCts.Cancel()
        ├── _saveCts = new CancellationTokenSource()
        └── SaveDebouncedAsync(_saveCts.Token)
              ├── await Task.Delay(500ms, ct)
              └── await _saveCallback()  // = MainWindowViewModel.SaveWorkspaceAsync
```

---

## 4. Data Stores Summary

No new stores. Edits flow through the existing `IWorkspaceStore.SaveAsync(workspace, path)` JSON writer.

The engine model (`TableToExtract`, `ExtractStrategy`, `SqlBuildStrategy`) is **unchanged**. Extras and Graph tab edit the same `Workspace.Package.Scripts[*].TablesToProcess` collection — they're two views over one list.

---

## 5. Extension Points

- **Pre/post-extraction scripts (deferred to follow-up features):** the Extras tab's `<Grid>` is laid out so a second section can be appended below the standalone-tables list. `ExtrasView.xaml`'s outer container uses `Grid.RowDefinitions` with explicit Auto rows; the script section slots in as a new row. `ExtrasViewModel` will gain `PreScripts` / `PostScripts` collections in `desktop-extras-scripts`.
- **Bulk operations:** the per-row `StandaloneTableViewModel` is independent of the parent collection; future "select multiple → set strategy on all" can compose by iterating `StandaloneTables`.

---

## 6. Security & Isolation

No new trust boundaries. Edits run in-process; persistence goes through the existing `IWorkspaceStore` (DPAPI-encrypted password untouched). Add-table dialog presents only the tables the workspace's connection can already see — no new credentials, no new network surface.

---

## 7. Risks & Open Questions

- **Graph hydration latency vs Extras render.** If the operator switches to Extras before the graph finishes loading, `reachableKeys` is empty and Extras shows ALL `TablesToProcess`. As graph hydrates, the list narrows. The transition is observable but harmless. Mitigation accepted as v1 behaviour; flag with a small "computing filter…" hint above the list while `GraphViewModel.RefreshCommand.IsRunning`.
- **Reverse migration.** If an operator adds a "lookup" table via Extras and configures `OnlyOneTable`, then later adds an FK-connected ancestor that brings the lookup into the FK reachable set, the table migrates from Extras to Graph view automatically. This matches operator intent (the table is no longer "outside the graph"). Document in the per-row tooltip if it confuses smoke testing.
- **Inspector overlap with Graph tab.** A table that's currently visible in Extras (because graph hasn't reached it) could also appear in Graph if a future operator action exposes the FK path. Both editors point to the same `TableToExtract` instance, so edits in either view are immediately visible in the other on next refresh. No duplication.
- **Empty-state UX.** "No standalone tables yet — everything in this script's TablesToProcess is reachable from the seed via FKs" — friendlier than blank.
- **Add-table dialog reuse.** `IDialogService.ShowAddStandaloneTableDialogAsync` is the second consumer of `Controls/TablePicker/`. If a third dialog appears (e.g., add-script-target later), the dialog method may generalise. Defer until that materialises.

---

## 8. What this feature does NOT build

- Pre/post-extraction script editor (UI half) — `desktop-extras-scripts`
- Pre/post-extraction script engine support — `engine-pre-post-scripts`
- A `Standalone` flag on the engine model — the filter is computed, not persisted
- "Add from file…" external script import
- Variable substitution in scripts
- Per-script "skip on dry-run" flag
- Multi-script bulk add / edit
