# 03 — desktop-startup-and-open-workspace — Welcome view + Open workflow + File menu

## Goal

Replace `MainWindow.xaml`'s empty-shell stub with a proper Welcome view (mockup [M1](../../docs/design/desktop-ui/01-startup.md)) when `Workspace == null`. Wire the Open workflow end-to-end through `IDialogService` (step 01) and `IRecentFilesStore` (step 02). Add a File menu (`New (disabled) / Open / Recent ▸ / Close / Exit`). Steps 01 + 02 land the abstractions; step 03 is the first user-visible consumer.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`ParameterizationExtractor.Desktop/MainWindow.xaml`](../../ParameterizationExtractor.Desktop/MainWindow.xaml) — current 3-row Grid (toolbar / TabControl / status bar). Step 03 wraps the existing chrome in a visibility-driven swap.
- [`ParameterizationExtractor.Desktop/MainWindowViewModel.cs`](../../ParameterizationExtractor.Desktop/MainWindowViewModel.cs) — current state per `desktop-shell` step 02: ctor takes `IWorkspaceStore` + `OverviewViewModel`; has `Workspace`, `WindowTitle`, `LastSavedDisplay`, `ConnectionDisplay`, `IsDirty`, `InitializeAsync`. `OnWorkspaceChanged` propagates to `Overview.Show`.
- [`ParameterizationExtractor.Desktop/App.xaml.cs`](../../ParameterizationExtractor.Desktop/App.xaml.cs) — current `OnStartup` parses `e.Args.FirstOrDefault()` and awaits `InitializeAsync(path)` before `Show()`.
- [`ParameterizationExtractor.Desktop/Views/Overview/`](../../ParameterizationExtractor.Desktop/Views/Overview/) — canonical View ↔ ViewModel pairing reference.
- `IDialogService` + `DialogService` + `FakeDialogService` from step 01.
- `IRecentFilesStore` + `JsonRecentFilesStore` + `InMemoryRecentFilesStore` from step 02.

## What to Build

### `Views/Welcome/WelcomeViewModel.cs`

`internal sealed partial class WelcomeViewModel : ObservableObject`. Namespace `Quipu.ParameterizationExtractor.Desktop.Views.Welcome`.

Constructor:
```csharp
public WelcomeViewModel(IDialogService dialog, IRecentFilesStore recents, Func<string, Task> openHandler)
```

The `Func<string, Task> openHandler` is supplied by `MainWindowViewModel` at construction (see DI section below). Welcome doesn't load workspaces directly — it bubbles a path back to the parent.

Members:
- `[ObservableProperty] private bool _isLoadingRecents;` — flips during `RefreshAsync`.
- `public ObservableCollection<RecentFile> Recents { get; } = new();` — populated by `RefreshAsync`.
- `public bool HasRecents => Recents.Count > 0;` — drives empty-state text. Wire via `Recents.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasRecents));` in the ctor.
- `public bool IsNewWorkspaceEnabled => false;` — locked false in this feature; binding is in place so `desktop-connection-management` flips it without re-laying-out the view.
- `public string NewWorkspaceTooltip => "Available with desktop-connection-management";` — bound to `[ New workspace... ]`'s `ToolTip`.
- `[RelayCommand] private async Task OpenAsync()` — calls `_dialog.OpenFileAsync("Open workspace", "SQL Buldozer workspace (*.bws)|*.bws|All files (*.*)|*.*")`; if non-null path, `await _openHandler(path)`.
- `[RelayCommand] private async Task OpenRecentAsync(string? path)` — guards `path is not null`; `await _openHandler(path)`.
- `public async Task RefreshAsync()` — `IsLoadingRecents = true; try { var items = await _recents.GetAsync(); Recents.Clear(); foreach (var i in items) Recents.Add(i); } finally { IsLoadingRecents = false; }`. Called by `MainWindowViewModel.OnWorkspaceChanged` when the workspace becomes null and once at construction.
- `[RelayCommand] private async Task RefreshAsyncCommand()` — re-exposed for tests / future "Refresh recents" UX.

VMs never reference WPF types — verified by grep on the file (no `using System.Windows*`).

### `Views/Welcome/WelcomeView.xaml` + `.xaml.cs`

