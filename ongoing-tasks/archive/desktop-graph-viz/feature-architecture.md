# Desktop Graph Viz — Architecture Overview

> Closes ADR-008's deferred follow-up: graph library is `AutomaticGraphLayout` 1.1.12. First feature to land `IGraphBuilder` (Logic seam), `Controls/GraphHost/` (AGL wrapper), and `Excluded` flag on `TableToExtract` (engine model + wiring).

---

## 1. Pipeline / Integration

```
Workspace loaded                                     (existing)
   ↓
MainWindowViewModel.OnWorkspaceChanged(ws)
   ↓
GraphViewModel.Hydrate(ws, plaintextPassword)        (NEW)
   ├── connStr = WorkspaceConnectionStringBuilder.Build(ws.Source, plaintext)
   ├── seedTables = ws.Package.Scripts[*].RootRecords[*] → TableRef list
   ├── (background) await IGraphBuilder.BuildAsync(connStr, seedTables, ct)
   │     ↑ engine opens SqlConnection, walks FKs from each seed,
   │       returns ReachableGraph(Nodes, Edges) — same FK source as DependencyBuilder
   │
   ├── continuation: await _ui.InvokeAsync(() => {
   │     // Map ReachableGraph → ObservableCollection<GraphNodeViewModel>
   │     // For each ReachableGraph.Node:
   │     //   - find matching TablesToProcess[*] entry by (Schema, Name)
   │     //   - State = entry == null         → Pending
   │     //            entry.Excluded == true → Excluded
   │     //            else                   → Configured
   │     //   - StrategyChip = entry?.ExtractStrategy.GetType().Name short
   │     //   - IsSeed = node ∈ seedTables
   │     // For each ReachableGraph.Edge:
   │     //   - Style = both endpoints Configured              → Follow
   │     //            either endpoint Excluded                → Stop
   │     //            else                                    → Pending
   │     Nodes = mapped; Edges = mapped; Status = $"{configuredCount}/{Nodes.Count}";
   │   })
   └── on failure: Status = "Failed: {message}"
```

| Stage | What changes | Where |
|-------|--------------|-------|
| Engine model | `Excluded` flag added to `TableToExtract` | `Logic/Model/RootRecord.cs` |
| Engine wiring | `DependencyBuilder` skips excluded tables; T4 omits emission | `Logic/MSSQL/DependencyBuilder.cs`, `Logic/Templates/DefaultTemplate.tt` |
| Engine seam | New `IGraphBuilder` + `MSSqlGraphBuilder` | `Logic/Schema/` |
| Desktop control | New `Controls/GraphHost/` wrapping `AutomaticGraphLayout.Wpf.GraphViewer` | Desktop project |
| Desktop VMs | New `GraphViewModel` (Singleton) + `GraphNodeViewModel` + `GraphEdgeViewModel` | `Desktop/Views/Graph/` |
| Wire-up | `MainWindowViewModel` 9-param ctor; Graph tab swap | `MainWindow.xaml(.cs)`, `MainWindowViewModel.cs` |

---

## 2. Component diagram

```
+------------------------------------------------------+
|  ParameterizationExtractor.Desktop                   |
|                                                      |
|  Views/Graph/                                        |
|   ├── GraphView.xaml                                 |
|   └── GraphViewModel  (Singleton)                    |
|         ├── Nodes: ObservableCollection<            |
|         │     GraphNodeViewModel>                    |
|         ├── Edges: ObservableCollection<            |
|         │     GraphEdgeViewModel>                    |
|         ├── LayoutChoice (enum)                      |
|         ├── SelectedNodeId  (string?)                |
|         ├── Status / IsLoading                       |
|         ├── RefreshCommand                           |
|         └── Hydrate(WorkspaceModel?, string?)        |
|                                                      |
|  Controls/GraphHost/                                 |
|   ├── GraphHostView (UserControl)                    |
|   │     wraps AutomaticGraphLayout.Wpf.GraphViewer   |
|   │     DPs: Graph, Layout, SelectedNodeId           |
|   └── (AGL types confined here)                      |
+------------------------------------------------------+
                       │
                       │ IGraphBuilder
                       ↓
+------------------------------------------------------+
|  ParameterizationExtractor.Logic                     |
|  Schema/                                             |
|   ├── IGraphBuilder + ReachableGraph + GraphNode     |
|   │   + GraphEdge                                    |
|   └── MSSqlGraphBuilder                              |
|        uses IObjectMetaDataProvider for FK data     |
|        BFS from seed tables; respects Excluded flag |
+------------------------------------------------------+
```

No new project references. Desktop still references `Common` + `Logic`.

---

## 3. Excluded flag — model + wiring

The mockup's `✗` state is **not cosmetic**. v1 ships the engine wiring so `Excluded == true` actually skips the table during extraction.

