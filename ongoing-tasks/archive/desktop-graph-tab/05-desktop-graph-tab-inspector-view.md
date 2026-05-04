# 05 — desktop-graph-tab — `NodeInspectorView` (M6 slide-in Flyout)

## Goal

Build the slide-in inspector pane (mockup [M6](../../docs/design/desktop-ui/06-node-inspector.md)). MahApps `<Flyout>` from the right; bound to `GraphViewModel.InspectedNode`. Hosts the strategy radio, the Where editor, the Excluded checkbox, and the Add-to-extract / Recompute buttons.

## Track

`desktop` (WPF / C#).

## What Exists

- `NodeInspectorViewModel` (step 04).
- `GraphViewModel.InspectedNode` + `CloseInspectorCommand` (step 03).
- [`Controls/SqlEditor/SqlEditorView.xaml`](../../ParameterizationExtractor.Desktop/Controls/SqlEditor/SqlEditorView.xaml) — reused for the Where editor (`IsSingleLine="True"`).
- MahApps `<Flyout>` already used by other dialogs; XML namespace already in `MainWindow.xaml`.

## What to Build

### `Desktop/Views/Graph/NodeInspectorView.xaml(.cs)`

`UserControl` (or directly `Flyout`-friendly content). Code-behind only `InitializeComponent()`.

Sections (per M6 mockup):

1. **Header** — node label `{Schema}.{Name}` with state badge (✓/◯/✗) and seed indicator if applicable. Close button (✕) → `CloseInspectorCommand` from the parent `GraphViewModel`.

2. **Strategy** — `RadioButton` group bound to `StrategyChoice` via the `EnumMatchConverter` from step 02:
   ```
   ◉ FKDependency   — walks both parents & children
   ○ OnlyChildren   — walks down only
   ○ OnlyParent     — walks up only
   ○ OnlyOneTable   — extract just this table; don't follow FKs
   ```
   Each radio has a one-line tooltip explaining what it does.

3. **Where filter** — `<editor:SqlEditorView IsSingleLine="True" Text="{Binding Where, Mode=TwoWay}"/>` with a small label and example placeholder.

4. **Excluded** — `<CheckBox IsChecked="{Binding Excluded}" Content="Exclude from extraction"/>` with a one-line caption: "When checked, this table is skipped during the FK walk and emits no SQL."

5. **Add to extract** — large button visible only when `IsAddable == true`, bound to `AddToExtractCommand`. Shown for Pending nodes.

6. **Recompute graph** — secondary button bound to `RecomputeGraphCommand`. Always visible; tooltip: "Re-walk the FK graph against the updated workspace."

### `MainWindow.xaml` — Flyout host

The Graph tab's `<TabItem>` content already shows `<graph:GraphView/>`. Nest a `<mah:FlyoutsControl>` either at the `<MetroWindow>` root or scoped to the Graph tab. Pragmatic: scoped to the Graph tab via `<Grid>` overlay.

Inside `GraphView.xaml`:

```xml
<Grid>
    <!-- existing graph content -->
    <mah:Flyout Header="Inspector"
                Position="Right"
                Width="380"
                Theme="Adapt"
                IsOpen="{Binding InspectedNode, Converter={StaticResource NotNullToBool}}"
                CloseCommand="{Binding CloseInspectorCommand}">
        <graph:NodeInspectorView DataContext="{Binding InspectorVm}"/>
    </mah:Flyout>
</Grid>
```

Where `InspectorVm` is a property on `GraphViewModel` that materialises the `NodeInspectorViewModel` when `InspectedNode` is non-null. Implementation: when `OnInspectedNodeChanged` fires, instantiate a fresh `NodeInspectorViewModel`, call `Open(InspectedNode, _workspace, currentScript)`, and assign to `InspectorVm`. Closes when `InspectedNode == null`.

### `Converters/NotNullToBoolConverter.cs` (new — if not already present)

Maps `value != null` → true. Used for `IsOpen` binding.

### Manual smoke

- Run desktop, open `examples/sample.bws`.
- Switch to Graph tab → see seed (Patient) + 1 hop with `+N` badges on Pending neighbours.
- Click a Pending node (e.g. `Visit`) → expands; new neighbours appear with their own `+N` badges. Inspector does not open.
- Click a Configured node (e.g. `Patient` itself) → inspector slides in from the right.
- Change Strategy from `FKDependency` to `OnlyChildren` → workspace persists; node visual chip updates.
- Set Where = `Id < 100` → persists.
- Toggle Excluded → node turns ✗ in graph; edges to it dash. Untoggle → returns to ✓.
- Click an Excluded node → inspector opens with Excluded prominently shown.
- Pick a Pending node, click Add to extract → entry created; inspector morphs into edit mode; graph recomputes (may bring new partners into reach if FKs lead anywhere new).
- Click Recompute graph → re-runs `IGraphBuilder.BuildAsync`.
- Close + reopen workspace → all edits persisted.

Log outcome in checklist Notes.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-04 N stays at N (UI not unit-tested).
- Manual smoke per above.

## Acceptance Criteria

- [ ] `NodeInspectorView.xaml(.cs)` exists; code-behind only `InitializeComponent()`.
- [ ] Flyout slides from the right; bound to `InspectedNode != null`.
- [ ] Strategy radio + Where editor + Excluded checkbox functional and persistent.
- [ ] Add-to-extract button works for Pending nodes; morphs to edit mode on click.
- [ ] Recompute graph button re-runs the engine.
- [ ] No business logic in code-behind; `IUiDispatcher`, no direct Dispatcher.
- [ ] Manual smoke walked end-to-end. Logged in checklist Notes.
- [ ] `dotnet build` / `dotnet test` green.

## References

- Mockup: [M6 — Node inspector](../../docs/design/desktop-ui/06-node-inspector.md).
- Pattern exemplars: `Views/Seed/SeedView.xaml` (DataTemplate hosting), `Controls/SqlEditor/` (SqlEditor reuse), MahApps Flyout samples.
- Depends on: [04 — Inspector VM](./04-desktop-graph-tab-inspector-vm.md).
