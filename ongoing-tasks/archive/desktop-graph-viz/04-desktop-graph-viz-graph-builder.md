# 04 — desktop-graph-viz — Engine seam: `IGraphBuilder` + `MSSqlGraphBuilder` + reachable-graph model

## Goal

Land the engine-side seam the Desktop's Graph tab will call: walk FKs from a seed set and return the reachable subgraph as nodes + edges. Mirrors `IDatabaseExplorer` from `desktop-seed-tab` — engine owns `SqlConnection`; Desktop hands a connection string + seed list and gets back a record-shaped graph. **No UI consumer yet** — step 06. Step 04 produces a registered, tested seam.

## Track

`engine` (.NET / C#).

## What Exists

- [`Logic/Schema/IDatabaseExplorer.cs`](../../ParameterizationExtractor.Logic/Schema/IDatabaseExplorer.cs) — pattern exemplar: interface + records + `DatabaseExplorerException`. The graph builder reuses `DatabaseExplorerException` for failure wrapping (single seam → single exception type for desktop callers).
- [`Logic/MSSQL/ObjectMetaDataProvider.cs`](../../ParameterizationExtractor.Logic/MSSQL/ObjectMetaDataProvider.cs) — already loads FK metadata via the joined `sys.foreign_keys × sys.foreign_key_columns × sys.tables × sys.schemas` query. Graph builder reuses this — no new SQL.
- [`Tests/EngineDatabaseExplorerTests/`](../../Tests/EngineDatabaseExplorerTests/) — pattern exemplar for engine integration tests against the live test DB.
- The `cross-schema` test fixture (audit.SchemaTestParent + dbo.SchemaTestChild) — useful for asserting the builder respects `(Schema, Name)` tuples.

## What to Build

### `Logic/Schema/IGraphBuilder.cs`

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

Visibility: all `public` (engine surface). Reuses existing `TableRef` from `IDatabaseExplorer.cs` (same namespace) — caller hands seed tables in the same shape the table picker produces.

### `Logic/Schema/MSSqlGraphBuilder.cs`

- `public sealed class MSSqlGraphBuilder : IGraphBuilder`. Constructor: `ILogger<MSSqlGraphBuilder>` + `IObjectMetaDataProvider` (DI; same provider DependencyBuilder uses).
- `BuildAsync(connectionString, seedTables, ct)`:
  - 10s connect timeout via `SqlConnectionStringBuilder`.
  - Open `SqlConnection` once, observing `ct`.
  - Load all FKs via `IObjectMetaDataProvider.GetFKsAsync(conn, ct)` (or the existing equivalent — name to verify; the goal is the same data the dependency walker uses).
  - BFS from each seed table. Visited set keyed on `(Schema, Name)` tuple (case-insensitive ordinal).
  - For each visited node: collect every FK whose endpoint matches the visited node; enqueue the other end. Both directions (children + parents) — graph is undirected for reachability, but each edge is recorded with its actual `From → To` direction (parent → child by FK convention).
  - Return `ReachableGraph` with `Nodes` ordered by `(Schema, Name)` (ordinal-ignore-case) and `Edges` ordered by `(FromSchema, FromName, ToSchema, ToName, ConstraintName)`.
- Failure modes: `SqlException` / `InvalidOperationException` / `ArgumentException` → throw `DatabaseExplorerException("Failed to build graph: " + ex.Message, ex)`. `OperationCanceledException` propagates.
- `KeyNotFoundException` / `FormatException` from `SqlConnectionStringBuilder` → wrap in `DatabaseExplorerException` (mirrors `MSSqlDatabaseExplorer`).

### DI registration

- **Desktop** ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs)): `services.AddSingleton<IGraphBuilder, MSSqlGraphBuilder>();`
- **CLI** ([`AppBootstrap.cs`](../../ParameterizationExtractor/AppBootstrap.cs) `AddMSSQL`): same — symmetric per the established pattern.

### Tests

Per `prompts/execution.md` — CODE: TDD cycle. Integration tests against the live test DB (mirror `MSSqlDatabaseExplorerTests`).

New file `Tests/EngineGraphBuilderTests/MSSqlGraphBuilderTests.cs`:

1. `BuildAsync_SingleSeed_ReturnsSeedNode` — pass `[("dbo","Patient")]`; assert seed appears in `Nodes`.
2. `BuildAsync_SeedReachesChildrenViaFK` — pass a seed with known children (e.g. `dbo.TherapyPrograms`); assert at least one child appears in `Nodes` AND the corresponding `Edge` is in `Edges`.
3. `BuildAsync_RespectsSchemaTuple` — pass `[("dbo","SchemaTestChild")]` (cross-schema fixture); assert `("audit","SchemaTestParent")` appears reachable.
4. `BuildAsync_NoFKsForSeed_ReturnsOnlySeed` — pass an island-table seed; assert `Nodes.Count == 1` and `Edges` is empty.
5. `BuildAsync_MultipleSeeds_UnionsReachable` — pass two unrelated seeds; assert both appear; edges appropriate to each.
6. `BuildAsync_OrderingIsStable` — call twice; assert `Nodes` and `Edges` are byte-identical across calls (ordering guarantee).
7. `BuildAsync_BadConnection_ThrowsDatabaseExplorerException` — bogus server.
8. `BuildAsync_Cancelled_PropagatesOperationCancelled` — pre-cancelled token.
9. `BuildAsync_EmptySeedList_ReturnsEmptyGraph` — `seedTables.Count == 0` → `Nodes` and `Edges` both empty (don't throw).
10. `BuildAsync_DuplicateSeeds_DeduplicatedInOutput` — pass two copies of the same seed; assert single node.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-03 N + 10 = N + 10, all green.
- `Microsoft.Data.SqlClient` types confined to `Logic/` (grep verifies).

## Acceptance Criteria

- [ ] `Logic/Schema/{IGraphBuilder.cs, MSSqlGraphBuilder.cs}` exist; `public` surface.
- [ ] `MSSqlGraphBuilder` reuses `IObjectMetaDataProvider` for FK data — no new SQL query.
- [ ] BFS from seed; both directions traversed; visited set keyed on `(Schema, Name)`.
- [ ] Output ordering deterministic (sorted nodes + edges).
- [ ] `DatabaseExplorerException` reused for failure wrapping (single exception type for desktop callers).
- [ ] DI registration in Desktop + CLI.
- [ ] All 10 new tests pass against the live test DB.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Pattern exemplars: `IDatabaseExplorer` + `MSSqlDatabaseExplorer` (desktop-seed-tab step 01); `DependencyBuilder` (FK-walk reference); `ObjectMetaDataProvider` (FK source).
- Depends on: [01 — ADR-012 + AGL NuGet](./01-desktop-graph-viz-adr-and-nuget.md) (sequencing only — no library use yet).