```csharp
public class TableToExtract
{
    // ...existing fields...

    [XmlAttribute("excluded"), DefaultValue(false)]
    [JsonPropertyName("excluded"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Excluded { get; set; }
}
```

`DependencyBuilder` reads the flag at the top of the FK walk: when the candidate table maps to a `TablesToProcess` entry with `Excluded == true`, the walk does not enqueue it and the T4 template does not emit it. New characterisation scenario `excluded-table` pins the behaviour with a recorded golden so the engine output stays stable.

**v1 has no UI to set Excluded** — it round-trips through the workspace JSON. Hand-edited workspaces or `desktop-graph-tab`'s inspector flip it to `true`.

---

## 4. Engine seam contract (`IGraphBuilder`)

```csharp
namespace Quipu.ParameterizationExtractor.Logic.Schema;

public interface IGraphBuilder
{
    Task<ReachableGraph> BuildAsync(
        string connectionString,
        IReadOnlyList<TableRef> seedTables,
        CancellationToken ct = default);
}

public sealed record ReachableGraph(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges);

public sealed record GraphNode(string Schema, string Name);

public sealed record GraphEdge(
    string FromSchema, string FromName,
    string ToSchema, string ToName,
    string ConstraintName);
```

`MSSqlGraphBuilder.BuildAsync`:
- Opens a `SqlConnection` (single-use; Microsoft.Data.SqlClient stays inside Logic per tripwire).
- Reads the FK graph via the same `IObjectMetaDataProvider` that powers `DependencyBuilder` (existing query joins `sys.foreign_keys` × `sys.foreign_key_columns` × `sys.tables` × `sys.schemas`).
- BFS from each seed table; collects every reachable `(Schema, Name)` and the edge that brought it in.
- Returns nodes sorted by `(Schema, Name)`; edges sorted by `(From, To, ConstraintName)`.
- Throws `DatabaseExplorerException` (existing type — the seam reuses it) on `SqlException` / `InvalidOperationException` / `ArgumentException`. `OperationCanceledException` propagates.

The builder is read-only on the database. No DDL.

---

## 5. AGL isolation pattern (mirrors SqlEditor)

```
+--------------------------------------------+
|  Controls/GraphHost/GraphHostView.xaml     |
|                                            |
|  <UserControl>                             |
|    <agl:GraphViewer x:Name="Viewer"/>      |
|  </UserControl>                            |
|                                            |
|  Code-behind:                              |
|  - InitializeComponent()                   |
|  - DependencyProperty: Graph (ReachableGraph?) |
|  - DependencyProperty: Layout (enum)       |
|  - DependencyProperty: SelectedNodeId      |
|  - PropertyChanged callbacks bridge        |
|    DP ↔ Viewer.Graph (AGL DrawingGraph)    |
|  - Subscribe to Viewer.MouseDown →         |
|    update SelectedNodeId DP                |
+--------------------------------------------+
```

**Tripwire:** ViewModels see only `ReachableGraph` (engine record), `GraphLayoutKind` (enum), and `SelectedNodeId` (string). They never touch `AutomaticGraphLayout.*`, `Microsoft.Msagl.*`, AGL `DrawingGraph`, AGL `GraphViewer`, or AGL `Node`/`Edge`. The only place AGL types are visible is `GraphHostView.xaml.cs` — confined per ADR-008's "AvalonEdit isolation" pattern.

Recipe section "Graph host" added in step 10 documents this.

---

## 6. Node-state matrix

| Discovery condition | `TablesToProcess` entry | `Excluded` flag | UI state |
|---|---|---|---|
| Reached via FK walk | absent | — | `Pending` (◯) |
| Reached via FK walk | present | `false` (default) | `Configured` (✓) |
| Reached via FK walk | present | `true` | `Excluded` (✗) |
| Seed root | (any) | — | `Configured` + `IsSeed = true` (rendered with seed badge) |

State computation lives in `GraphViewModel.Hydrate`. Pure mapping function — no engine call.

---

## 7. Edge-style matrix

| Endpoint A state | Endpoint B state | Edge style |
|---|---|---|
| Configured | Configured | `Follow` (solid) |
| Configured | Pending | `Pending` (thin) |
| Pending | Pending | `Pending` (thin) |
| Configured / Pending | Excluded | `Stop` (dashed grey) |

Computed during `Hydrate` after node states are known. Pure mapping — derived state, never persisted.

---

## 8. Threading & cancellation

