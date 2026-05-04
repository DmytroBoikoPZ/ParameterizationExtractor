# 012 — Graph visualisation library — `AutomaticGraphLayout` (Msagl fork)

## Status

Accepted

## Context

[ADR-008](./008-desktop-ui-controls.md) locked the desktop chrome (MahApps.Metro) and the code editor (AvalonEdit) but deliberately deferred the graph visualisation library to "the `desktop-graph-viz` feature". The constraint locked at that time: the chosen library **must support edge-pick events** so the planned v2 upgrade (clicking a graph edge to toggle Follow / Stop in place — see [`docs/design/desktop-ui/05-graph.md`](../docs/design/desktop-ui/05-graph.md) and the deferred `desktop-graph-edge-click` roadmap entry) does not require rewriting the rendering surface.

The `desktop-graph-viz` feature ships v1 of the Graph tab: read-only FK-subgraph view with per-node state badges (`✓` configured / `◯` pending / `✗` excluded) and per-edge styling (Follow / Stop / Pending). v1 needs nodes + edges + automatic layout (hierarchical default; layered + force-directed selectable). v1 does **not** need edge-click interaction, but it must not pick a library that closes the door to it.

Like ADR-007 and ADR-008, AI-driven implementation is the constraint that shapes the pick: prefer a mature library with a NuGet-first install, .NET-only dependencies, and enough training-data weight that generated code lands close to idiomatic.

## Decision

We will use **`AutomaticGraphLayout` 1.1.12** as the graph visualisation library for the desktop's Graph tab.

Concretely, three NuGet packages added to `ParameterizationExtractor.Desktop.csproj`:

- `AutomaticGraphLayout` 1.1.12 — core graph + layout types (`Microsoft.Msagl.*` namespace).
- `AutomaticGraphLayout.Drawing` 1.1.12 — `Microsoft.Msagl.Drawing.*` (presentation-layer graph types: `Graph`, `Node`, `Edge` with colours / labels / shapes).
- `AutomaticGraphLayout.WpfGraphControl` 1.1.12 — `Microsoft.Msagl.WpfGraphControl.GraphViewer` (the WPF host control that renders a `DrawingGraph`).

Owner: NuGet user `pvila` (community-maintained fork of the original Microsoft Research package). All three publish under `Microsoft.Msagl.*` namespaces so the API is identical to the upstream `Microsoft.Msagl` packages.

**Edge-pick events are satisfied** — the WPF `GraphViewer` exposes `MouseDown` and per-element pick events that surface the clicked `IViewerObject` (which can be cast to `IViewerNode` or `IViewerEdge`). The v2 wiring is additive.

Tripwire (mirrors the AvalonEdit isolation in ADR-008): **`AutomaticGraphLayout.*` and `Microsoft.Msagl.*` types live only inside `Controls/GraphHost/`.** ViewModels see only the engine's `ReachableGraph` record, the `GraphLayoutKind` enum, and `SelectedNodeId` (string). The `Viewer.MouseDown` ↔ `SelectedNodeId` bridge is the only place AGL types are visible — confined to `GraphHostView.xaml.cs`. Recipe section "Graph host" in `docs/methodology/wpf-desktop.md` documents the convention.

Alternatives considered and rejected:

- **`Microsoft.Msagl` 1.1.6** (official Microsoft Research) — same API surface; last release 2017. Skipped because `AutomaticGraphLayout` is API-compatible AND more recently maintained while the upstream package is dormant. If the community fork ever stalls, swapping back is a `<PackageReference>` change with no source edits — the namespaces are identical (`Microsoft.Msagl.*`).
- **`GraphX` (`Stride.GraphX.PCL.*`)** — generic graph-viz library with multiple layout backends. Skipped because the `Stride`-namespaced fork suggests instability; the original `GraphX.PCL.*` packages are unmaintained; PCL targeting is also a poor fit for `net10.0-windows`.
- **`Northwoods.GoJS`** — feature-rich JS graph library hosted via WebView2. Skipped because pulling in the JS runtime + interop seam is too heavy for one tab, and licensing is commercial.
- **Hand-rolled Canvas with a custom hierarchical layout (Sugiyama / GraphViz-like)** — full control; layout engine is multi-week work; rejected on YAGNI grounds.
- **`OxyPlot.Wpf` / `LiveCharts2`** — chart libraries, not graph-viz. Mentioned for completeness; not applicable.
- **`Domemtech.AutomaticGraphLayout` / `Extellect.AutomaticGraphLayout`** — additional community forks of Msagl with much smaller download counts than `pvila`'s `AutomaticGraphLayout`. Skipped because the chosen fork has the deepest community footprint and recent activity; the others are reserve fallbacks if `pvila` ever stalls.

## Consequences

- **Easier:**
  - Edge-pick events satisfied — v2 wiring is additive, not a rebuild.
  - Hierarchical / layered / force-directed layouts available out-of-the-box (`SugiyamaLayoutSettings`, `LayeredLayout`, `MdsLayoutSettings`).
  - NuGet-first install; no native binaries beyond what Msagl ships.
  - `Microsoft.Msagl.*` namespaces match the upstream package exactly — fallback to `Microsoft.Msagl` 1.1.6 is a `<PackageReference>` swap if the community fork ever stalls.
  - The recipe-level "AGL types confined to `Controls/GraphHost/`" rule mirrors the existing AvalonEdit pattern; no new `CLAUDE.md` tripwire needed.

- **Harder:**
  - AGL's API is C#-idiomatic but documented sparingly; future contributors may need to read the source. Mitigated by keeping the API surface small (build a `DrawingGraph`, attach to `GraphViewer`, listen for one mouse event).
  - AGL's WPF `GraphViewer` is a heavy control; perceived rendering on graphs above ~100 nodes degrades. Acceptable for v1; revisit only if user reports it.
  - Self-referential FKs render imperfectly without manual layout hints. Documented as a v1 limitation in the feature's architecture.

- **Open follow-ups:**
  - **`desktop-graph-tab`** (next feature) — slide-in node inspector pane (M6); edits per-node strategy / Where / Follow-Stop. Uses the `SelectedNodeId` two-way DP wired in this feature.
  - **`desktop-graph-edge-click`** (v2) — clicking a graph edge directly toggles Follow / Stop. The library's edge-pick event support (locked here) is the precondition.
  - **Performance / filtering** — only revisit if the operator hits the ceiling.
