# 06 — Node inspector

> Captures: M6 from the mockup pass. Status: **implemented (2026-05-03)** — slide-in pane covers Strategy / Where / Excluded / Add-to-extract / Recompute graph; right-click context menu in the host handles Exclude / Include / Reset / Show on whole graph. `BuildDirectivesEditor` (INSERT/UPDATE checkboxes + identity-insert) and `FKEdgeList` (per-edge Follow/Stop) deferred — engine model already supports them but inspector v1 doesn't expose them.
>
> Slides in from the right when a node is clicked on [05 — Graph](./05-graph.md). Where most of the user's clicking lives — strategy, Where filter, per-edge Follow/Stop, build directives.

## Mockup

```
┌─ Patient ──────────────────────────────[ Apply ][ Apply & Next ][ X ]─┐
│ dbo.Patient · 372 cols · 23 FKs in graph                              │
│ Status: (•) In path  ( ) Excluded                                     │
│                                                                       │
│ ── Strategy ─────────────────────────────────────────────────────────  │
│ (•) FKDependency   walk parents and children                          │
│ ( ) OnlyChildren   walk only children of this row                     │
│ ( ) OnlyParent     walk only parents of this row                      │
│ ( ) OnlyOneTable   extract only this row, no traversal                │
│                                                                       │
│ ── Where filter (applied to every row visited at this table) ─────── │
│ ┌───────────────────────────────────────────────────────────────────┐ │
│ │ Active = 1 AND DeletedAt IS NULL                                  │ │
│ └───────────────────────────────────────────────────────────────────┘ │
│                                                                       │
│ ── FK edges ─────────────────────────────────────────────────────────  │
│ Parents (this → other):                                               │
│   → Country         [ Follow v ]  child gets: OnlyOneTable            │
│   → Insurance       [ Stop   v ]                                      │
│                                                                       │
│ Children (other → this):                                              │
│   ← Visit           [ Follow v ]  child gets: OnlyChildren            │
│   ← Phone           [ Follow v ]  child gets: OnlyParent              │
│   ← Note            [ Stop   v ]                                      │
│                                                                       │
│ ── Build directives ─────────────────────────────────────────────────  │
│ [✓] Emit INSERT   [ ] Emit UPDATE                                     │
│ Identity insert: ( ) auto  (•) on  ( ) off                            │
└───────────────────────────────────────────────────────────────────────┘
```

## Implications

- The inspector is **slide-in**, not modal — the graph stays visible behind it.
- **The inspector is the sole edit surface for Follow/Stop in v1** (locked L3). Edges on the graph display state but are not clickable. v2 will add edge-click as an additional input surface; v1 ships without it.
- "Apply & Next" jumps to the next pending node so the user can sweep through linearly. This is the primary keyboard-friendly walkthrough — and the reason inspector-only is acceptable UX for v1: the high-frequency case is sweeping, not flipping individual edges.
- The **per-FK Follow/Stop** dropdown is how the user "clicks like this is a path how to extract" from the original concept — each edge is an explicit yes/no.
- Selecting `Follow` on an edge that was previously Stopped causes the destination node to appear on the graph (or transition from `✗` to `◯`).
- Selecting `Stop` on an edge prunes that branch; downstream nodes that are now unreachable transition to `✗` with a "no longer reachable" hint.
- The inspector is **bound to the selected node**; closing it does not clear selection on the graph. Re-opening picks up where it left off. Unapplied changes show a dirty hint on the inspector chrome.

## Components used

- [NodeInspector](./components.md#nodeinspector) — the slide-in pane chrome.
- [StrategyPicker](./components.md#strategypicker) — the four-radio strategy chooser. Reused on [07 — Extras](./07-extras.md).
- [WhereFilterEditor](./components.md#wherefiltereditor) — single-line SQL editor with light validation. Reused on [07 — Extras](./07-extras.md). Wraps [SqlEditor](./components.md#sqleditor) in single-line mode.
- [FKEdgeList](./components.md#fkedgelist) — Parents / Children grouped list with Follow/Stop dropdowns.
- [BuildDirectivesEditor](./components.md#builddirectiveseditor) — INSERT/UPDATE checkboxes + identity-insert tri-state.

## Open questions specific to this screen

- **What does "child gets:" override do?** Currently the dropdown next to each FK edge shows the strategy that *the destination node* will have if Follow is chosen. Should that be locked to the destination's own strategy (read-only), or overridable per-edge here?
- **Where filter validation.** Parse to ensure it's a valid `WHERE` clause? Or trust the user and let the engine fail?
- **Identity-insert tri-state.** "Auto" means "decide at SQL gen time based on whether the table has an identity column". Should this default to `auto` or `on`?
- **Apply vs Apply & Next vs auto-apply on field change.** Today an explicit Apply is required. Auto-apply on every field change is also viable (simpler MVVM). Pick.
- **Bulk operations.** Select multiple nodes on the graph → set strategy on all at once? Useful for "set all lookup tables to OnlyOneTable in one go".