- `RefreshCommand` is `[RelayCommand(IncludeCancelCommand = true)]` on `async Task RefreshAsync(CancellationToken ct)`.
- `MSSqlGraphBuilder.BuildAsync` calls `await conn.OpenAsync(ct)` and `await reader.ReadAsync(ct)` — every async observes the token.
- Cancellation surfaces as `OperationCanceledException` → VM catches → `Status = "Cancelled"`.
- `IUiDispatcher` is the only dispatcher access. `Hydrate`'s engine call uses `ConfigureAwait(false)`; the observable mutation goes through `_ui.InvokeAsync`.

---

## 9. State model

`GraphViewModel` (Singleton):

| Member | Type | Source |
|---|---|---|
| `Nodes` | `ObservableCollection<GraphNodeViewModel>` | computed during `Hydrate` |
| `Edges` | `ObservableCollection<GraphEdgeViewModel>` | computed during `Hydrate` |
| `LayoutChoice` | `GraphLayoutKind` | user-selectable; default `Hierarchical` |
| `SelectedNodeId` | `string?` | from `GraphHostView` selection |
| `Status` | `string` | e.g. `""` / `"Loading…"` / `"17/372"` / `"Failed: …"` / `"Cancelled"` |
| `IsLoading` | computed `bool` | from `RefreshCommand.IsRunning` |
| `Reachable` | `ReachableGraph?` | exposed for `GraphHostView.Graph` DP binding |
| `RefreshCommand` | `IAsyncRelayCommand` | `[RelayCommand(IncludeCancelCommand = true)]` |
| `Hydrate(WorkspaceModel?, string?)` | method | called from `MainWindowViewModel.OnWorkspaceChanged` |

`GraphNodeViewModel`:

| Member | Type | Notes |
|---|---|---|
| `NodeId` | `string` | `"{Schema}.{Name}"` — stable identifier for AGL |
| `Schema` | `string` | |
| `Name` | `string` | |
| `IsSeed` | `bool` | derived during Hydrate |
| `State` | `NodeState` enum (`Configured` / `Pending` / `Excluded`) | |
| `StrategyChip` | `string` | e.g. `"FKDependency"`; empty when state is Pending |

`GraphEdgeViewModel`:

| Member | Type | Notes |
|---|---|---|
| `FromNodeId` | `string` | matches a node's `NodeId` |
| `ToNodeId` | `string` | matches a node's `NodeId` |
| `Style` | `EdgeStyle` enum (`Follow` / `Stop` / `Pending`) | |
| `ConstraintName` | `string` | tooltip / debug |

---

## 10. Risks & open questions

- **AGL on net10.0-windows.** `AutomaticGraphLayout` 1.1.12 is built against older targets; relies on .NET-Standard surface for the core + WPF-specific assembly for `GraphViewer`. Verify it loads and renders in step 05 before committing further. Fallback: pin `Microsoft.Msagl` 1.1.6 (truly stable but unmaintained) — same API, drop-in.
- **Self-referential FK rendering.** Engine returns the edge; AGL renders self-loops imperfectly without manual layout hints. Accept v1 quality; flag in mockup notes.
- **Performance ceiling.** AGL hierarchical layout on 100+ nodes is multi-second. v1 doesn't filter or chunk — accept; revisit only if user reports it.
- **`SelectedNodeId` two-way binding.** v1 just exposes the property; nothing reads it. Keeping the binding shape so `desktop-graph-tab` can drop the inspector in without rewiring the host.
- **Excluded flag without UI.** v1 ships the JSON round-trip + engine respect. Operator who wants to exclude a table edits the workspace by hand (or waits for `desktop-graph-tab`). Documented limitation.
- **Engine model decision: `Excluded` lives on `TableToExtract`, not on `RecordsToExtract`.** Reasoning: exclusion is a per-table decision (don't walk into / out of this table), not a per-seed-row decision. Matches the mockup's per-node ✗.

---

## 11. Security & isolation

- **No new trust boundaries.** Builder uses the same SQL Server the operator already authenticated to. Reads `sys.foreign_keys` (read-only system view).
- **Decrypted password lifetime.** `MainWindowViewModel.OnWorkspaceChanged` decrypts once; `GraphViewModel.Hydrate` reads it (via the connection-string builder) when invoking `IGraphBuilder`. Plaintext lives only in process memory. Cleared on Close.
- **No SQL execution from user input.** The graph builder runs a fixed query (FK metadata); no operator SQL is forwarded.
- **AGL rendering surface.** AGL renders text labels. Schema / table names come from `sys.tables` — trusted source. No HTML / script injection surface.

---

## 12. What this feature does NOT build

- No slide-in node inspector (M6 → `desktop-graph-tab`).
- No edge-click toggling Follow/Stop (v2 → `desktop-graph-edge-click`).
- No right-click context menu.
- No auto-pan to "next pending" node.
- No persisted layout preference per workspace.
- No cycle visualisation beyond what AGL renders by default.
- No UI to set `Excluded` (the flag is hand-edit / future-feature settable in v1).
