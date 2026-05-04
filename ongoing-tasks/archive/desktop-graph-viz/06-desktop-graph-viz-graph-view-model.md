# 06 — desktop-graph-viz — `GraphViewModel` (Singleton, hydrate + state computation)

## Goal

Land `GraphViewModel` — the top-level VM for the Graph tab. Hydrates from the workspace + connection string, invokes `IGraphBuilder.BuildAsync`, and translates the engine's `ReachableGraph` into per-node / per-edge state for the view (`✓` / `◯` / `✗`; Follow / Stop / Pending). Mirrors `SeedViewModel`'s shape: Singleton, parent-handler `Hydrate`, `RefreshCommand`, dispatcher-marshalled engine call. **Does NOT build the per-node VMs yet** — that's step 07.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`Logic/Schema/IGraphBuilder.cs`](../../ParameterizationExtractor.Logic/Schema/IGraphBuilder.cs) (step 04).
- [`Desktop/Services/Workspace/WorkspaceConnectionStringBuilder.cs`](../../ParameterizationExtractor.Desktop/Services/Workspace/WorkspaceConnectionStringBuilder.cs) — connection-string assembly shared with seed-tab.
- [`Desktop/Views/Seed/SeedViewModel.cs`](../../ParameterizationExtractor.Desktop/Views/Seed/SeedViewModel.cs) — pattern exemplar for "Singleton VM, `Bind(Func<Task>)` save handler, `Hydrate(Workspace, plaintext)` entry point, dispatcher-marshalled async call".
- `Tests/Desktop/Fakes/{FakeUiDispatcher.cs, FakeDialogService.cs}` — pattern exemplars.
- `Tests/Desktop/SeedViewModelTests.cs` — pattern exemplar for "harness with fakes + multiple Hydrate scenarios".

## What to Build

### `Tests/Desktop/Fakes/FakeGraphBuilder.cs`

Queue-driven fake mirroring `FakeDatabaseExplorer`:

- `EnqueueResult(ReachableGraph)`, `EnqueueException(Exception)`.
- Records `ConnectionStringsCalled` and `SeedsCalled` (`List<IReadOnlyList<TableRef>>`).
- Lives in `Tests/Desktop/Fakes/`.

### `Desktop/Views/Graph/GraphViewModel.cs`

`internal sealed partial class GraphViewModel : ObservableObject`. Constructor:

```csharp
public GraphViewModel(
    IGraphBuilder builder,
    IUiDispatcher ui,
    IDialogService dialog,
    ILoggerFactory loggerFactory)
```

(Identical shape to `SeedViewModel`'s ctor — same DI footprint so `MainWindowViewModel`'s injection list grows by one.)

**Members:**

- `[ObservableProperty] private GraphLayoutKind _layoutChoice = GraphLayoutKind.Hierarchical;`
- `[ObservableProperty] private string? _selectedNodeId;`
- `[ObservableProperty] private ReachableGraph? _reachable;` — exposed for `GraphHostView.Graph` DP binding.
- `[ObservableProperty] private string _status = string.Empty;`
- `public ObservableCollection<GraphNodeViewModel> Nodes { get; } = new();` — populated in step 07; created here as an empty hook.
- `public ObservableCollection<GraphEdgeViewModel> Edges { get; } = new();` — same.
- `public bool IsLoading => RefreshCommand.IsRunning;`
- Internal: `WorkspaceModel? _workspace;`, `string? _connectionString;`.

**Public methods:**

- `public void Hydrate(WorkspaceModel? workspace, string? plaintextPassword)`:
  1. Cancel any running refresh.
  2. `Nodes.Clear(); Edges.Clear(); Reachable = null; SelectedNodeId = null;`
  3. If `workspace == null`: `_workspace = null; _connectionString = null; Status = string.Empty;` and return.
  4. `_workspace = workspace; _connectionString = WorkspaceConnectionStringBuilder.Build(workspace.Source, plaintextPassword);`
  5. Fire-and-forget `_ = RefreshCommand.ExecuteAsync(null);`.

**Commands:**

`[RelayCommand(IncludeCancelCommand = true)] private async Task RefreshAsync(CancellationToken ct)`:

1. Bail if `_workspace == null` or `_connectionString` empty: `Status = ""; return;`
2. Collect seed tables: `_workspace.Package.Scripts[*].RootRecords[*]` → distinct `TableRef(Schema ?? "", TableName)` list.
3. If seed list empty: `Status = "No seed roots configured"; Nodes.Clear(); Edges.Clear(); Reachable = null; return;`
4. `Status = "Loading…";`
5. `try { var graph = await _builder.BuildAsync(_connectionString, seeds, ct).ConfigureAwait(false); await _ui.InvokeAsync(() => MapAndApply(graph, seeds)); }`
6. `catch (OperationCanceledException) { await _ui.InvokeAsync(() => Status = "Cancelled"); }`
7. `catch (DatabaseExplorerException ex) { await _ui.InvokeAsync(() => Status = $"Failed: {ex.Message}"); }`

**`MapAndApply(ReachableGraph graph, IReadOnlyList<TableRef> seeds)`** (step 07 fills in the body that creates `GraphNodeViewModel` / `GraphEdgeViewModel` instances; step 06 stubs it):

For step 06's tests, `MapAndApply` does at minimum:
- `Reachable = graph;`
- `Status = $"{graph.Nodes.Count} nodes / {graph.Edges.Count} edges";` (step 07 refines this to "configured/total").

This lets step 06 land + tests pass without coupling to step 07's per-node VMs.

### DI registration

[`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs): `services.AddSingleton<GraphViewModel>();`

### Tests

Per `prompts/execution.md` — CODE: TDD cycle.

New file `Tests/Desktop/GraphViewModelTests.cs`:

Setup: `Harness` (mirrors `SeedViewModelTests`'s) building the VM with `FakeGraphBuilder`, `FakeUiDispatcher`, `FakeDialogService`, `NullLoggerFactory.Instance`.

1. `Default_State_NoNodes_StatusEmpty`.
2. `Hydrate_NullWorkspace_ClearsEverything`.
3. `Hydrate_WorkspaceWithNoScripts_StatusSaysNoSeed_NoBuilderCall` — empty `Package.Scripts` → builder NOT called; `Status` mentions "No seed".
4. `Hydrate_WorkspaceWithSeed_BuildAsyncInvokedWithSeedList` — workspace with one script + one root; assert `FakeGraphBuilder.SeedsCalled[0]` matches the root's `(Schema, TableName)`.
5. `Hydrate_BuilderReturnsGraph_ReachablePopulated_StatusFormatted`.
6. `Hydrate_BuilderThrowsDatabaseExplorerException_StatusFailedDoesNotThrow`.
7. `Hydrate_BuilderThrowsOperationCanceled_StatusCancelled`.
8. `Hydrate_TwiceWithDifferentWorkspaces_RebuildsAndReplaces` — first workspace produces graph A; second produces graph B; assert `Reachable` matches B.
9. `LayoutChoice_DefaultIsHierarchical`.
10. `RefreshCommand_NoWorkspaceLoaded_NoBuilderCall` — explicit `RefreshCommand.ExecuteAsync` without prior `Hydrate`; builder not called.
11. `Hydrate_DeduplicatesSeedTables` — workspace with two scripts pointing at the same root; assert `SeedsCalled[0]` has one distinct entry.
12. `DesktopHost_ResolvesGraphViewModel` (in `HostCompositionTests.cs`) — DI smoke; Singleton check.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-05 N + 12, all green.

## Acceptance Criteria

- [ ] `Desktop/Views/Graph/GraphViewModel.cs` exists; `internal sealed partial`; no `using System.Windows*`.
- [ ] Constructor takes `(IGraphBuilder, IUiDispatcher, IDialogService, ILoggerFactory)`.
- [ ] `Hydrate(WorkspaceModel?, string?)` clears state on null; on non-null builds connection string + fires `RefreshCommand`.
- [ ] `RefreshAsync` collects + dedupes seed tables; bails when empty; uses `ConfigureAwait(false)` + `_ui.InvokeAsync` for observable mutation.
- [ ] Three exception paths covered: `OperationCanceledException` → "Cancelled"; `DatabaseExplorerException` → `"Failed: …"`; success → status with counts.
- [ ] DI Singleton in `DesktopHost.cs`.
- [ ] All 12 tests pass.
- [ ] No tripwires introduced.

## References

- Pattern exemplars: `SeedViewModel` + `SeedViewModelTests` (desktop-seed-tab steps 05).
- Methodology: `docs/methodology/wpf-desktop.md` § Service abstractions, § Threading.
- Depends on: [04 — IGraphBuilder](./04-desktop-graph-viz-graph-builder.md).
