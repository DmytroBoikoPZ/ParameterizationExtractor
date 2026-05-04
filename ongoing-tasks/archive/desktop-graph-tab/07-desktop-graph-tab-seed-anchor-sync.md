# 07 — desktop-graph-tab — Seed-tab → Graph-tab anchor sync

## Goal

When the operator selects a different script in the Seed tab, the Graph tab's anchor (the seed) follows. Single-context view: the inspector edits whichever script the operator is currently focused on in the Seed tab.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`Desktop/Views/Seed/SeedViewModel.cs`](../../ParameterizationExtractor.Desktop/Views/Seed/SeedViewModel.cs) — exposes `[ObservableProperty] ScriptEditorViewModel? _selectedScript;`.
- `GraphViewModel` collects seeds from `Workspace.Package.Scripts[*].RootRecords[*]` — currently aggregates all scripts.

## What to Build

### `GraphViewModel` — single-script anchor mode

Add:

```csharp
[ObservableProperty]
private SourceForScript? _anchorScript;

public void SetAnchor(SourceForScript? script)
{
    AnchorScript = script;
    _ = RefreshCommand.ExecuteAsync(null);  // recompute against the new anchor
}
```

Modify `CollectSeeds` to use `AnchorScript?.RootRecords` instead of all scripts:

```csharp
private static IReadOnlyList<TableRef> CollectSeeds(WorkspaceModel workspace, SourceForScript? anchor)
{
    var script = anchor ?? workspace.Package?.Scripts?.FirstOrDefault();
    if (script?.RootRecords is null) return Array.Empty<TableRef>();

    var seen = new HashSet<(string, string)>(SchemaNameComparer.Instance);
    var result = new List<TableRef>();
    foreach (var root in script.RootRecords)
    {
        var schema = root.Schema ?? string.Empty;
        var name = root.TableName ?? string.Empty;
        if (string.IsNullOrEmpty(name)) continue;
        if (seen.Add((schema, name))) result.Add(new TableRef(schema, name));
    }
    return result;
}
```

`RefreshAsync` consumes `AnchorScript`. The inspector edits `AnchorScript.TablesToProcess` (single source of truth per current view).

### `MainWindowViewModel` — wire seed → graph

In ctor or as a property-changed subscription:

```csharp
Seed.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(SeedViewModel.SelectedScript))
    {
        var anchor = Seed.SelectedScript?.Source; // exposes via the internal Source getter from desktop-seed-tab
        Graph.SetAnchor(anchor);
    }
};
```

Or pass a callback to `Seed.Bind(...)` similar to the save callback — pick the cleaner shape.

### Tests

Extend `Tests/Desktop/GraphViewModelTests.cs`:

1. `SetAnchor_TriggersRefreshWithAnchorScriptSeeds` — assert `FakeGraphBuilder.SeedsCalled` is the anchor's seeds, not the union.
2. `Hydrate_NoAnchor_FallsBackToFirstScript` — when SetAnchor not called, behaviour matches v1 (first script's seeds).
3. `SetAnchor_NullScript_ClearsGraph`.
4. `Hydrate_TwoScripts_FirstSelected_SecondsRootsNotInSeeds`.

Extend `Tests/Desktop/MainWindowViewModelGraphHydrationTests.cs`:

5. `SeedSelectedScriptChanged_UpdatesGraphAnchor` — using a real `MainWindowViewModel`, change `Seed.SelectedScript`; assert `Graph.AnchorScript` updates and the rebuild fires.

### Verification

- `dotnet build` / `dotnet test` — pre-step-06 N + 5, all green.

## Acceptance Criteria

- [ ] `GraphViewModel.AnchorScript` + `SetAnchor` exist.
- [ ] `CollectSeeds` reads from `AnchorScript` only (with fallback to first script).
- [ ] `MainWindowViewModel` wires `Seed.SelectedScript` change → `Graph.SetAnchor`.
- [ ] All 5 tests pass.

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 9 (Open questions: multi-script workspaces).
- Pattern exemplar: existing `Seed.Bind(...)` callback wiring.
- Depends on: [01 — Focus filter](./01-desktop-graph-tab-focus-filter.md), [04 — Inspector VM](./04-desktop-graph-tab-inspector-vm.md).
