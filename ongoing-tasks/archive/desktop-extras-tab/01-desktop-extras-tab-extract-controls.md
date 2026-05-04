# 01 — desktop-extras-tab — Extract `WhereFilterEditor` + `StrategyPicker` + refactor `NodeInspectorView`

## Goal

Promote the inlined `WhereFilterEditor` (a single-line `SqlEditor` wrapper) and `StrategyPicker` (the four-radio chooser) from `NodeInspectorView.xaml` into proper reusable UserControls under `Controls/`. Refactor `NodeInspectorView` to use them — **no behaviour change, only XAML composition**. Closes the second-consumer note that `desktop-graph-tab` left in `components.md`.

## Track

`desktop` (WPF / C#).

## What Exists

- [`Desktop/Views/Graph/NodeInspectorView.xaml`](../../ParameterizationExtractor.Desktop/Views/Graph/NodeInspectorView.xaml) — currently inlines:
  - A 4-radio `<RadioButton GroupName="Strategy"…/>` group bound to `StrategyChoice` via `EnumMatchConverter`.
  - `<editor:SqlEditorView IsSingleLine="True" Text="{Binding Where, Mode=TwoWay}"/>`.
- [`Desktop/Views/Graph/StrategyKind.cs`](../../ParameterizationExtractor.Desktop/Views/Graph/StrategyKind.cs) — the enum the picker drives. Will move to `Controls/StrategyPicker/StrategyKind.cs` so the control owns its public contract.
- [`Desktop/Controls/SqlEditor/SqlEditorView.xaml(.cs)`](../../ParameterizationExtractor.Desktop/Controls/SqlEditor/SqlEditorView.xaml) — the underlying editor wrapped by `WhereFilterEditor`.
- [`Desktop/Converters/EnumMatchConverter.cs`](../../ParameterizationExtractor.Desktop/Converters/EnumMatchConverter.cs) — used internally by `StrategyPicker`.

## What to Build

### `Controls/WhereFilterEditor/WhereFilterEditorView.xaml(.cs)` (new)

`UserControl` with one DP: `Text` (string, two-way, `BindsTwoWayByDefault`). Lays out:

```
[Where filter] (small label, optional)
[ <SqlEditorView IsSingleLine="True" Text="{TemplateBinding Text}"/> ]
[ "e.g. Active = 1" placeholder hint when Text is empty ]
```

Code-behind only `InitializeComponent()`. The `Text` DP forwards to the inner `SqlEditorView` via XAML element binding (`{Binding Text, RelativeSource={RelativeSource AncestorType=UserControl}}`).

Optional second DP: `LabelVisible` (bool, default true) so the host can hide the label when it has its own labelling. Defer if not needed by Extras step 03.

### `Controls/StrategyPicker/StrategyPickerView.xaml(.cs)` + `StrategyKind.cs` (new)

Move `StrategyKind` enum from `Views/Graph/StrategyKind.cs` to `Controls/StrategyPicker/StrategyKind.cs` so the control owns the public contract. Update `using` in `NodeInspectorViewModel.cs`.

`StrategyPickerView` is a `UserControl` with one DP: `StrategyChoice` (StrategyKind, two-way). Lays out 4 `<RadioButton>` rows with one-line tooltips, identical to the current inline group. Internally binds each radio's `IsChecked` to the DP via the existing `EnumMatchConverter`.

Code-behind only `InitializeComponent()`.

### `NodeInspectorView.xaml` — refactor

Replace the inline strategy radio group with:
```xml
<strategyPicker:StrategyPickerView Grid.Row="2"
                                   StrategyChoice="{Binding StrategyChoice, Mode=TwoWay}"
                                   Margin="0,0,0,12"/>
```

Replace the inline single-line `SqlEditorView` with:
```xml
<whereEditor:WhereFilterEditorView Grid.Row="4"
                                   Text="{Binding Where, Mode=TwoWay}"
                                   Margin="0,0,0,12"/>
```

Add the two new XAML namespaces. **Behaviour must be byte-identical** to before — `NodeInspectorViewModelTests` continue to pass without changes.

### Tests

UI not unit-tested per recipe. The refactor is verified by:
- `NodeInspectorViewModelTests` continues to pass (9/9) — VM contract unchanged.
- `dotnet build` 0 errors — XAML compiles, namespaces resolve.

If a quick `WhereFilterEditorViewTests` for the DP forwarding is cheap, add one (smoke-only). Same for `StrategyPickerViewTests`. Otherwise skip — these are pure XAML wrappers.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test` — 9/9 `NodeInspectorViewModelTests` still green; no other suite affected.
- Grep confirms `Views/Graph/StrategyKind.cs` deleted; `Controls/StrategyPicker/StrategyKind.cs` is the only definition.
- Grep confirms `<editor:SqlEditorView IsSingleLine="True"…/>` no longer appears in `NodeInspectorView.xaml`.

## Acceptance Criteria

- [ ] `Controls/WhereFilterEditor/WhereFilterEditorView.xaml(.cs)` exists; `Text` DP two-way; code-behind only `InitializeComponent()`.
- [ ] `Controls/StrategyPicker/StrategyPickerView.xaml(.cs)` + `StrategyKind.cs` exist; `StrategyChoice` DP two-way; code-behind only `InitializeComponent()`.
- [ ] `Views/Graph/StrategyKind.cs` deleted (moved to `Controls/StrategyPicker/`).
- [ ] `NodeInspectorView.xaml` uses the new controls; no inline radio group; no inline `SqlEditorView IsSingleLine="True"`.
- [ ] `NodeInspectorViewModelTests` 9/9 continue to pass byte-identically.
- [ ] `dotnet build` 0 errors.
- [ ] No new tripwires (no logic in code-behind beyond `InitializeComponent()`; no WPF types leak into VMs; AvalonEdit confined to `SqlEditor` as before).

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 2.
- Pattern exemplars: [`Controls/SqlEditor/SqlEditorView.xaml`](../../ParameterizationExtractor.Desktop/Controls/SqlEditor/SqlEditorView.xaml) (DP-only wrapper), [`Controls/TablePicker/`](../../ParameterizationExtractor.Desktop/Controls/TablePicker/) (UserControl + DP).
- Components map entry: [`docs/design/desktop-ui/components.md#whereeditfiltereditor`](../../docs/design/desktop-ui/components.md), [`#strategypicker`](../../docs/design/desktop-ui/components.md).
- Depends on: nothing (this step is self-contained refactor).
