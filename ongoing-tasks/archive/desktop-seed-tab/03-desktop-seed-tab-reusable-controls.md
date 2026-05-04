# 03 — desktop-seed-tab — Reusable controls: SqlEditor + TablePicker + RowsPreviewGrid

## Goal

Land three reusable controls per the [components map](../../docs/design/desktop-ui/components.md): `SqlEditor` (the foundational AvalonEdit wrapper — first feature to land it; reused by `desktop-extras-tab` and `desktop-graph-tab`), `TablePicker` (combobox over `IDatabaseExplorer.ListTablesAsync`), and `RowsPreviewGrid` (DataGrid materialiser for `PreviewResult`). Step 03 produces controls that compile + render in isolation; step 04 hosts them in the Seed view.

## Track

`desktop` (WPF / C#).

## What Exists

- `IDatabaseExplorer` (step 01) + `IUiDispatcher` (step 02).
- AvalonEdit dependency already declared in `ParameterizationExtractor.Desktop.csproj` (per ADR-008). If not actually present yet, this step adds it.
- [`Controls/ConnectionEditor/`](../../ParameterizationExtractor.Desktop/Controls/ConnectionEditor/) — pattern exemplar for a reusable Control + VM pair.
- [`docs/methodology/wpf-desktop.md` § Code editor (AvalonEdit)](../../docs/methodology/wpf-desktop.md) — the recipe rule that AvalonEdit types stay inside `SqlEditor`.

## What to Build

### `Controls/SqlEditor/SqlEditorView.xaml(.cs)` + dependency properties

- AvalonEdit NuGet (verify already-installed; if not, add `<PackageReference Include="ICSharpCode.AvalonEdit" Version="..." />` — pick the latest stable that targets net10.0-windows).
- `UserControl`, `x:ClassModifier="internal"`, namespace `Quipu.ParameterizationExtractor.Desktop.Controls.SqlEditor`.
- XAML: `<avalon:TextEditor x:Name="Editor" SyntaxHighlighting="TSQL" ... />` with `xmlns:avalon="http://icsharpcode.net/sharpdevelop/avalonedit"`.
- Code-behind dependency properties:
  - `public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(SqlEditorView), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));`
  - `public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(...)` — bool, default false.
  - `public static readonly DependencyProperty IsSingleLineProperty = DependencyProperty.Register(...)` — bool, default false. When true: `Editor.ShowLineNumbers = false;` and intercept Enter to send Tab.
- Bridge `Editor.TextChanged` → set `Text` DP (with an `_isUpdating` guard to avoid loops, mirroring `PasswordBoxBindingBehavior`).
- Bridge `Text` DP changed → set `Editor.Text` (with the same guard).
- Bridge `IsReadOnly` DP changed → set `Editor.IsReadOnly`.

Code-behind contains the bridges + `InitializeComponent()` only — no business logic.

### `App.xaml.cs` — register T-SQL highlighting once

In `OnStartup`, before `host.StartAsync`:

```csharp
var hm = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance;
if (hm.GetDefinition("TSQL") is null)
{
    using var stream = ...; // load AvalonEdit's built-in T-SQL definition or our own .xshd
    var def = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(...);
    hm.RegisterHighlighting("TSQL", new[] { ".sql" }, def);
}
```

AvalonEdit ships with several built-in definitions; verify whether T-SQL is one of them. If yes, just register; if not, embed an `.xshd` resource (small file). Document the choice in Notes.

### `Controls/TablePicker/TablePickerView.xaml(.cs) + TablePickerViewModel.cs`

`internal sealed partial class TablePickerViewModel : ObservableObject`:

- Constructor: `(IDatabaseExplorer explorer, ILogger<TablePickerViewModel> log)`.
- `ObservableCollection<TableRef> Tables { get; } = new();`
- `[ObservableProperty] private TableRef? _selectedTable;`
- `[ObservableProperty] private bool _isLoading;`
- `[ObservableProperty] private string? _errorMessage;`
- `public string? ConnectionString { get; set; }` — set by parent (Seed VM via the connection-string builder).
- `[RelayCommand] private async Task RefreshAsync()`:
  - If `ConnectionString` null/empty → set `ErrorMessage = "No connection"; return;`.
  - `IsLoading = true; ErrorMessage = null; try { var refs = await _explorer.ListTablesAsync(ConnectionString); Tables.Clear(); foreach (var t in refs) Tables.Add(t); } catch (DatabaseExplorerException ex) { ErrorMessage = ex.Message; } finally { IsLoading = false; }`

`TablePickerView.xaml`: `<ComboBox ItemsSource="{Binding Tables}" SelectedItem="{Binding SelectedTable, Mode=TwoWay}" IsEditable="True" IsTextSearchEnabled="True" DisplayMemberPath="..." />` — actually `TableRef.ToString()` could format `{Schema}.{Name}` if we override; cleaner: a `DataTemplate` on the items with `<TextBlock Text="{Binding Path=Schema}"/>.<TextBlock Text="{Binding Path=Name}"/>` joined. Or override `TableRef.ToString` in a `partial` method on the record (records support `ToString` override).

### `Controls/RowsPreviewGrid/RowsPreviewGridView.xaml(.cs)`

- `UserControl`, `internal`. DPs:
  - `Result` (`PreviewResult?`, default null).
  - On `Result` change: rebuild the `DataGrid.Columns` from `Result.ColumnNames` and set `DataGrid.ItemsSource = Result.Rows` (each row is a `string?[]` — DataGrid can bind via `[index]` for column N or via a row-converter; pick the simpler one — a `RowsToDataTableConverter` that produces a `DataTable` is the cleanest WPF idiom).
- Truncation banner: a small `<Border>` above the DataGrid with `Visibility="{Binding Result.Truncated, Converter={StaticResource BoolToVis}}"` and `<TextBlock Text="Showing first 200 rows; refine the query to see more"/>`.

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- `services.AddTransient<TablePickerViewModel>();` — Transient because each consumer (Seed tab today, Extras tab tomorrow) gets its own instance.

The reusable controls themselves (`SqlEditorView`, `TablePickerView`, `RowsPreviewGridView`) are `UserControl`s instantiated by their hosting view's XAML; no DI registration needed for the views.

### Tests

Per `prompts/execution.md` — CODE: TDD for VMs.

New file `Tests/Desktop/TablePickerViewModelTests.cs`:

1. `Default_State_Empty`.
2. `RefreshAsync_NoConnectionString_SetsErrorMessage`.
3. `RefreshAsync_ExplorerReturnsTables_PopulatesCollection` — uses a fake `IDatabaseExplorer` that returns a stub list.
4. `RefreshAsync_ExplorerThrows_SetsErrorMessage_DoesNotThrow`.
5. `IsLoading_TogglesAroundRefresh`.

`FakeDatabaseExplorer` (NEW, `Tests/Desktop/Fakes/FakeDatabaseExplorer.cs`): queue-driven (results + exceptions), records calls. Mirrors `FakeConnectionTester`.

The WPF UI bits (SqlEditorView, RowsPreviewGridView) are NOT unit-tested per recipe — exercised manually + via the Seed view smoke in step 04.

DI smoke in `HostCompositionTests.cs`:
6. `DesktopHost_ResolvesTablePickerViewModel_AsTransient`.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-02 N + 6 = N + 6, all green.
- Manual smoke: not feasible until step 04 hosts them. Mark as deferred.

## Acceptance Criteria

- [ ] `Controls/SqlEditor/SqlEditorView.xaml(.cs)` exists; three DPs (`Text`, `IsReadOnly`, `IsSingleLine`); AvalonEdit types confined to the `.xaml.cs` (no leakage to public surface).
- [ ] T-SQL highlighting registered once in `App.xaml.cs` `OnStartup`.
- [ ] `Controls/TablePicker/{TablePickerView.xaml(.cs), TablePickerViewModel.cs}` exist; VM has `Tables`, `SelectedTable`, `RefreshCommand`, `ConnectionString` slot.
- [ ] `Controls/RowsPreviewGrid/RowsPreviewGridView.xaml(.cs)` exists; renders columns from `PreviewResult.ColumnNames`; truncation banner.
- [ ] `FakeDatabaseExplorer` test fake exists.
- [ ] `TablePickerViewModel` registered Transient.
- [ ] All 6 new tests pass.
- [ ] No `MessageBox.Show` / `IConfiguration[..]` / `Console.WriteLine` / `TODO` / `FIXME`. AvalonEdit `using` only in `Controls/SqlEditor/`. Tripwires honoured.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § Code editor.
- Components map: [SqlEditor](../../docs/design/desktop-ui/components.md#sqleditor), [TablePicker](../../docs/design/desktop-ui/components.md#tablepicker), [RowsPreviewGrid](../../docs/design/desktop-ui/components.md#rowspreviewgrid).
- Pattern exemplars: `Controls/ConnectionEditor/`, `Controls/ConnectionEditor/PasswordBoxBindingBehavior.cs` (DP bridge pattern).
- Depends on: [01 — IDatabaseExplorer](./01-desktop-seed-tab-database-explorer.md).
