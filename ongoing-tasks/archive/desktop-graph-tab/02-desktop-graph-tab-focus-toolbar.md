# 02 — desktop-graph-tab — Focus toolbar + GraphHostView wiring to render only visible subset

## Goal

Land the toolbar Focus radio + Reset focus button in `GraphView.xaml`, and switch `GraphHostView` to render `VisibleNodes` / `VisibleEdges` instead of `Reachable`. After this step, the operator sees seed + 1 hop on workspace open; click-dispatch + inspector come in steps 03–05.

## Track

`desktop` (WPF / C#).

## What Exists

- `GraphViewModel.FocusModeChoice`, `VisibleNodes`, `VisibleEdges`, `ResetFocusCommand` (step 01).
- [`Desktop/Views/Graph/GraphView.xaml`](../../ParameterizationExtractor.Desktop/Views/Graph/GraphView.xaml) — v1 toolbar (layout dropdown / Refresh / Cancel / status pill).
- [`Desktop/Controls/GraphHost/GraphHostView.xaml.cs`](../../ParameterizationExtractor.Desktop/Controls/GraphHost/GraphHostView.xaml.cs) — currently consumes `Graph` (`ReachableGraph`) DP. Extend or swap to a new pair of DPs.

## What to Build

### `GraphView.xaml` — Focus radio group + Reset button

Add to the toolbar (above or beside the existing layout dropdown):

```xml
<TextBlock Text="Focus:" Margin="12,0,4,0" VerticalAlignment="Center" Opacity="0.7"/>
<RadioButton Content="Seed + 1 hop" GroupName="FocusMode"
             IsChecked="{Binding FocusModeChoice, Converter={StaticResource EnumMatchConverter}, ConverterParameter=SeedPlusOneHop}"
             Margin="0,0,8,0"/>
<RadioButton Content="Seed + 2 hops" GroupName="FocusMode"
             IsChecked="{Binding FocusModeChoice, Converter={StaticResource EnumMatchConverter}, ConverterParameter=SeedPlusTwoHops}"
             Margin="0,0,8,0"/>
<RadioButton Content="Whole subgraph" GroupName="FocusMode"
             IsChecked="{Binding FocusModeChoice, Converter={StaticResource EnumMatchConverter}, ConverterParameter=WholeSubgraph}"
             Margin="0,0,12,0"/>
<Button Content="Reset focus" Command="{Binding ResetFocusCommand}" Padding="10,4" Margin="0,0,12,0"/>
```

### `Converters/EnumMatchConverter.cs` (new)

`IValueConverter` mapping `(enumValue, parameterAsString)` → `bool` for radio binding. Two-way: setting `IsChecked=true` writes back the enum value parsed from the parameter.

### `GraphHostView` — switch to visible-subset DPs

**Decision:** rather than passing the full `ReachableGraph`, the host should consume **the visible node + edge collections** directly. This avoids the host re-deriving visibility.

Add two new DPs:

```csharp
public static readonly DependencyProperty VisibleNodesProperty = DependencyProperty.Register(
    nameof(VisibleNodes),
    typeof(IEnumerable<GraphNodeViewModel>),
    typeof(GraphHostView),
    new FrameworkPropertyMetadata(null, OnVisibleSubsetChanged));

public static readonly DependencyProperty VisibleEdgesProperty = DependencyProperty.Register(
    nameof(VisibleEdges),
    typeof(IEnumerable<GraphEdgeViewModel>),
    typeof(GraphHostView),
    new FrameworkPropertyMetadata(null, OnVisibleSubsetChanged));
```

Drop the existing `Graph` DP (or keep it for back-compat callers that don't use focus filtering — sample.bws-driven scenarios may still want the full graph). **Pragmatic v1:** keep `Graph` as a fallback path; if either `VisibleNodes` or `VisibleEdges` is non-null, use them; otherwise fall back to building from `Graph`. Document the precedence in the control's XML doc comment.

`OnVisibleSubsetChanged` rebuilds the AGL `DrawingGraph` from the bound collections. The `INotifyCollectionChanged` events on `ObservableCollection<...>` should also trigger a redraw — subscribe lazily (in OnVisibleSubsetChanged with a small handler that calls `Render`).

Update `Render` to consume `IEnumerable<GraphNodeViewModel>` / `IEnumerable<GraphEdgeViewModel>` instead of `ReachableGraph.Nodes` / `Edges`. The mapping is identical otherwise.

### `GraphView.xaml` — bind the host

```xml
<graphHost:GraphHostView Grid.Column="0"
                         VisibleNodes="{Binding VisibleNodes}"
                         VisibleEdges="{Binding VisibleEdges}"
                         Layout="{Binding LayoutChoice, Mode=TwoWay}"
                         SelectedNodeId="{Binding SelectedNodeId, Mode=TwoWay}"/>
```

(Existing `Graph="{Binding Reachable}"` stays as a back-compat fallback OR is removed; pick one at impl time.)

### Tests

UI not unit-tested per recipe. The toolbar wiring is verified by the smoke step (08). The DPs are wiring-only.

If a unit test for `EnumMatchConverter` is cheap, add one (`Tests/Desktop/Converters/EnumMatchConverterTests.cs`).

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-01 N stays at N (or +small for converter tests).
- The `GraphView` opens; toolbar shows three radios + Reset focus; default is SeedPlusOneHop.

## Acceptance Criteria

- [ ] Toolbar has Focus radio group (3 options) + Reset focus button.
- [ ] `EnumMatchConverter` exists + registered in `App.xaml`.
- [ ] `GraphHostView` consumes `VisibleNodes` / `VisibleEdges` (or has the precedence-fallback wired).
- [ ] On workspace open, the rendered graph is the seed + 1 hop subset (not the full subgraph).
- [ ] Layout / Refresh / Cancel / status pill from v1 unchanged.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Mockup: [`docs/design/desktop-ui/05-graph.md`](../../docs/design/desktop-ui/05-graph.md) (revised).
- Pattern exemplar: existing `GraphHostView` v1 DPs.
- Depends on: [01 — Focus filter](./01-desktop-graph-tab-focus-filter.md).
