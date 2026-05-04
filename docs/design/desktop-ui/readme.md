# Desktop UI — design mockups (index)

> **Status:** decisions locked through Pass 2 of refinement (2026-05-02). The "Decisions already locked" block below is now binding policy for the upcoming UI features. Per-screen mockups are still drafts and will refine during their owning features.
>
> Source conversations: 2026-05-02 design session + Pass 2 question batch.

## User concept (verbatim)

> "1) user enters connection string 2) tool loads all the metadata 3) user plays with data, pickups root record (writes query) then tool shows all the graph then user going one be one over nodes and clicks like this is a path how to extract on each node user can define configuration of the strategy. 4) the tool tracks all this, in kind of workspace, which can be edited and adding additional scripts, and tables. 5) so workspace to design the package kind of."

## Decisions already locked

### Engine boundary

- Engine accepts **both** XML and JSON workspace formats indefinitely. JSON is canonical for new workspaces; XML stays readable so existing `ExtractConfig.xml` files keep working. No flag day. *(L10.)*
- Graph = FK **schema** graph, not data graph. Other tools cover data browsing.

### Stack (captured in ADR-007 / ADR-008 — see [adr/readme.md](../../../adr/readme.md))

- WPF on `net10.0-windows`.
- New `ParameterizationExtractor.Desktop` project; refs `Logic` + `Common` only. Module layering tripwire holds.
- MVVM = CommunityToolkit.Mvvm 8.x.
- Composition = Microsoft.Extensions.Hosting (Generic Host), symmetric with the CLI.
- UI chrome = MahApps.Metro 2.4.x.
- Code editor surface = AvalonEdit (wrapped in a `SqlEditor` UserControl).
- No docking library — `Grid` + `GridSplitter` initially.
- Graph viz library = TBD (deferred to `desktop-graph-viz` feature). **Constraint:** chosen library must support edge-level pick / click events so v2 can add direct-edge interaction (see L3 below). Msagl is the leading candidate.
- Testing = NUnit + FluentAssertions; ViewModel-level only, no automated UI tests.

### Shell layout *(L1, L2)*

- **Tabs**, not a single canvas with side panels. Tab strip: `Overview / Seed / Graph / Extras / Run`.
- **No left explorer tree** in v1. The graph + tab content already convey the workspace structure. Reconsider only if usage shows the gap.

### Document model *(L7, L9)*

- **Single-document.** Opening a workspace replaces the current one (with a dirty-prompt if needed).
- **Explicit Save.** Title-bar `*` marker for dirty state. No auto-save.

### File format *(L8)*

- Workspace files use the **`.bws`** extension. Recent-files list, file associations, and `OpenFileDialog` filters all key off this.

### Connection & credentials *(L5, L6)*

- Connection lives **inside** the workspace JSON. No separate connection store.
- **Password at rest:** DPAPI-encrypt the password field on save, with an explicit "Store credentials" checkbox the user can untick (in which case the password is empty in the file and prompted at open). Never plaintext-by-default. Decision lands as an ADR inside the `desktop-connection-management` feature.

### Graph interaction — Follow / Stop per FK edge *(L3)*

- **v1: Inspector-only.** Follow/Stop is set in the [node inspector](./06-node-inspector.md), one row per FK. Graph is read-only display.
- **Visual state on graph:** edges still render with full state — solid (Follow), dashed-grey (Stop), thin (Pending). The user can *see* the state at a glance even though they can't click edges.
- **v2 upgrade path (deferred, not a current feature):** add edge-click on the graph that toggles Follow/Stop in place. This is why the graph viz library must support edge-pick events.

### Run / Save / Execute *(L4)*

- One Run tab with three radio modes: Dry-run / Generate `.sql` to disk / Execute against target. Same results pane regardless of mode.

## Screens (one mockup per file)

1. [01 — Startup / empty state](./01-startup.md)
2. [02 — New workspace dialog](./02-new-workspace.md)
3. [03 — Main shell](./03-shell.md)
4. [04 — Seed tab](./04-seed.md)
5. [05 — Graph tab](./05-graph.md)
6. [06 — Node inspector](./06-node-inspector.md)
7. [07 — Extras tab](./07-extras.md)
8. [08 — Run tab](./08-run.md)

## Reusable components

[components.md](./components.md) — placeholder inventory of UI bits that appear on more than one screen.

## Remaining open questions (Pass 3 — late-binding, deferred to feature implementation)

These do not gate the next feature. They live here for the relevant feature step file to pull in when work begins.

- M1: drag-and-drop on empty state · recent list size · recent list location.
- M2: database list discovery method · default save-path.
- M4: multi-row seed UX explicit-mention · preview row cap configurability · long-query cancel.
- M5: graph self-loops · cycle visualization · performance ceiling for huge graphs · auto-pan-to-next-pending.
- M6: child-strategy override per FK edge · where-filter validation · identity-insert default · auto-apply vs explicit Apply · multi-select bulk operations.
- M7: script naming auto-numbered · external script files · skip-on-dry-run flag · script variables.
- M8: cancel running operation · output path memory · schema diff before execute · streaming progress · execute confirm dialogs.

## Pass 1 inconsistency fixes applied

- Scripts content lives **inside the Extras tab** (M7). Tab strip in M3 mockup updated to remove a separate Scripts tab.
- M2 mockup hides User/Password fields when Windows auth is selected.
- M3 status bar simplified — file dirty/save state only; source schema details live on the Overview tab.
- M5 layout dropdown options enumerated: hierarchical / layered / force-directed.
