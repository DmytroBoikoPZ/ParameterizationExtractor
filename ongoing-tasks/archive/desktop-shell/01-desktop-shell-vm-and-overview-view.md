# 01 — desktop-shell — Shell chrome + Overview view + VMs

## Goal

Replace `MainWindow.xaml`'s empty `<Grid />` with the M3 shell chrome (toolbar + tab strip + status bar + dirty title), refactor `MainWindowViewModel` to hold a `WorkspaceModel?` and child VMs, and ship the first real `View` ↔ `ViewModel` pair (`OverviewView` + `OverviewViewModel`) under `Views/Overview/`. **No workspace loading yet** — that happens in step 02. Step 01 produces the shell as a static structure that renders correctly given a non-null `Workspace`.

## Track

`desktop` (WPF / C#).

## What Exists

- [`ParameterizationExtractor.Desktop/MainWindow.xaml`](../../ParameterizationExtractor.Desktop/MainWindow.xaml) — `mah:MetroWindow` with `Title="{Binding WindowTitle}"` and `<Grid />` body.
- [`ParameterizationExtractor.Desktop/MainWindow.xaml.cs`](../../ParameterizationExtractor.Desktop/MainWindow.xaml.cs) — only `InitializeComponent()`.
- [`ParameterizationExtractor.Desktop/MainWindowViewModel.cs`](../../ParameterizationExtractor.Desktop/MainWindowViewModel.cs) — `internal sealed partial class : ObservableObject` with one read-only property `WindowTitle => "SQL Buldozer"`.
- [`ParameterizationExtractor.Desktop/Services/Workspace/WorkspaceModel.cs`](../../ParameterizationExtractor.Desktop/Services/Workspace/WorkspaceModel.cs) — POCO holding `Version`, `Name`, `Source`, `Global`, `Package`.
- [`docs/methodology/wpf-desktop.md` § How to add a screen](../../docs/methodology/wpf-desktop.md) — the procedure this step follows.
- [`docs/design/desktop-ui/03-shell.md`](../../docs/design/desktop-ui/03-shell.md) — the visual contract.
- Tests: [`Tests/Desktop/HostCompositionTests.cs`](../../Tests/Desktop/HostCompositionTests.cs) — pattern exemplar for DI-resolution tests on the desktop side.
- FluentAssertions 7.2.0 + NUnit 4 in `Tests.csproj`.

## What to Build

### `MainWindowViewModel.cs` refactor

- Add fields: `[ObservableProperty] private WorkspaceModel? _workspace;`
- Add child VM: `public OverviewViewModel Overview { get; }`. Take it via constructor injection from DI alongside the existing `IWorkspaceStore` (which is added in step 02 — for step 01, no `IWorkspaceStore` dep yet; add a placeholder ctor parameter only when step 02 needs it).
- Computed properties (use `[NotifyPropertyChangedFor(nameof(WindowTitle))]` etc. to recompute on `Workspace` change):
  - `WindowTitle` → `"SQL Buldozer"` when `Workspace == null`; `"SQL Buldozer — {Workspace.Name}{(IsDirty ? " *" : "")}"` otherwise.
  - `LastSavedDisplay` → always `"never"` (slot for future Save).
  - `ConnectionDisplay` → `""` if no workspace; `"{Workspace.Source.Database} @ {Workspace.Source.Server}"` otherwise.
- `IsDirty` → always `false` in this feature; slot exists so binding compiles.
- VMs never reference WPF types (verified by inspection — only `using CommunityToolkit.Mvvm.*` and engine namespaces).

### `Views/Overview/OverviewViewModel.cs` (new)

- `internal sealed partial class OverviewViewModel : ObservableObject`. Constructor takes nothing (no services for step 01; binding to `WorkspaceModel` is via a method/property the parent calls). Use the simplest shape that compiles.
- Add a single method `void Show(WorkspaceModel? workspace)` that sets observable properties from the workspace. Keep computed-string properties:
  - `WorkspaceName` (string) — `"<no workspace>"` when null; `workspace.Name` otherwise.
  - `SourceSummary` (string) — `"<no workspace>"` or `"{database} @ {server}"`.
  - `SeedSummary` (string) — `"<not yet configured>"` (slot until seed support lands).
  - `PathSummary` (string) — `"<no workspace>"` or `"{N} tables in package"` where N = `workspace.Package.Scripts.SelectMany(s => s.TablesToProcess).Count()`.
  - `ExtrasSummary` (string) — always `"0 standalone tables · 0 scripts"` (placeholder; no Extras concept on `WorkspaceModel` yet).
  - `LastDryRunSummary` (string) — always `"never"`.
  - `PendingWarning` (string?) — always `null`.
- `MainWindowViewModel.Workspace` setter calls `Overview.Show(value)` so changes propagate.

### `Views/Overview/OverviewView.xaml` + `.xaml.cs` (new)

- `UserControl` with `x:ClassModifier="internal"`; namespace `Quipu.ParameterizationExtractor.Desktop.Views.Overview`.
- Layout: matches mockup [M3 Overview content](../../docs/design/desktop-ui/03-shell.md). Vertical stack of TextBlocks bound to the VM's summary properties. No styling cleverness — readable and minimal.
- Code-behind contains only `InitializeComponent()`.

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- Add: `services.AddSingleton<OverviewViewModel>();` alongside the existing `MainWindow` / `MainWindowViewModel` Singletons.
- `MainWindowViewModel` constructor adds an `OverviewViewModel` parameter; DI resolves it as Singleton.

### `MainWindow.xaml` redesign

- Top section: title-bar text from `WindowTitle` (existing binding); `mah:MetroWindow` chrome stays.
- New layout (proposed; finalise during the step):
  ```
  Grid (rows: Toolbar=Auto / Content=* / StatusBar=Auto)
    ├── Toolbar (StackPanel Horizontal; placeholder buttons disabled)
    ├── TabControl (5 tabs)
    │     ├── Overview tab → <views:OverviewView DataContext="{Binding Overview}" />
    │     ├── Seed tab     → <TextBlock>Seed tab — implemented in desktop-seed-tab</TextBlock>
    │     ├── Graph tab    → <TextBlock>Graph tab — implemented in desktop-graph-viz / desktop-graph-tab</TextBlock>
    │     ├── Extras tab   → <TextBlock>Extras tab — implemented in desktop-extras-tab</TextBlock>
    │     └── Run tab      → <TextBlock>Run tab — implemented in desktop-dry-run-and-execute</TextBlock>
    └── StatusBar (TextBlock bound to LastSavedDisplay etc.)
  ```
- Empty-shell mode: when `Workspace == null`, the Overview tab's content shows a single line `"Pass a .bws path on the command line to load a workspace. (Welcome view lands in desktop-startup-and-open-workspace.)"` — this can be a value converter, a DataTrigger, or a sibling TextBlock with `Visibility` driven by `Workspace == null`. Author's call.
- Code-behind on `MainWindow.xaml.cs` stays at `InitializeComponent()`.

### Tests (TDD)

Per [`prompts/execution.md`](../../prompts/execution.md) CODE classification — RED → GREEN → REFACTOR.

New file `Tests/Desktop/OverviewViewModelTests.cs`:

1. `Show_WithNullWorkspace_PopulatesEmptyStateStrings` — every "summary" property reads `"<no workspace>"` or empty.
2. `Show_WithWorkspace_PopulatesNameAndSourceSummary` — given a `WorkspaceModel { Name = "X", Source = { Server = "s", Database = "d" } }`, asserts `WorkspaceName == "X"` and `SourceSummary == "d @ s"`.
3. `Show_WithMultiTableWorkspace_PathSummaryCountsTables` — workspace with 2 scripts × 3 tables each → `PathSummary == "6 tables in package"` (or whatever phrasing the impl picks; the test follows the impl's own constant).

New tests in existing `Tests/Desktop/HostCompositionTests.cs`:

4. `DesktopHost_ResolvesOverviewViewModel` — DI smoke test mirroring the existing `IWorkspaceStore` test.
5. `MainWindowViewModel_NoWorkspaceLoaded_TitleIsBranded` — instantiate VM with default state; assert `WindowTitle == "SQL Buldozer"`.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test "SQL Buldozer.sln"` — pre-existing 55 + new 5 = **60 tests**, all green.
- Manual launch: `dotnet run --project ParameterizationExtractor.Desktop` opens a window with the tab strip and an empty-shell Overview. Step 02 introduces the path-arg path.

## Acceptance Criteria

- [ ] `Views/Overview/OverviewView.xaml` and `OverviewViewModel.cs` exist; `OverviewView.xaml.cs` contains only `InitializeComponent()`; `OverviewViewModel` doesn't reference any WPF type (grep verifies).
- [ ] `MainWindow.xaml` body is a `Grid` with three rows (Toolbar / TabControl / StatusBar). The TabControl has exactly 5 tabs in the order Overview / Seed / Graph / Extras / Run.
- [ ] Overview tab content is `<views:OverviewView DataContext="{Binding Overview}" />`. Other tabs hold a single placeholder `TextBlock` referencing the future feature name.
- [ ] `MainWindowViewModel` exposes `Workspace` (`WorkspaceModel?`), `Overview` (child VM), `WindowTitle` (computed), `LastSavedDisplay`, `ConnectionDisplay`, `IsDirty`. Setting `Workspace` triggers `Overview.Show(value)`.
- [ ] `OverviewViewModel.Show(workspace)` populates all 7 summary properties; null input maps to `"<no workspace>"`-style placeholders.
- [ ] DI registration adds `OverviewViewModel` as Singleton; `DesktopHost_ResolvesOverviewViewModel` test passes.
- [ ] No `Click=` event handlers, no logic in `.xaml.cs` files beyond `InitializeComponent()`. Verified by grep.
- [ ] No `MessageBox.Show`, no `IConfiguration[..]`, no `Console.WriteLine`, no `Dispatcher.Invoke` introduced. Verified by grep.
- [ ] `dotnet build` 0 errors; `dotnet test` 60/60 pass.
- [ ] Manual: `dotnet run --project ParameterizationExtractor.Desktop` opens a window showing the tab strip; Overview tab shows the empty-shell placeholder. Window closes cleanly with exit 0.

## References

- ADR: [`adr/007-desktop-wpf-stack.md`](../../adr/007-desktop-wpf-stack.md), [`adr/008-desktop-ui-controls.md`](../../adr/008-desktop-ui-controls.md), [`adr/009-workspace-format.md`](../../adr/009-workspace-format.md)
- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) (specifically § How to add a screen)
- Mockup: [`docs/design/desktop-ui/03-shell.md`](../../docs/design/desktop-ui/03-shell.md)
- Pattern exemplars in code: [`Tests/Desktop/HostCompositionTests.cs`](../../Tests/Desktop/HostCompositionTests.cs), existing `MainWindow.xaml`, existing `Services/Workspace/`.
- Depends on: nothing (first step).
