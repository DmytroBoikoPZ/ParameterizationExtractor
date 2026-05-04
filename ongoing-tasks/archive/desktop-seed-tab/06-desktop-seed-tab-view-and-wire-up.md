# 06 — desktop-seed-tab — SeedView + MainWindow wire-up + sample workspace + manual smoke

## Goal

Replace the Seed-tab placeholder with `<seed:SeedView/>`. Extend `MainWindowViewModel` to inject `SeedViewModel`, hand it the save callback, and call `Hydrate` from `OnWorkspaceChanged`. Update `examples/sample.bws` so the tab has something to render on first open. Manual smoke proves the end-to-end UX. **No new VMs** — purely view + composition.

## Track

`desktop` (WPF / C#) + `tests` + `examples`.

## What Exists

- `ScriptEditorViewModel` (step 04), `SeedViewModel` (step 05).
- `Controls/{SqlEditor, TablePicker, RowsPreviewGrid}/` (step 03).
- `MainWindowViewModel.cs` — current 7-param ctor.
- `MainWindow.xaml` — Seed tab is a placeholder `<TextBlock>`.
- `examples/sample.bws` — currently has minimal `Package.Scripts` (likely empty or with one script). Verify before editing.

## What to Build

### `Views/Seed/SeedView.xaml(.cs)`

`UserControl`, `x:ClassModifier="internal"`. Code-behind: `InitializeComponent()` only.

Layout (matches mockup [M4](../../docs/design/desktop-ui/04-seed.md), extended for multi-script):

```xml
<UserControl ...
             xmlns:editor="clr-namespace:...Controls.SqlEditor"
             xmlns:picker="clr-namespace:...Controls.TablePicker"
             xmlns:grid="clr-namespace:...Controls.RowsPreviewGrid">
  <Grid Margin="12">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="220"/>
      <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- Left rail: Scripts list -->
    <DockPanel Grid.Column="0" LastChildFill="True" Margin="0,0,8,0">
      <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,0,0,8">
        <Button Content="+ Add" Command="{Binding AddScriptCommand}" Padding="10,4" Margin="0,0,4,0"/>
        <Button Content="Remove" Command="{Binding RemoveSelectedScriptCommand}" Padding="10,4"/>
      </StackPanel>
      <ListBox ItemsSource="{Binding Scripts}" SelectedItem="{Binding SelectedScript}">
        <ListBox.ItemTemplate>
          <DataTemplate>
            <!-- Inline-rename via a TextBox bound two-way (UpdateSourceTrigger=LostFocus -->
            <TextBox Text="{Binding ScriptName, UpdateSourceTrigger=LostFocus}"
                     BorderThickness="0"
                     Background="Transparent"/>
          </DataTemplate>
        </ListBox.ItemTemplate>
      </ListBox>
    </DockPanel>

    <!-- Right pane: selected script editor -->
    <ContentControl Grid.Column="1" Content="{Binding SelectedScript}">
      <ContentControl.Resources>
        <DataTemplate DataType="{x:Type vm:ScriptEditorViewModel}">
          <Grid>
            <Grid.RowDefinitions>
              <RowDefinition Height="Auto"/>  <!-- Root picker -->
              <RowDefinition Height="200"/>   <!-- SQL editor -->
              <RowDefinition Height="Auto"/>  <!-- Buttons + status -->
              <RowDefinition Height="*"/>     <!-- Preview grid -->
            </Grid.RowDefinitions>

            <DockPanel Grid.Row="0" LastChildFill="True" Margin="0,0,0,8">
              <TextBlock DockPanel.Dock="Left" Text="Root:" Margin="0,0,8,0" VerticalAlignment="Center" Opacity="0.7"/>
              <picker:TablePickerView .../>  <!-- bound via SelectedTable ↔ RootRef -->
            </DockPanel>

            <editor:SqlEditorView Grid.Row="1" Text="{Binding SeedQuery, Mode=TwoWay}"/>

            <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,8">
              <Button Content="Run preview" Command="{Binding RunPreviewCommand}"
                      Visibility="{Binding IsRunningPreview, Converter={StaticResource InverseBoolToVis}}"
                      Padding="14,4" Margin="0,0,8,0"/>
              <Button Content="Cancel" Command="{Binding RunPreviewCancelCommand}"
                      Visibility="{Binding IsRunningPreview, Converter={StaticResource BoolToVis}}"
                      Padding="14,4" Margin="0,0,8,0"/>
              <TextBlock Text="{Binding PreviewStatus}" VerticalAlignment="Center"/>
            </StackPanel>

            <grid:RowsPreviewGridView Grid.Row="3" Result="{Binding PreviewResult}"/>
          </Grid>
        </DataTemplate>
      </ContentControl.Resources>
    </ContentControl>

    <!-- Empty state when no scripts -->
    <TextBlock Grid.Column="1"
               Text="No scripts yet. Click + Add to create one."
               HorizontalAlignment="Center" VerticalAlignment="Center"
               Opacity="0.5" FontStyle="Italic"
               Visibility="{Binding HasScripts, Converter={StaticResource InverseBoolToVis}}"/>
  </Grid>
</UserControl>
```

The TablePicker's `Tables` collection is populated by its own `RefreshCommand` — wire it to fire on `SelectedScript` change OR on first hydrate. Decision: refresh once per workspace load (cheap) — `SeedViewModel` could expose `Tables` itself instead of nesting a `TablePickerViewModel`. **Simpler**: keep `TablePickerView` as a self-contained DataTemplate inside the script editor; it sources its `IDatabaseExplorer` and the parent's connection string via a binding from the parent `SeedViewModel` (binding to `DataContext.ConnectionString` from the `AncestorType=UserControl`). Implementer's call during step 06 — pick the cleaner WPF binding shape.

For the data template: `xmlns:vm="clr-namespace:...Views.Seed"` to reference `ScriptEditorViewModel` as the DataType.

### `MainWindowViewModel.cs` — 8th param + Hydrate wiring

- Constructor signature gains `SeedViewModel seed` (8th parameter):

```csharp
public MainWindowViewModel(
    IWorkspaceStore store,
    OverviewViewModel overview,
    WelcomeViewModel welcome,
    SeedViewModel seed,
    IDialogService dialog,
    IRecentFilesStore recents,
    IPasswordProtector protector,
    ILogger<MainWindowViewModel> log)
```

- Property: `public SeedViewModel Seed { get; }`.
- Ctor body: `Seed = seed; Seed.Bind(SaveWorkspaceAsync);` where `SaveWorkspaceAsync` is:
  ```csharp
  private async Task SaveWorkspaceAsync()
  {
      if (Workspace is null || _currentPath is null) return;
      try { await _store.SaveAsync(Workspace, _currentPath); }
      catch (IOException ex) { _log.LogWarning(ex, "Workspace save failed"); await _dialog.ShowMessageAsync("Save failed", ex.Message); }
      catch (UnauthorizedAccessException ex) { _log.LogWarning(ex, "Workspace save denied"); await _dialog.ShowMessageAsync("Save failed", ex.Message); }
  }
  ```
- `OnWorkspaceChanged(WorkspaceModel? value)` — extend to call `Seed.Hydrate(value, plaintextForSeed)`:
  - `string? plaintextForSeed = null;`
  - `if (value?.Source.PasswordEncrypted is { Length: > 0 } cipher) { try { plaintextForSeed = _protector.Unprotect(cipher); } catch (CryptographicException) { } catch (FormatException) { } }`
  - `Seed.Hydrate(value, plaintextForSeed);`
  - (Existing `Overview.Show(value)` and `_ = Welcome.RefreshAsync()` calls remain.)

### `MainWindow.xaml` — Seed-tab content swap

- Add `xmlns:seed="clr-namespace:Quipu.ParameterizationExtractor.Desktop.Views.Seed"`.
- Replace:
  ```xml
  <TabItem Header="Seed">
    <TextBlock Margin="20" Opacity="0.7" Text="Seed tab — implemented in desktop-seed-tab"/>
  </TabItem>
  ```
  with:
  ```xml
  <TabItem Header="Seed">
    <seed:SeedView DataContext="{Binding Seed}"/>
  </TabItem>
  ```

### `examples/sample.bws` update

Verify the current shape; if `Package.Scripts` is empty, add one entry so the Seed tab renders something on first open with the sample workspace:

```json
{
  "scriptName": "PatientClearing",
  "query": "SELECT * FROM dbo.Patient WHERE Id = 1",
  /* root reference fields per the engine model */
}
```

If the sample already has a script (likely from `desktop-workspace-format`), no change needed — verify.

### Tests

**Existing test fixtures must compile** under the 8-param ctor. Update:

- `Tests/Desktop/MainWindowViewModelOpenTests.cs`: `NewVm()` factory adds `SeedViewModel` to the constructor call. Build a real `SeedViewModel` with the test fakes (or create a `FakeSeedViewModel` if useful — probably real instance with `FakeDatabaseExplorer` etc. is fine).
- `Tests/Desktop/HostCompositionTests.cs`: existing `MainWindowViewModel_NoWorkspaceLoaded_TitleIsBranded` continues to work since `MainWindowViewModel` resolves through DI.
- `Tests/Desktop/NewWorkspaceCommandTests.cs` + `EditConnectionFlowTests.cs`: same `NewVm()` factory updates.

New tests:

`Tests/Desktop/MainWindowViewModelSeedHydrationTests.cs`:

1. `OnWorkspaceChanged_HydratesSeedWithDecryptedPassword` — load a workspace whose `PasswordEncrypted` decrypts to `"hunter2"` via `InMemoryPasswordProtector`; assert `Seed.Hydrate` was called with that plaintext (use a `SeedViewModel` test double OR assert via observable side-effects — likely `Seed.Scripts` got populated and the explorer sees a connection string with the password embedded).
2. `OnWorkspaceChanged_DecryptFailure_PassesNullPlaintextToSeed` — corrupt `PasswordEncrypted`; assert `Seed.Hydrate` invoked with null.
3. `OnWorkspaceChanged_NullWorkspace_ClearsSeed` — set workspace then null; assert `Seed.Scripts.Count == 0`.
4. `SaveWorkspaceAsync_OnIOException_ShowsDialog` — fake `IWorkspaceStore` that throws `IOException` on `SaveAsync`; trigger save via `Seed.AddScript`; assert `FakeDialogService` recorded a `ShowMessage` call.

DI smoke: covered by step 05's `DesktopHost_ResolvesSeedViewModel`. No new DI smoke here.

### Manual smoke (mandatory)

- `dotnet build "ParameterizationExtractor.Desktop"`. `dotnet run --project ParameterizationExtractor.Desktop -- examples/sample.bws`.
- App opens; switch to Seed tab.
- See the sample script (or an empty list with `+ Add` if the sample has no scripts yet).
- Click `+ Add`; type a name; pick a root table from the picker (which loads on workspace open); type a SELECT in the editor; click `Run preview`; see rows.
- Click `Cancel` mid-preview (test against a slow query if available — `SELECT * FROM big_table`); status reverts.
- Edit the script name; switch tabs and back; close and reopen the workspace; verify the edit persisted.
- Add a second script; remove the first; verify selection updates.
- Logged in checklist Notes.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-05 N + 4 (new) + 0 net (existing test fixture updates compile) = N + 4, all green.
- Manual smoke per above.

## Acceptance Criteria

- [ ] `Views/Seed/SeedView.xaml(.cs)` exists; pairing follows recipe.
- [ ] `MainWindowViewModel` ctor takes 8 params (added `SeedViewModel`); `OnWorkspaceChanged` propagates to `Seed.Hydrate(value, plaintextForSeed)`.
- [ ] `MainWindow.xaml` Seed tab hosts `<seed:SeedView/>` (not the placeholder TextBlock).
- [ ] `examples/sample.bws` has at least one script in `Package.Scripts` so first-open renders content (if it didn't already).
- [ ] `MainWindowViewModelOpenTests`, `HostCompositionTests`, `NewWorkspaceCommandTests`, `EditConnectionFlowTests` updated for 8-param ctor without test logic changes (just the factory).
- [ ] All 4 new `MainWindowViewModelSeedHydrationTests` pass.
- [ ] No `MessageBox.Show`, `IConfiguration[..]`, `Console.WriteLine`, `Dispatcher.Invoke`, `TODO`, `FIXME` introduced.
- [ ] Manual smoke walked end-to-end (Add script → preview → cancel → edit name → close+reopen → persisted). Logged in checklist Notes.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § How to add a screen.
- Mockup: [`docs/design/desktop-ui/04-seed.md`](../../docs/design/desktop-ui/04-seed.md).
- Pattern exemplars: `Views/Welcome/` (View + Bind handler from MainWindow), `MainWindow.xaml` (existing Tab content swaps).
- Depends on: [04 — ScriptEditorViewModel](./04-desktop-seed-tab-script-editor-vm.md), [05 — SeedViewModel](./05-desktop-seed-tab-seed-vm-container.md).