- `UserControl`, `x:ClassModifier="internal"`, code-behind only `InitializeComponent()`.
- Layout (per [M1 mockup](../../docs/design/desktop-ui/01-startup.md)):
  ```
  Grid centred horizontally and vertically (using HorizontalAlignment="Center" + VerticalAlignment="Center" + an outer Border with rounded corners + soft shadow if MahApps style allows; minimum: a centred StackPanel)
    StackPanel
      ├── TextBlock "No workspace open"   (header — 18pt)
      ├── empty row spacer
      ├── StackPanel Horizontal
      │     ├── Button "New workspace..."    IsEnabled="{Binding IsNewWorkspaceEnabled}" ToolTip="{Binding NewWorkspaceTooltip}"
      │     └── Button "Open workspace..."   Command="{Binding OpenCommand}"
      ├── TextBlock "Recent:" (caption)
      ├── ContentControl bound to HasRecents:
      │     ├── True  → ItemsControl bound to Recents; each item is a hyperlink-styled Button:
      │     │             Content = "{Binding Path}"  (display the full path; trim with TextTrimming="CharacterEllipsis" if needed)
      │     │             Command = "{Binding DataContext.OpenRecentCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
      │     │             CommandParameter = "{Binding Path}"
      │     │             plus a subtle " — {LastOpenedUtc:relative}" caption (use a converter or a precomputed string)
      │     └── False → TextBlock "(empty)"  italic, dim
      └── (no extras; M1 keeps it minimal)
  ```
- Use the existing MahApps theme — no per-window resources (tripwire).
- Trimming long paths is acceptable; the full path appears in the button's `ToolTip`.

### `MainWindowViewModel.cs` extensions

- New ctor parameters (added to existing): `IDialogService dialog, IRecentFilesStore recents, WelcomeViewModel welcome`.
  - Resulting ctor signature:
    ```csharp
    public MainWindowViewModel(
        IWorkspaceStore store,
        OverviewViewModel overview,
        WelcomeViewModel welcome,
        IDialogService dialog,
        IRecentFilesStore recents,
        ILogger<MainWindowViewModel> log)
    ```
  - `ILogger<MainWindowViewModel>` is added to surface the open-failure path's log entry. (Today the VM has no logger; this is the first feature to need one.)
- New properties:
  - `public WelcomeViewModel Welcome { get; }`.
  - `public bool IsWorkspaceLoaded => Workspace is not null;` — annotate `Workspace` with `[NotifyPropertyChangedFor(nameof(IsWorkspaceLoaded))]`.
