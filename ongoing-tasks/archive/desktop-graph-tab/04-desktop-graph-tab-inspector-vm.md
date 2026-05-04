# 04 — desktop-graph-tab — `NodeInspectorViewModel` + tests

## Goal

Land the VM that backs the slide-in inspector pane. Edits Strategy / Where / Excluded on the engine's `TableToExtract` for the inspected node; supports adding a Pending node to extract; mirrors save-on-blur from the seed-tab pattern. **No view yet** — step 05.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`Desktop/Views/Seed/ScriptEditorViewModel.cs`](../../ParameterizationExtractor.Desktop/Views/Seed/ScriptEditorViewModel.cs) — pattern exemplar for save-on-blur + parent save-callback.
- `GraphViewModel.InspectedNode` (step 03) — what triggers the inspector to open.
- `Logic.Model.{TableToExtract, ExtractStrategy, FKDependencyExtractStrategy, OnlyChildrenExtractStrategy, OnlyParentExtractStrategy, OnlyOneTableExtractStrategy, SqlBuildStrategy}` — engine model.

## What to Build

### `Desktop/Views/Graph/StrategyKind.cs` (new)

```csharp
internal enum StrategyKind { FKDependency, OnlyChildren, OnlyParent, OnlyOneTable }
```

### `Desktop/Views/Graph/NodeInspectorViewModel.cs`

`internal sealed partial class NodeInspectorViewModel : ObservableObject`. Constructor:

```csharp
public NodeInspectorViewModel(
    Func<Task> saveCallback,
    Func<Task> recomputeGraphCallback,
    ILogger<NodeInspectorViewModel> log);
```

The inspector is short-lived (one per inspected node). The host (`GraphView`) instantiates it on `InspectedNode` change. Singleton vs Transient: **Transient** — fresh state per node.

**Initialization:**

```csharp
public void Open(GraphNodeViewModel node, WorkspaceModel workspace, SourceForScript currentScript)
{
    Node = node;
    _workspace = workspace;
    _currentScript = currentScript;
    Source = currentScript.TablesToProcess
        .FirstOrDefault(t =>
            (string.IsNullOrEmpty(t.Schema) || t.Schema.Equals(node.Schema, StringComparison.OrdinalIgnoreCase))
            && t.TableName.Equals(node.Name, StringComparison.OrdinalIgnoreCase));

    HydrateFromSource();
}

private void HydrateFromSource()
{
    _loading = true;
    try
    {
        if (Source is null)
        {
            // Pending node — show "Add to extract" affordance.
            StrategyChoice = StrategyKind.FKDependency;
            Where = string.Empty;
            Excluded = false;
            return;
        }
        StrategyChoice = ToStrategyKind(Source.ExtractStrategy);
        Where = Source.ExtractStrategy?.Where ?? string.Empty;
        Excluded = Source.Excluded;
    }
    finally { _loading = false; }
}
```

**Observable properties:**

```csharp
[ObservableProperty] private GraphNodeViewModel? _node;
[ObservableProperty] private TableToExtract? _source;
[ObservableProperty] private StrategyKind _strategyChoice = StrategyKind.FKDependency;
[ObservableProperty] private string _where = string.Empty;
[ObservableProperty] private bool _excluded;
public bool IsAddable => Source is null;
```

**Save-on-change wiring:**

```csharp
partial void OnStrategyChoiceChanged(StrategyKind value) => OnAnyEditableChanged(replaceStrategy: true);
partial void OnWhereChanged(string value)               => OnAnyEditableChanged();
partial void OnExcludedChanged(bool value)              => OnAnyEditableChanged();

private void OnAnyEditableChanged(bool replaceStrategy = false)
{
    if (_loading || Source is null) return;

    if (replaceStrategy)
    {
        Source.ExtractStrategy = StrategyChoice switch
        {
            StrategyKind.FKDependency  => new FKDependencyExtractStrategy { Where = Where },
            StrategyKind.OnlyChildren  => new OnlyChildrenExtractStrategy { Where = Where },
            StrategyKind.OnlyParent    => new OnlyParentExtractStrategy   { Where = Where },
            StrategyKind.OnlyOneTable  => new OnlyOneTableExtractStrategy { Where = Where },
            _ => Source.ExtractStrategy,
        };
    }
    else
    {
        if (Source.ExtractStrategy is { } s) s.Where = Where;
    }

    Source.Excluded = Excluded;

    _ = _saveCallback();   // fire-and-forget; mirrors seed-tab
}
```

**Commands:**

```csharp
[RelayCommand(CanExecute = nameof(IsAddable))]
private async Task AddToExtractAsync()
{
    if (_workspace is null || _currentScript is null || Node is null) return;

    var entry = new TableToExtract(Node.Name, new FKDependencyExtractStrategy(), new SqlBuildStrategy())
    {
        Schema = Node.Schema,
    };
    _currentScript.TablesToProcess.Add(entry);
    Source = entry;     // morph the inspector into "edit" mode
    OnPropertyChanged(nameof(IsAddable));
    await _saveCallback();
    await _recomputeGraphCallback();
}

[RelayCommand]
private async Task RecomputeGraphAsync() => await _recomputeGraphCallback();
```

### Tests

New file `Tests/Desktop/NodeInspectorViewModelTests.cs`:

1. `Open_ConfiguredNode_HydratesStrategyAndWhere`.
2. `Open_PendingNode_IsAddableTrue_FieldsDefault`.
3. `OnStrategyChoiceChanged_ReplacesEngineStrategy_PreservesWhere`.
4. `OnWhereChanged_MutatesEngineWhere_FiresSave`.
5. `OnExcludedChanged_MutatesEngineExcluded_FiresSave`.
6. `AddToExtractAsync_CreatesTableToExtractEntry_FiresSaveAndRecompute`.
7. `AddToExtractAsync_SetsSourceAndIsAddableFalse`.
8. `HydrateFromSource_DoesNotFireSave` — `_loading` guard works.
9. `RecomputeGraphAsync_DelegatesToCallback`.

Use a fake save callback that increments a counter and a fake recompute callback that records calls. Pattern: mirror `ScriptEditorViewModelTests`'s harness.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-03 N + 9, all green.

## Acceptance Criteria

- [ ] `Desktop/Views/Graph/{StrategyKind.cs, NodeInspectorViewModel.cs}` exist.
- [ ] Strategy radio mutates engine model in-place (replaces `ExtractStrategy` with the matching kind, preserves Where).
- [ ] Where editor save-on-change.
- [ ] Excluded checkbox save-on-change.
- [ ] `AddToExtractAsync` creates the engine entry + flips inspector to edit mode + recomputes graph.
- [ ] `HydrateFromSource` guarded against save-loop.
- [ ] All 9 tests pass.

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 4, § 7.
- Pattern exemplars: `ScriptEditorViewModel` (save-on-blur), v1 `GraphViewModel.MapAndApply` (strategy-kind reverse mapping).
- Depends on: [03 — Click-dispatch](./03-desktop-graph-tab-click-dispatch.md).
