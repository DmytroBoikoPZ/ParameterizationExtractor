# 03 — desktop-seed-tab-root-inference — `SeedView` reordering + inline hint banner + manual smoke

## Goal

Make the Seed tab's narrative obvious: SQL editor is primary; Root picker is a passive auto-detected confirmation; mismatch surfaces as a small inline banner under the picker. **No VM changes** — step 02 already added `RootMismatchHint`.

## Track

`desktop` (WPF / C#).

## What Exists

- [`Desktop/Views/Seed/SeedView.xaml`](../../ParameterizationExtractor.Desktop/Views/Seed/SeedView.xaml) — current Seed-tab layout. Root picker + SQL editor are visually peers.
- `ScriptEditorViewModel.RootMismatchHint` (step 02).

## What to Build

### `SeedView.xaml` — minor reorder + relabel

In the per-script `<DataTemplate>`:

- Change the Root row label from `Root:` to `Root (auto-detected from query):` — softens the implication that the operator must set it manually.
- Add an inline hint banner immediately under the Root row, visible only when `RootMismatchHint` is non-empty:

  ```xml
  <Border Grid.Row="0" Margin="0,4,0,8"
          Padding="8,4"
          Background="#FFFFF4CE"
          BorderBrush="#FFE0C04C"
          BorderThickness="0,0,0,1"
          Visibility="{Binding RootMismatchHint, Converter={StaticResource StringNonEmptyToVis}}">
    <DockPanel LastChildFill="True">
      <Button DockPanel.Dock="Right"
              Content="✕"
              Padding="4,0"
              Margin="8,0,0,0"
              Background="Transparent"
              BorderThickness="0"
              Command="{Binding DismissRootMismatchHintCommand}"/>
      <TextBlock Text="{Binding RootMismatchHint}"
                 Foreground="#FF7A5A00"
                 TextWrapping="Wrap"/>
    </DockPanel>
  </Border>
  ```

  **Decision:** `StringNonEmptyToVis` converter — a simple `IValueConverter` mapping non-empty string → Visible, empty → Collapsed. If it doesn't already exist, add to `Desktop/Converters/StringNonEmptyToVisibilityConverter.cs` + register in `App.xaml`. (Mirrors the existing `InverseBoolToVis`.)

  Or sidestep: bind Visibility to `RootMismatchHint.Length > 0` via a tiny converter. Same effect, cleaner.

- Make the **SQL editor row** (`<editor:SqlEditorView Grid.Row="1" .../>`) visually primary by giving it more vertical breathing room (e.g. `MinHeight="240"`) so the operator's eye lands there first.

- Add a subtle hint TextBlock above the SQL editor: `"Type a SELECT — the root table is detected automatically. Click Run preview to see matched rows."` Small, italic, dimmed.

### `Converters/StringNonEmptyToVisibilityConverter.cs` (new)

```csharp
internal sealed class StringNonEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

Register in `App.xaml`:
```xml
<converters:StringNonEmptyToVisibilityConverter x:Key="StringNonEmptyToVis"/>
```

### Manual smoke (mandatory — operator-driven)

- Run the desktop against `examples/sample.bws` (assumes step 02 done).
- Open the Seed tab → see the new "Root (auto-detected from query):" label and the SQL hint above the editor.
- Type `SELECT * FROM dbo.Patient` into the SQL editor → Root picker auto-fills with `dbo.Patient`. No mismatch banner.
- Manually change the picker to `dbo.Visit` → mismatch banner appears: "SQL targets dbo.Patient; picked root is dbo.Visit".
- Click the ✕ on the banner → banner clears.
- Edit SQL to `SELECT * FROM dbo.Visit` → banner cleared (matches now).
- Erase the picker (back to empty) and type a fresh SQL → picker re-auto-populates.
- Save / close / reopen the workspace → typed SQL + picked root persist (existing behaviour).

Log outcome in checklist Notes (date + bullets).

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-02 N stays at N (no new VM tests; UI not unit-tested per recipe).
- Manual smoke per above.

## Acceptance Criteria

- [ ] `SeedView.xaml` Root row relabelled.
- [ ] Inline mismatch banner present, dismissible, bound to `RootMismatchHint`.
- [ ] `StringNonEmptyToVis` (or equivalent) converter added + registered.
- [ ] SQL editor minimally repositioned to be visually primary; small narrative hint above it.
- [ ] No business logic in code-behind (recipe tripwire).
- [ ] Manual smoke walked end-to-end. Logged in checklist Notes.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Pattern exemplars: `Controls/RowsPreviewGrid/RowsPreviewGridView.xaml` (truncation banner — same structural pattern), `Converters/InverseBoolConverter.cs`.
- Depends on: [02 — VM wiring](./02-desktop-seed-tab-root-inference-vm-wiring.md).