- New commands:
  - `[RelayCommand] private async Task OpenAsync()` — delegates to `_dialog.OpenFileAsync(...)` then `OpenWorkspaceAsync(path)`.
  - `[RelayCommand(CanExecute = nameof(CanClose))] private void Close()` — sets `Workspace = null`. `CanClose` returns `IsWorkspaceLoaded`.
  - `[RelayCommand] private static void Exit() => Application.Current?.Shutdown();` — service-level access to `Application.Current` is allowed (mirrors `DialogService`'s comment); add the same one-line comment.
- New method (the canonical entry point):
  ```csharp
  public async Task OpenWorkspaceAsync(string path, CancellationToken ct = default)
  {
      try
      {
          var ws = await _store.LoadAsync(path, ct).ConfigureAwait(true);
          Workspace = ws;
          await _recents.PushAsync(path, ct).ConfigureAwait(true);
      }
      catch (FileNotFoundException ex)
      {
          _log.LogWarning(ex, "Workspace not found: {Path}", path);
          await _dialog.ShowMessageAsync("Workspace not found", ex.Message);
      }
      catch (JsonException ex)
      {
          _log.LogWarning(ex, "Workspace JSON invalid: {Path}", path);
          await _dialog.ShowMessageAsync("Workspace is not valid JSON", ex.Message);
      }
      catch (InvalidOperationException ex)
      {
          _log.LogWarning(ex, "Workspace cannot be opened: {Path}", path);
          await _dialog.ShowMessageAsync("Workspace cannot be opened", ex.Message);
      }
  }
  ```
- `InitializeAsync(path, ct)` is refactored to delegate: `if (string.IsNullOrEmpty(path)) { Workspace = null; return; } await OpenWorkspaceAsync(path, ct);`. **Note the behaviour change:** `InitializeAsync` no longer rethrows on missing/invalid file — it surfaces a dialog. The previous `MainWindowViewModel_InitializeAsync_WithMissingFile_Throws` test from `desktop-shell` is **renamed** to `MainWindowViewModel_InitializeAsync_WithMissingFile_ShowsDialog_LeavesWorkspaceNull`. This is intentional — at this point the user has the UI to recover.
- `OnWorkspaceChanged(WorkspaceModel? value)` calls `Overview.Show(value)` (existing) and `_ = Welcome.RefreshAsync()` (new — fire-and-forget; refreshing a tiny in-memory list won't block, and we don't want `OnWorkspaceChanged` to be async).

### `Views/Welcome/WelcomeViewModel` open-handler wiring

`MainWindowViewModel` cannot ctor-inject `WelcomeViewModel` and have `WelcomeViewModel` ctor-inject a `Func<string, Task>` pointing at `MainWindowViewModel.OpenWorkspaceAsync` — that's a circular DI dependency.

**Resolution:** the `Func<string, Task>` is supplied not via DI but via a small "init" call from `MainWindowViewModel`'s constructor:

- `WelcomeViewModel` is registered Singleton with the **two service deps only** (`IDialogService`, `IRecentFilesStore`). The `Func<string, Task>` is settable via an internal method `internal void Bind(Func<string, Task> openHandler)`. `WelcomeViewModel.OpenAsync` and `OpenRecentAsync` first guard `if (_openHandler is null) throw new InvalidOperationException("WelcomeViewModel.Bind not called");`.
- `MainWindowViewModel`'s ctor calls `welcome.Bind(OpenWorkspaceAsync);` immediately after assigning fields. This is the cleanest option that the architecture overview's "Risks & Open Questions" section discusses.
- Tests construct `WelcomeViewModel` with a test handler (e.g. `path => { _openCalls.Add(path); return Task.CompletedTask; }`); no DI involvement.

If the `Bind` flavour proves brittle, revisit in a follow-up; an `IMessenger` route is the obvious next option.

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- `services.AddSingleton<WelcomeViewModel>();`
- `services.AddSingleton<MainWindowViewModel>();` (already registered — no change).

### `MainWindow.xaml` redesign

- Add a `<Menu>` row above the toolbar:
  ```xml
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>  <!-- Menu (NEW) -->
    <RowDefinition Height="Auto"/>  <!-- Toolbar (existing; visibility on IsWorkspaceLoaded) -->
    <RowDefinition Height="*"/>     <!-- Content (Welcome OR TabControl) -->
    <RowDefinition Height="Auto"/>  <!-- StatusBar (existing; visibility on IsWorkspaceLoaded) -->
  </Grid.RowDefinitions>
  ```
- Menu structure:
  ```xml
  <Menu Grid.Row="0">
    <MenuItem Header="_File">
      <MenuItem Header="_New workspace..." IsEnabled="False"/>   <!-- ToolTip optional -->
      <MenuItem Header="_Open workspace..." Command="{Binding OpenCommand}"/>
      <MenuItem Header="_Recent" ItemsSource="{Binding Welcome.Recents}">
        <MenuItem.ItemContainerStyle>
          <Style TargetType="MenuItem">
            <Setter Property="Header" Value="{Binding Path}"/>
            <Setter Property="Command" Value="{Binding DataContext.Welcome.OpenRecentCommand, RelativeSource={RelativeSource AncestorType=Menu}}"/>
            <Setter Property="CommandParameter" Value="{Binding Path}"/>
          </Style>
        </MenuItem.ItemContainerStyle>
      </MenuItem>
      <Separator/>
      <MenuItem Header="_Close workspace" Command="{Binding CloseCommand}"/>
      <Separator/>
      <MenuItem Header="E_xit" Command="{Binding ExitCommand}"/>
    </MenuItem>
  </Menu>
  ```
  Note: when `Welcome.Recents` is empty, the Recent submenu is empty — that's fine for v1. Optionally add a single disabled `MenuItem Header="(none)"` via a CollectionContainer; defer.
- Toolbar (Grid.Row=1) and StatusBar (Grid.Row=3) get `Visibility="{Binding IsWorkspaceLoaded, Converter={StaticResource BoolToVis}}"`.
- Content (Grid.Row=2) is a `Grid` with two children:
  - `<welcome:WelcomeView DataContext="{Binding Welcome}" Visibility="{Binding IsWorkspaceLoaded, Converter={StaticResource InverseBoolToVis}}"/>`
  - `<TabControl ...existing... Visibility="{Binding IsWorkspaceLoaded, Converter={StaticResource BoolToVis}}"/>`
- Add `xmlns:welcome="clr-namespace:Quipu.ParameterizationExtractor.Desktop.Views.Welcome"` to the root.
- Add an `InverseBoolToVis` converter: WPF doesn't ship one, so add a tiny `Converters/InverseBoolToVisibilityConverter.cs` (`internal sealed`, implements `IValueConverter`). Register in `App.xaml`'s `<Application.Resources>` alongside the existing `BoolToVis`.

### `App.xaml.cs` — recents push on command-line load

- Modify `OnStartup` to push to recents after a successful `InitializeAsync(path)`:
  ```csharp
  var workspacePath = e.Args.FirstOrDefault();
  await vm.InitializeAsync(workspacePath);
  if (!string.IsNullOrEmpty(workspacePath) && vm.Workspace is not null)
  {
      // recents push happens inside OpenWorkspaceAsync now (via InitializeAsync delegation);
      // no extra call needed here.
  }
  ```
  In practice, `InitializeAsync` → `OpenWorkspaceAsync` already pushes via `_recents.PushAsync(path)`. So **`App.xaml.cs` doesn't need to call `_recents` directly** — the change is removing any path that pushes outside the VM. Verify: command-line successful load → recents file has the entry. The existing test `MainWindowViewModel_InitializeAsync_WithSampleBws_LoadsWorkspaceAndPropagatesToOverview` is extended (or a new test added) to assert recents.

### Tests

Update `Tests/Desktop/HostCompositionTests.cs`:

- New: `DesktopHost_ResolvesWelcomeViewModel`.
- Rename + adjust: `MainWindowViewModel_InitializeAsync_WithMissingFile_ShowsDialog_LeavesWorkspaceNull` (replaces the previous `_Throws` test). To get a fake dialog into the host-built VM, **use `InMemoryRecentFilesStore` and `FakeDialogService` constructed manually rather than via DI** for the assertion-on-state tests; the DI test stays at "resolves the type". Pattern:
  ```csharp
  var dialog = new FakeDialogService();
  var recents = new InMemoryRecentFilesStore();
  var welcome = new WelcomeViewModel(dialog, recents);
  var store = new JsonWorkspaceStore(/* ... */);
  var overview = new OverviewViewModel();
  var log = NullLogger<MainWindowViewModel>.Instance;
  var vm = new MainWindowViewModel(store, overview, welcome, dialog, recents, log);
  ```

New file `Tests/Desktop/WelcomeViewModelTests.cs`:

1. `Initial_HasNoRecents_HasRecentsFalse` — fresh VM with empty store; assert `HasRecents` is false.
2. `RefreshAsync_PopulatesRecentsCollection` — push two items into the in-memory store, call `RefreshAsync`, assert `Recents.Count == 2` and order.
3. `OpenCommand_DialogReturnsPath_InvokesOpenHandler` — enqueue `@"C:\x.bws"` on `FakeDialogService`, set up a `Func<string, Task>` that records the path, execute `OpenCommand`, assert path captured.
4. `OpenCommand_DialogReturnsNull_DoesNotInvokeOpenHandler` — enqueue `null`, execute, assert handler not called.
5. `OpenRecentCommand_PassesPathToHandler` — execute with `@"C:\y.bws"` parameter, assert handler called with that path.
6. `IsNewWorkspaceEnabled_Locked_False` — assert `false` (regression guard against accidental flip).

New file `Tests/Desktop/MainWindowViewModelOpenTests.cs` (consolidates open-related tests away from `HostCompositionTests`):

7. `OpenWorkspaceAsync_ValidPath_LoadsWorkspaceAndPushesRecents` — given the sample bws, assert `Workspace != null` and `recents.GetAsync()` returns one entry pointing at the canonical path.
8. `OpenWorkspaceAsync_MissingFile_ShowsDialog_WorkspaceUnchanged` — start with `Workspace = null`, call with bogus path, assert `FakeDialogService.Calls` has one entry with title `"Workspace not found"`, assert `Workspace == null` still.
9. `OpenWorkspaceAsync_InvalidJson_ShowsDialog_WorkspaceUnchanged` — write garbage to a temp `.bws`, call, assert dialog title `"Workspace is not valid JSON"`.
10. `CloseCommand_WhenWorkspaceLoaded_ClearsWorkspace_RefreshesWelcome` — load sample, execute `CloseCommand`, assert `Workspace == null`, assert `Welcome.RefreshAsync` was called (observable: `Welcome.Recents` reflects post-load state).
11. `OpenWorkspaceAsync_SameFileTwice_RecentsHasOneEntry` — call twice with the same path; assert one entry (de-dup) with the most recent timestamp.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test "SQL Buldozer.sln"` — pre-step-03 81 + new (1 host smoke + 6 Welcome + 5 OpenWorkspace) − 1 (renamed) = **92 tests**, all green.
- **Manual smoke** (mandatory):
  - `dotnet run --project ParameterizationExtractor.Desktop` (no args) → window opens; Welcome view centred; "Recent: (empty)" visible (or populated if previous runs touched the real `%APPDATA%`); File menu present and functional. Click `Open workspace...` → file picker; pick `examples/sample.bws`. Window switches to the populated Overview tab. Title reads `SQL Buldozer — sample-clearing`. Status bar visible.
  - Close workspace via File → Close. Window switches back to Welcome view; the recent list now shows `sample.bws`. Click the recent item → workspace reloads.
  - Close window cleanly; exit code 0.

## Acceptance Criteria

- [ ] `Views/Welcome/{WelcomeView.xaml(.cs), WelcomeViewModel.cs}` exist; pairing follows the recipe.
- [ ] `Converters/InverseBoolToVisibilityConverter.cs` exists; registered in `App.xaml`'s resources.
- [ ] `MainWindow.xaml` content swaps based on `IsWorkspaceLoaded`: Welcome view when false, existing chrome when true. File menu is present at the top.
- [ ] `MainWindowViewModel` ctor takes the new four parameters in addition to the existing two (`IWorkspaceStore`, `OverviewViewModel`); new commands `OpenCommand`, `CloseCommand`, `ExitCommand`; new method `OpenWorkspaceAsync`; refactored `InitializeAsync` delegates to it.
- [ ] `OpenWorkspaceAsync` catches exactly `FileNotFoundException`, `JsonException`, `InvalidOperationException`; routes each through `IDialogService.ShowMessageAsync` with a category title; logs a warning. Other exceptions propagate.
- [ ] `WelcomeViewModel` has `Recents`, `HasRecents`, `IsNewWorkspaceEnabled = false`, `OpenCommand`, `OpenRecentCommand`, `RefreshAsync`. Open commands bubble paths via the bound `Func<string, Task>` handler.
- [ ] DI: `WelcomeViewModel` registered as Singleton; everything resolves (`DesktopHost_ResolvesWelcomeViewModel` passes).
- [ ] Welcome view opens via dialog correctly: enqueueing `null` on `FakeDialogService` does not call the handler; enqueueing a valid path does.
- [ ] `Close` clears `Workspace` and triggers `Welcome.RefreshAsync`.
- [ ] Recents are pushed after every successful load (CLI-arg + dialog + recent-click paths).
- [ ] No `Click=` event handlers, no logic in `.xaml.cs` files beyond `InitializeComponent()`. Verified by grep.
- [ ] No `MessageBox.Show`, no `IConfiguration[..]`, no `Console.WriteLine`, no `Dispatcher.Invoke`, no new `TODO`/`FIXME` introduced. Service-level access to `Application.Current` exists only in the `Exit` command and `DialogService.cs`, each with the recipe-canonical comment.
- [ ] `dotnet build` 0 errors; `dotnet test` 92/92 pass.
- [ ] Manual smoke (CLI-no-args → Welcome → Open dialog → workspace loaded → Close → recent visible → click recent → workspace reloads → exit 0) verified and noted in the checklist's `## Notes`.

## References

- ADR: [`adr/007-desktop-wpf-stack.md`](../../adr/007-desktop-wpf-stack.md), [`adr/008-desktop-ui-controls.md`](../../adr/008-desktop-ui-controls.md), [`adr/009-workspace-format.md`](../../adr/009-workspace-format.md)
- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § How to add a screen, § Service abstractions, § Dialogs / file pickers
- Mockup: [`docs/design/desktop-ui/01-startup.md`](../../docs/design/desktop-ui/01-startup.md)
- Pattern exemplars in code: [`Views/Overview/`](../../ParameterizationExtractor.Desktop/Views/Overview/), [`MainWindow.xaml`](../../ParameterizationExtractor.Desktop/MainWindow.xaml), [`MainWindowViewModel.cs`](../../ParameterizationExtractor.Desktop/MainWindowViewModel.cs)
- Depends on: [`01 — IDialogService`](./01-desktop-startup-and-open-workspace-dialog-service.md), [`02 — IRecentFilesStore`](./02-desktop-startup-and-open-workspace-recent-files-store.md).
