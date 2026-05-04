# 08 — desktop-graph-viz — `GraphView` XAML + toolbar + legend + DataTemplates

## Goal

Land `Views/Graph/GraphView.xaml(.cs)` — the Graph-tab view that hosts `<graphHost:GraphHostView/>` and the toolbar (layout dropdown / Fit / +/- zoom / status pill) and the dock-right legend (matches mockup [M5](../../docs/design/desktop-ui/05-graph.md)). Wires `GraphViewModel.Reachable` → `GraphHostView.Graph` and `GraphViewModel.LayoutChoice` → `GraphHostView.Layout`. **No `MainWindowViewModel` changes yet** — step 09. Step 08 produces a self-contained view that renders against any bound `GraphViewModel`.

## Track

`desktop` (WPF / C#).

## What Exists

- `GraphHostView` (step 05) — the host control, three DPs.
- `GraphViewModel` + `GraphNodeViewModel` + `GraphEdgeViewModel` (steps 06–07).
- [`Views/Seed/SeedView.xaml`](../../ParameterizationExtractor.Desktop/Views/Seed/SeedView.xaml) — pattern exemplar for "Grid-with-toolbar + content area + DataTemplate".
- [`docs/design/desktop-ui/05-graph.md`](../../docs/design/desktop-ui/05-graph.md) — toolbar ASCII, legend ASCII.

## What to Build

### `Views/Graph/GraphView.xaml`

`UserControl`, `x:ClassModifier="internal"`. Layout (matches M5):

- Top row (Auto): toolbar — `<ComboBox>` for `LayoutChoice` (binds enum), `[ Fit ]`, `[ + ]`, `[ - ]` buttons (commands on `GraphViewModel`; defer impl with `IsEnabled="False"` if AGL Fit/zoom commands are non-trivial — minimum viable: layout dropdown only). Status pill on the right: `<TextBlock Text="{Binding Status}"/>`.
- Middle row (`*`): content split — graph host (left, `*` width) + legend (right, fixed width).
  - `<graphHost:GraphHostView Graph="{Binding Reachable}" Layout="{Binding LayoutChoice, Mode=TwoWay}" SelectedNodeId="{Binding SelectedNodeId, Mode=TwoWay}"/>`
  - Legend: vertical stack of `[swatch] label` rows — "── traversed FK", "-→ direction", "◯ pending decision", "✓ configured", "✗ excluded" (matches mockup).
- Empty state when `Reachable == null`: `<TextBlock>` centred, "No graph yet. Add a seed root in the Seed tab." Visibility bound to `Reachable` via a null-to-visibility converter (add `NullToVisibilityConverter` if it doesn't exist, or reuse a different signal).
- Loading overlay: ProgressRing (MahApps) bound to `IsLoading`.

### `Views/Graph/GraphView.xaml.cs`

`InitializeComponent()` only. No business logic in code-behind (recipe tripwire).

### Optional toolbar commands

If feasible without bloating step 08:
- `FitCommand`, `ZoomInCommand`, `ZoomOutCommand` on `GraphViewModel` — delegate to AGL via the `GraphHostView` exposing methods OR via additional DPs (`Zoom: double` two-way; `FitRequested: bool` one-shot trigger).

If non-trivial (the AGL API may not expose Fit cleanly), ship buttons as `IsEnabled="False"` placeholders and defer to a future feature. Document in checklist Notes.

### Legend converter / styling

Add small enum-to-something converters as needed:
- `NodeStateToBrushConverter` — `Configured` → green-ish, `Pending` → grey, `Excluded` → red-ish.
- `EdgeStyleToStrokeDashConverter` — `Follow` → solid, `Stop` → dashed, `Pending` → thin.

These converters drive the legend's swatch visuals AND (later, in step 09 manual smoke) inform the AGL `Node`/`Edge` styling inside `GraphHostView`. **Decision:** styling AGL nodes/edges from VM-side colours requires AGL API knowledge — confirm during step 05 / 08 implementation. v1 acceptable shape: AGL renders default; the LEGEND uses the converters for swatches; the actual graph coloring may be a step-09 follow-up if AGL styling is awkward. Document in Notes.

### Tests

Per `prompts/execution.md` — UI: implement → visual verify. **WPF UI is not unit-tested.** Step 08 produces a buildable view; rendering smoke happens in step 09.

If layout commands (`Fit`, `ZoomIn`, `ZoomOut`) are added, add VM-level command tests (`CanExecute` toggles based on `Reachable != null`). Otherwise no new tests.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-07 N stays at N (no new tests unless commands land — optional).

## Acceptance Criteria

- [ ] `Views/Graph/GraphView.xaml(.cs)` exists; `internal`; code-behind only `InitializeComponent()`.
- [ ] Toolbar: layout dropdown, Fit / +/- zoom buttons (live or placeholders), status pill bound to `Status`.
- [ ] Content area: `GraphHostView` bound to `Reachable` / `LayoutChoice` / `SelectedNodeId`; legend dock-right with five rows matching mockup.
- [ ] Empty state when `Reachable == null`.
- [ ] Loading overlay bound to `IsLoading`.
- [ ] Toolbar / legend / loading layout matches mockup [M5](../../docs/design/desktop-ui/05-graph.md) within reasonable WPF idiom (no requirement to match ASCII art pixel-for-pixel).
- [ ] No business logic in code-behind.
- [ ] No `MessageBox.Show` / `IConfiguration[..]` / `Console.WriteLine` / `Dispatcher.Invoke` / `TODO` / `FIXME`.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Mockup: [`docs/design/desktop-ui/05-graph.md`](../../docs/design/desktop-ui/05-graph.md).
- Pattern exemplars: `Views/Seed/SeedView.xaml` (toolbar + content + DataTemplate); `Controls/RowsPreviewGrid/RowsPreviewGridView.xaml` (control wrapping a third-party type via DPs).
- Depends on: [05 — GraphHost](./05-desktop-graph-viz-graph-host-control.md), [07 — Node/Edge VMs](./07-desktop-graph-viz-node-and-edge-vms.md).
