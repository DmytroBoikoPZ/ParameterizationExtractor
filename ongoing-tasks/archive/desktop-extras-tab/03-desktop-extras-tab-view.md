# 03 — desktop-extras-tab — `ExtrasView.xaml` + Add-table dialog flow + MainWindow wire-up

## Goal

Build the M7 standalone-tables UI: a list of rows with inline expand-to-edit, "+ Add table" button opening a `TablePicker` dialog, and per-row Remove. Wire `ExtrasViewModel` into `MainWindow` and the `Seed.SelectedScript` anchor.

## Track

`desktop` (WPF / C#).

## What Exists

- `ExtrasViewModel` + `StandaloneTableViewModel` (step 02), registered in DI.
- [`Desktop/Controls/TablePicker/`](../../ParameterizationExtractor.Desktop/Controls/TablePicker/) — combobox of `TableRef`s (step `desktop-seed-tab`).
- [`Controls/StrategyPicker/`](../../ParameterizationExtractor.Desktop/Controls/StrategyPicker/) + [`Controls/WhereFilterEditor/`](../../ParameterizationExtractor.Desktop/Controls/WhereFilterEditor/) (step 01).
- [`MainWindow.xaml`](../../ParameterizationExtractor.Desktop/MainWindow.xaml) — Extras tab currently has placeholder text.
- [`MainWindowViewModel`](../../ParameterizationExtractor.Desktop/MainWindowViewModel.cs) — has the `Seed.PropertyChanged` subscription routing `SelectedScript` → `Graph.SetAnchor`. Extend to also call `Extras.SetAnchor`.

## What to Build

### `Desktop/Views/Extras/ExtrasView.xaml(.cs)` (new)

`UserControl`, `x:ClassModifier="internal"`. Code-behind only `InitializeComponent()`.

Layout:
```xml
<Grid Margin="12">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>           <!-- Section header -->
        <RowDefinition Height="*"/>              <!-- Standalone tables list -->
        <RowDefinition Height="Auto"/>           <!-- + Add button + empty hint -->
    </Grid.RowDefinitions>

    <TextBlock Grid.Row="0" Text="Standalone tables (extracted outside the FK graph)"
               FontWeight="SemiBold" Margin="0,0,0,8"/>

    <ItemsControl Grid.Row="1" ItemsSource="{Binding StandaloneTables}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border BorderBrush="#22000000" BorderThickness="0,0,0,1" Padding="0,6">
                    <StackPanel>
                        <!-- Row summary: clickable to expand -->
                        <DockPanel LastChildFill="True"
                                   Cursor="Hand"
                                   MouseLeftButtonUp="OnRowClick"  <!-- toggles IsExpanded via attached behaviour OR via VM command, see below -->
                                   Background="Transparent">
                            <TextBlock DockPanel.Dock="Left" Text="{Binding Display}" FontWeight="Bold" Margin="0,0,12,0"/>
                            <TextBlock DockPanel.Dock="Left" Text="{Binding StrategyChip}" Opacity="0.7" Margin="0,0,12,0"/>
                            <TextBlock DockPanel.Dock="Left" Text="{Binding Where}" Opacity="0.55" FontStyle="Italic"/>
                            <Button DockPanel.Dock="Right" Content="✕"
                                    Command="{Binding RemoveCommand}"
                                    Padding="6,0" Margin="8,0,0,0"
                                    Background="Transparent" BorderThickness="0"/>
                            <Button DockPanel.Dock="Right" Content="Edit"
                                    Command="{Binding ToggleExpandCommand}"
                                    Padding="6,0"
                                    Background="Transparent" BorderThickness="0"/>
                        </DockPanel>

                        <!-- Inline expander shown when IsExpanded -->
                        <StackPanel Margin="16,8,0,0"
                                    Visibility="{Binding IsExpanded, Converter={StaticResource BoolToVis}}">
                            <strategy:StrategyPickerView StrategyChoice="{Binding StrategyChoice, Mode=TwoWay}"
                                                         Margin="0,0,0,8"/>
                            <whereEditor:WhereFilterEditorView Text="{Binding Where, Mode=TwoWay}"
                                                               Margin="0,0,0,8"/>
                            <CheckBox IsChecked="{Binding Excluded, Mode=TwoWay}"
                                      Content="Exclude from extraction"/>
                        </StackPanel>
                    </StackPanel>
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>

    <DockPanel Grid.Row="2" LastChildFill="True" Margin="0,12,0,0">
        <Button DockPanel.Dock="Left"
                Content="+ Add table…"
                Command="{Binding AddStandaloneTableCommand}"
                Padding="12,6"/>
        <TextBlock Text="{Binding EmptyHint}"
                   Margin="12,0,0,0"
                   FontStyle="Italic" Opacity="0.55"
                   VerticalAlignment="Center"
                   Visibility="{Binding EmptyHint, Converter={StaticResource StringNonEmptyToVis}}"/>
    </DockPanel>
</Grid>
```

**Code-behind decision:** the `MouseLeftButtonUp="OnRowClick"` handler is forbidden by the WPF tripwire (no logic in `.xaml.cs`). Two alternatives:

- **(a)** Use `<i:Interaction.Triggers>` from `Microsoft.Xaml.Behaviors.Wpf` (already a transitive dep via MahApps) to invoke `ToggleExpandCommand` on `MouseLeftButtonUp`. **Preferred** — declarative, no code-behind.
- **(b)** Skip the row-click-to-expand and rely solely on the explicit `[Edit]` button. Simpler. **Acceptable v1.**

Pick (b) for v1 to keep the XAML minimal; the inline `[Edit]` button is operator-discoverable. Drop the `MouseLeftButtonUp` line above; `OnRowClick` is never written.

### `Desktop/Services/Dialogs/DialogService.cs` — `ShowAddStandaloneTableDialogAsync` impl

The WPF impl uses MahApps' content dialog (or a minimal `Window`-based dialog if simpler) hosting a `TablePickerView` with OK / Cancel buttons. Returns the selected `TableRef?` or `null` on Cancel.

Pragmatic v1 approach: a `MetroDialog`-style dialog with body:

```xml
<StackPanel>
    <TextBlock Text="Pick a table to add as standalone" Margin="0,0,0,8"/>
    <picker:TablePickerView Tables="{Binding Tables}"
                            SelectedTable="{Binding Selected, Mode=TwoWay}"/>
</StackPanel>
```

The dialog's content's DataContext is a tiny `AddStandaloneTableDialogViewModel` exposing `Tables` (passed in) + `Selected`. `OK` returns `Selected`; `Cancel` returns null. Use the existing `IDialogService` pattern (`ShowEditConnectionDialogAsync` is the closest exemplar — `ContentDialog` style with VM-backed body).

### `MainWindowViewModel` — wire-up

Constructor signature gains `ExtrasViewModel extras`:

```csharp
public MainWindowViewModel(
    IWorkspaceStore store,
    OverviewViewModel overview,
    WelcomeViewModel welcome,
    SeedViewModel seed,
    GraphViewModel graph,
    ExtrasViewModel extras,        // NEW
    IDialogService dialog,
    IRecentFilesStore recents,
    IPasswordProtector protector,
    ILogger<MainWindowViewModel> log)
```

Body changes:
```csharp
public ExtrasViewModel Extras { get; } = extras;

// ctor body:
Extras.Bind(SaveWorkspaceAsync);
Seed.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(SeedViewModel.SelectedScript))
    {
        var anchor = Seed.SelectedScript?.Source;
        Graph.SetAnchor(anchor);
        Extras.SetAnchor(anchor);     // NEW
    }
};

// OnWorkspaceChanged:
Extras.Hydrate(value, plaintextForSeed);
```

### `MainWindow.xaml` — swap the Extras tab content

Replace the Extras `<TabItem>`'s placeholder TextBlock with:
```xml
<extras:ExtrasView DataContext="{Binding Extras}"/>
```

Add the XAML namespace `xmlns:extras="clr-namespace:Quipu.ParameterizationExtractor.Desktop.Views.Extras"`.

### Tests

UI not unit-tested per recipe. Behaviour is covered by step 02's VM tests + the existing `MainWindowViewModelGraphHydrationTests` (extend with one new test):

- `OnWorkspaceChanged_HydratesExtras_AnchorMatchesSeedSelectedScript` — open workspace; assert `Extras.AnchorScript == seed.Scripts[0].Source`.

`HostCompositionTests` already covered `ExtrasViewModel` resolution in step 02; nothing new here.

### Manual smoke (operator-driven, deferred)

Walk through with `examples/sample.bws`:
1. Open the sample → switch to Extras tab.
2. Initially: `LookupCountry` should appear in the standalone list (it's in `TablesToProcess` with `OnlyOneTable` and **not** FK-reachable from `Patient`).
3. Click `[Edit]` on the row → expander opens with strategy radios + Where editor. Change Where to `IsActive = 1`. Close + reopen workspace → persisted.
4. Click `[+ Add table…]` → dialog shows table list. Pick a non-FK table → row appears with `OnlyOneTable` strategy, empty Where, not Excluded.
5. Click `[✕]` on the new row → row vanishes; close + reopen → not in workspace.
6. Toggle `Excluded` checkbox on a row → engine model `TableToExtract.Excluded = true`. The Graph tab won't see this table (it's not in graph anyway), so no cross-tab visual change — but the engine T4 will skip it.
7. If a multi-script workspace exists: change Seed.SelectedScript → Extras list re-loads from the new anchor.

Log outcome with date in checklist Notes.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-02 N + 1 (or unchanged if test deferred to step 04).
- Manual smoke walked end-to-end (operator).

## Acceptance Criteria

- [ ] `Desktop/Views/Extras/ExtrasView.xaml(.cs)` exists; code-behind only `InitializeComponent()`.
- [ ] Per-row inline expander with `StrategyPickerView` + `WhereFilterEditorView` + Excluded checkbox; toggled by `[Edit]` button via `ToggleExpandCommand`.
- [ ] `[+ Add table…]` button bound to `AddStandaloneTableCommand`; dialog flow returns a `TableRef?` and the new row appears.
- [ ] Per-row `[✕]` Remove button bound to `RemoveCommand`.
- [ ] Empty hint visible only when `EmptyHint` non-empty.
- [ ] `MainWindowViewModel` ctor takes `ExtrasViewModel`; `Extras.Bind/Hydrate/SetAnchor` wired into the existing fan-out and Seed → anchor route.
- [ ] `MainWindow.xaml` Extras tab hosts `<extras:ExtrasView DataContext="{Binding Extras}"/>`.
- [ ] `IDialogService.ShowAddStandaloneTableDialogAsync` has a WPF impl (`DialogService`).
- [ ] Manual smoke walked end-to-end. Logged in checklist Notes.
- [ ] `dotnet build` 0 errors; existing tests green.

## References

- Feature architecture: [`feature-architecture.md`](./feature-architecture.md) § 2, § 3.
- Pattern exemplars: `Views/Seed/SeedView.xaml` (per-row DataTemplate hosting), `Views/Graph/NodeInspectorView.xaml` (StrategyPicker + WhereFilterEditor host), `Services/Dialogs/DialogService.ShowEditConnectionDialogAsync` (existing `ContentDialog`-style flow).
- Depends on: [01 — Extract controls](./01-desktop-extras-tab-extract-controls.md), [02 — VM](./02-desktop-extras-tab-view-model.md).
