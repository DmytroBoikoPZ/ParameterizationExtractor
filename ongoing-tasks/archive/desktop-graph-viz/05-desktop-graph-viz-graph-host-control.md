# 05 — desktop-graph-viz — Desktop `Controls/GraphHost/` (AGL wrapper)

## Goal

Land `Controls/GraphHost/GraphHostView.xaml(.cs)` — a thin WPF UserControl wrapping `AutomaticGraphLayout.Wpf.GraphViewer` that exposes three dependency properties (`Graph`, `Layout`, `SelectedNodeId`) and hides every AGL type from ViewModels. Mirrors the AvalonEdit isolation pattern of `SqlEditor`. **No VM yet** — step 06. Step 05 produces a registered, manually-instantiable control that renders any `ReachableGraph` you hand it.

## Track

`desktop` (WPF / C#).

## What Exists

- AGL NuGet packages installed in step 01.
- [`Controls/SqlEditor/SqlEditorView.xaml(.cs)`](../../ParameterizationExtractor.Desktop/Controls/SqlEditor/SqlEditorView.xaml.cs) — pattern exemplar for "third-party control type confined inside one UserControl with three DPs".
- [`Logic/Schema/IGraphBuilder.cs`](../../ParameterizationExtractor.Logic/Schema/IGraphBuilder.cs) — provides the `ReachableGraph` / `GraphNode` / `GraphEdge` records this control consumes.
- [`adr/008-desktop-ui-controls.md`](../../adr/008-desktop-ui-controls.md) + [`adr/012-graph-visualisation-library.md`](../../adr/012-graph-visualisation-library.md) — locked tripwire: AGL types confined here.

## What to Build

### `Controls/GraphHost/GraphLayoutKind.cs`

```csharp
namespace Quipu.ParameterizationExtractor.Desktop.Controls.GraphHost;

internal enum GraphLayoutKind
{
    Hierarchical,   // default — sugiyama / layered top-down
    Layered,        // explicit layer constraints (parent above child)
    ForceDirected   // physics-based; useful for dense graphs
}
```

### `Controls/GraphHost/GraphHostView.xaml`

- `UserControl`, `x:ClassModifier="internal"`, namespace `Quipu.ParameterizationExtractor.Desktop.Controls.GraphHost`.
- XAML: `<agl:GraphViewer x:Name="Viewer"/>` with `xmlns:agl="..."` (verify the actual XAML namespace from the AGL WPF assembly).
- If AGL's `GraphViewer` is not directly XAML-friendly, host it inside a `<Border>` and instantiate in code-behind on `Loaded`.

### `Controls/GraphHost/GraphHostView.xaml.cs`

- Three DPs:
  - `Graph` (`ReachableGraph?`, default `null`, `OnGraphChanged` callback).
  - `Layout` (`GraphLayoutKind`, default `Hierarchical`, `OnLayoutChanged` callback).
  - `SelectedNodeId` (`string?`, default `null`, two-way, `OnSelectedNodeIdChanged` callback).
- Internal state: an AGL `DrawingGraph` rebuilt on every `Graph` DP change.
- `OnGraphChanged`: clear viewer; if new graph non-null, build `DrawingGraph`:
  - For each node in `ReachableGraph.Nodes`, add an AGL `Node` with `Id = $"{Schema}.{Name}"` and `LabelText = $"{Schema}.{Name}"`.
  - For each edge in `ReachableGraph.Edges`, add an AGL `Edge` with `From = "{FromSchema}.{FromName}"`, `To = "{ToSchema}.{ToName}"`.
  - Apply current `Layout` choice (map enum → AGL layout settings: `SugiyamaLayoutSettings`, `MdsLayoutSettings`, etc. — verify exact AGL types).
  - Assign to `Viewer.Graph`.
- `OnLayoutChanged`: rebuild `DrawingGraph` with new layout settings (or apply directly to existing graph if AGL supports in-place layout swap).
- Subscribe `Viewer.MouseDown` (or AGL's specific node-pick event — verify): when an AGL node is picked, set `SelectedNodeId = node.Id` (the `"{Schema}.{Name}"` we assigned).
- `OnSelectedNodeIdChanged`: VM-driven selection (e.g. "scroll to node"); v1 may no-op. Wire the path so `desktop-graph-tab` doesn't have to.
- Code-behind contains AGL types only here — `using AutomaticGraphLayout.*` lines stay confined to this `.xaml.cs`.

### Recipe tripwire

Recipe section "Graph host" added in step 10 documents:

> **Tripwire — AGL isolation.** `AutomaticGraphLayout.*` and `Microsoft.Msagl.*` types live only inside `Controls/GraphHost/`. ViewModels never see `DrawingGraph`, `GraphViewer`, or AGL `Node` / `Edge` types. The `Viewer.MouseDown` ↔ `SelectedNodeId` bridge is the only place AGL types are visible — confined to `GraphHostView.xaml.cs`.

(This step does not edit the recipe — that's step 10. Step 05 establishes the convention; step 10 documents it.)

### Tests

Per `prompts/execution.md` — UI: implement → visual verify → component test for interaction logic. **WPF UI is not unit-tested per recipe.** Step 05 produces a buildable control; rendering smoke happens in step 09.

DI smoke: not applicable — `GraphHostView` is a UserControl instantiated from XAML, not a DI-resolved type.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-04 N stays at N (no new tests).
- AGL types grep: only `Controls/GraphHost/` files mention `AutomaticGraphLayout` or `Msagl`. No leakage.

## Acceptance Criteria

- [ ] `Controls/GraphHost/GraphHostView.xaml(.cs)` and `GraphLayoutKind.cs` exist; class modifier `internal`.
- [ ] Three DPs: `Graph` (`ReachableGraph?`), `Layout` (`GraphLayoutKind`), `SelectedNodeId` (`string?`).
- [ ] AGL types appear ONLY inside `GraphHostView.xaml.cs` (grep verifies).
- [ ] No business logic in code-behind beyond the DP bridges + AGL wiring (mirrors `SqlEditor`).
- [ ] `dotnet build` 0 errors; `dotnet test` green.
- [ ] **Manual smoke is deferred to step 09** — step 05 just guarantees the control compiles.

## References

- ADR: [`adr/008-desktop-ui-controls.md`](../../adr/008-desktop-ui-controls.md), [`adr/012-graph-visualisation-library.md`](../../adr/012-graph-visualisation-library.md).
- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § Code editor (AvalonEdit) — same pattern.
- Pattern exemplar: `Controls/SqlEditor/SqlEditorView.xaml(.cs)` — three DPs + bridge code-behind, third-party type confined.
- AGL docs: `https://github.com/microsoft/automatic-graph-layout` (Microsoft) / `https://github.com/pvila/automatic-graph-layout` (community fork — verify final source).
- Depends on: [01 — AGL NuGet](./01-desktop-graph-viz-adr-and-nuget.md), [04 — IGraphBuilder model](./04-desktop-graph-viz-graph-builder.md).
