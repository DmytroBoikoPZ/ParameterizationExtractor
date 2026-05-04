# 05 — Graph tab

> Captures: M5 from the mockup pass. Status: **implemented (2026-05-03)** — focus mode is the default; click-dispatch is wired (Pending → expand; Configured/Excluded → inspector); the M6 inspector pane is the click payoff. Earlier full-subgraph rendering shipped as `desktop-graph-viz` v1; the explore experience lives in `desktop-graph-tab`.
>
> Where the user starts at the seed root, sees its immediate FK neighbours (one hop in / out), and progressively walks outward by clicking nodes. Per-node strategy / Where / Excluded edits open in the slide-in inspector ([06 — Node inspector](./06-node-inspector.md)).

## Mockup — focus mode (default)

```
┌─ Graph ─────────────────────────────────────────────────────────────────────┐
│ Focus: ◉ Seed + 1 hop  ○ Seed + 2 hops  ○ Whole reachable subgraph          │
│ [Layout: hierarchical v]   [Fit]   [Reset focus]              17/372 nodes  │
│                                                                             │
│                       ┌──────────────────┐                                  │
│              ┌─→─→─→─ │  Patient  ★ ✓    │ ←─seed (always shown)            │
│              │        │  FKDependency    │                                  │
│              │        └──┬─────┬─────┬───┘                                  │
│              │           │     │     │                                      │
│              │           ↓     ↓     ↓                                      │
│           ┌──┴──┐    ┌────────┐ ┌─────┐ ┌──────────┐                        │
│           │Visit│    │Address │ │Phone│ │PatientNote│                       │
│           │  ◯  │    │   ◯    │ │  ◯  │ │    ◯      │                       │
│           │ +5  │    │  +1    │ │ +0  │ │   +2      │                       │
│           └─────┘    └────────┘ └─────┘ └──────────┘                        │
│                                                                             │
│ Click a hollow node ◯ → expand its FK neighbours (1 more hop)               │
│ Click a configured node ✓ → opens inspector (M6) for strategy / Where edit  │
│ Right-click any node → Exclude / Reset / "Show on whole graph"              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Interaction model

The graph is a **manually-grown subgraph** of the operator's choosing, anchored on the seed root.

- The view starts with the seed plus its **immediate FK partners** (one hop in either direction). The seed has a star icon; the focus radius around it is fixed by the toolbar's "Focus" radio.
- A **hollow** node `◯` (Pending — discovered via FK but no decision yet) carries a count badge `+N` showing how many *unexplored* FK neighbours sit beyond it. Clicking expands those neighbours into view.
- A **configured** node `✓` represents a table the operator already added to `TablesToProcess`; clicking opens the M6 inspector instead of expanding (the FK partners are already part of the extraction plan and visible).
- An **excluded** node `✗` is dim, dashed-bordered; clicking opens the inspector with the Excluded toggle highlighted (so the operator sees the reason).
- **Reset focus** restores the view to "seed + 1 hop", discarding any expansions.
- The "Whole reachable subgraph" radio falls back to the v1 survey mode (mostly debug / validation use; not the primary mode).

## Implications

- Node state semantics from `desktop-graph-viz` carry over unchanged: `✓` configured / `◯` pending / `✗` excluded. What changes is the **rendering scope** — the graph now shows a subset and grows on demand, not the full reachable cone at load time.
- The engine seam (`IGraphBuilder.BuildAsync`) keeps returning the **whole** reachable subgraph in one call (it's cheap — 328 tables × 490 FKs in the test DB came back in milliseconds). The view filters client-side. No new engine method needed.
- The `+N` count badge on each pending node is computed locally: `degree(node) − (neighbours already on screen)`.
- Selecting a configured node feeds the same `SelectedNodeId` DP as v1; the inspector pane (M6) binds to it. Clicking elsewhere closes the inspector.
- "Excluded" is now a **reachable** state (the inspector lets the operator flip it). Exclusion is a per-table decision; FK edges to/from an excluded node are styled `Stop`.
- Layout choice (hierarchical / layered / force-directed) still applies. With the focused subset the layouts are useful; full survey mode often hits performance / readability ceilings on real schemas.

## Components used

- [GraphView](./components.md#graphview) — host of the focus toolbar, the canvas, the legend.
- [GraphHostView](./components.md#graphhost) — AGL wrapper (already shipped in `desktop-graph-viz`).
- [NodeInspector](./components.md#nodeinspector) (M6) — slide-in pane bound to the selected configured node.
- [GraphLegend](./components.md#graphlegend) + [GraphToolbar](./components.md#graphtoolbar) — extended with the **Focus** radio group + **Reset focus** button.

## Open questions specific to this screen

- **Star icon vs colour for seed**: pick at impl time. Seed should be unmistakable.
- **+N badge accuracy**: should `+N` count *all* FK partners or only those NOT already on screen? Leaning toward "not already on screen" — gives the operator a sense of "what's left to discover from here".
- **Multi-seed workspaces**: each script's root is a separate anchor. Show all roots simultaneously? Or focus on the seed-tab's `SelectedScript`? Leaning toward the latter — single context per view.
- **Self-referential FKs**: AGL renders self-loops poorly. Consider a small badge on the node ("recurses on itself") instead of an actual self-edge.
- **Cycle handling**: BFS visit-tracking already breaks cycles. Visualize cycle membership? *(Pass 3.)*
- **Performance ceiling**: focused mode means we render at most "seed + N hops" worth of nodes — typically <30. Survey mode is the only one that hits AGL's perf wall.

## What ships in v1 (`desktop-graph-tab`)

- Focus radio (Seed + 1 hop default; Seed + 2 hops; Whole reachable subgraph) and Reset focus button on the toolbar.
- Click-to-expand on `◯` Pending nodes; +N count badge.
- Click-to-inspect on `✓` Configured nodes → opens M6 inspector.
- Click-to-inspect on `✗` Excluded nodes → opens M6 inspector with Excluded prominently shown.
- Inspector edits flow through to `Workspace.Package.Scripts[*].TablesToProcess[*]` (add / mutate / set Excluded).

## What's deferred

- Right-click context menu (mockup hints at it).
- Edge-click toggling Follow/Stop — `desktop-graph-edge-click` per ADR-008 constraint.
- Auto-pan / "go to next pending" navigation.
- Per-workspace persisted layout / focus-radius preference.
