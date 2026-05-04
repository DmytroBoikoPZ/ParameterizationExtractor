# WPF Desktop module recipe — SQL Buldozer

> Stack-specific recipe for `ParameterizationExtractor.Desktop`. Tripwires live in [`CLAUDE.md`](../../CLAUDE.md) — this file is the longer-form "how" for routine WPF work.

The desktop is a Windows-only operator-facing UI on top of the existing engine. It is **not** a service. Do not import service-shaped patterns (controllers, MediatR, EF, `IHttpClientFactory`, health-check endpoints, OpenAPI).

This recipe is the document the AI reads every time it touches the desktop project. Treat it as opinionated and concrete, not aspirational.

---

## Composition

- **Composition root:** `App.xaml.cs` `OnStartup`. Builds a Generic Host via `Microsoft.Extensions.Hosting`, registers desktop services, resolves `MainWindow` from the host's `IServiceProvider`, sets `MainWindow.DataContext = MainWindowViewModel` from DI, calls `Show()`.
- **Host shape:**
  ```csharp
  Host.CreateApplicationBuilder()
      .ConfigureAppConfiguration(cb => cb.AddJsonFile("appsettings.json", optional: false).AddEnvironmentVariables())
      .ConfigureLogging((ctx, lb) => lb.AddSerilog(/* read Serilog section from ctx.Configuration */))
      .ConfigureServices(services => services
          .AddSingleton<MainWindow>()
          .AddSingleton<MainWindowViewModel>()
          // future: .AddBuldozerEngine() symmetric with the CLI
      );
  ```
- **DI container is `Microsoft.Extensions.DependencyInjection`** ([ADR-006](../../adr/006-msdi-container.md)). Same primitives the CLI uses; only the host wrapper differs.
- **Lifecycle interlock:** in `App.OnExit`, call `host.StopAsync(...).GetAwaiter().GetResult()` and dispose. The host's hosted services run their `StopAsync` before the process exits. Never call `Environment.Exit` from a ViewModel — close the main window or call `Application.Current.Shutdown()`.
- **No service locator.** `IServiceProvider` is consulted exactly once (when `App.OnStartup` resolves `MainWindow`). Everything downstream uses constructor injection. Do not stash `IServiceProvider` on `App` and look things up later.
- **Constructor injection only.** No `[Import]`, no MEF, no plug-in-from-disk discovery. See [ADR-007](../../adr/007-desktop-wpf-stack.md).

---

## MVVM conventions

- **Library is `CommunityToolkit.Mvvm` 8.x** ([ADR-007](../../adr/007-desktop-wpf-stack.md)). Use the source generators: `[ObservableObject]` + `partial class`, `[ObservableProperty]` for fields, `[RelayCommand]` for command methods. Hand-rolled `INotifyPropertyChanged` is forbidden.
- **Naming and pairing:**
  - `Views/Xxx/XxxView.xaml` + `Views/Xxx/XxxView.xaml.cs` + `Views/Xxx/XxxViewModel.cs` (folder per screen, three files paired).
  - Top-level window stays at the project root: `MainWindow.xaml(.cs)` + `MainWindowViewModel.cs`.
  - Tab content views live under `Views/{Overview,Seed,Graph,Extras,Run}/`.
  - Reusable controls live under `Controls/Xxx/` as `XxxControl.xaml(.cs)`.
- **Visibility:** `internal sealed` for ViewModels and Views by default. The desktop project does not expose anything to other assemblies — `internal` is correct.
- **Hard rule — ViewModels never reference WPF types.** No `using System.Windows*`, no `PresentationCore`, no `Visibility`, no `Brush`, no `Window`, no `Dispatcher` directly. If you need to express UI state, use a domain enum + a value converter in the View. If you need to marshal back to the dispatcher, depend on `IUiDispatcher` (an interface owned by the desktop project).
- **Commands:** prefer `[RelayCommand]` on a method (`SaveCommand` is generated from `Save`). Async commands work via `[RelayCommand]` on `async Task`. Cancel tokens flow through `[RelayCommand(IncludeCancelCommand = true)]` when needed.
- **Messaging:** prefer constructor-injected services over `IMessenger` for cross-VM coordination. Reach for `IMessenger` only when the interaction is genuinely many-to-many or fire-and-forget.

---

## Code-behind discipline

- `.xaml.cs` files contain **only** `InitializeComponent()` plus framework overrides that demonstrably cannot live elsewhere (e.g. `OnRenderSizeChanged` for a custom-drawn control). No `Click=` event handlers. No `if/else`. No `MessageBox.Show`.
- For event-to-command glue use **`Microsoft.Xaml.Behaviors.Wpf`** (`<i:Interaction.Triggers>`) — pull this package only when needed.
- For input validation that's hard to express via bindings, write an attached behaviour, not code-behind.

---

## DI lifetimes

| Kind | Default lifetime | Reason |
|------|------------------|--------|
| Top-level windows (`MainWindow`) | Singleton | One instance for app lifetime. |
| Tab/screen ViewModels (`OverviewViewModel`, `SeedViewModel`, …) | Singleton | Tabs are persistent in the shell; their state must survive switching tabs. |
| Dialog ViewModels (one-shot) | Transient | Each dialog open is a fresh instance. |
| Services (`IWorkspaceStore`, `IDialogService`, `IUiDispatcher`, …) | Singleton | Stateless or app-scoped. |
| Per-operation services (extraction runner per run) | Transient | Hold per-run state. |

Override these only when the pattern doesn't fit, and call out the override in a one-line comment with the reason.

---

## Configuration

- `appsettings.json` is shipped in the desktop project (`<Content Include="appsettings.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>`). Note: the repo-wide `.gitignore` lists `ParameterizationExtractor/appsettings.json` and `Tests/appsettings.test.json` — the desktop's `appsettings.json` is **not** ignored and is committed.
- Layered with environment variables via `Microsoft.Extensions.Configuration`.
- Bound to POCOs in `App.xaml.cs` only. Tripwire: **no `IConfiguration["..."]` lookups in business code.** ViewModels and services receive their settings as POCOs through constructor injection.

---

## Logging

- **Serilog** with Console + File sinks, configured from the `Serilog` section of `appsettings.json`. Same shape as the CLI.
- Resolve via `ILogger<T>`. **Tripwire: no `Console.WriteLine`** anywhere in the desktop.
- Use **named placeholders**, never string interpolation:
  ```csharp
  logger.LogInformation("Workspace {Path} opened with {TableCount} tables", path, count);
  ```
- A future feature will add an in-app UI sink for the Run-tab log pane. Until then, the file sink is canonical.

---

## Theming / chrome (MahApps.Metro)

- **Theme dictionaries are loaded once in `App.xaml`** in the `<Application.Resources>` `<ResourceDictionary>` `<ResourceDictionary.MergedDictionaries>` block:
  ```xaml
  <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="pack://application:,,,/MahApps.Metro;component/Styles/Controls.xaml" />
      <ResourceDictionary Source="pack://application:,,,/MahApps.Metro;component/Styles/Fonts.xaml" />
      <ResourceDictionary Source="pack://application:,,,/MahApps.Metro;component/Styles/Themes/Light.Blue.xaml" />
  </ResourceDictionary.MergedDictionaries>
  ```
  Per-window theme loading is **forbidden** — duplicates resources and breaks restyling.
- Top-level windows derive from `mah:MetroWindow` (XAML namespace `xmlns:mah="http://metro.mahapps.com/winfx/xaml/controls"`).
- The slide-in inspector pane on the Graph tab uses MahApps `<mah:Flyout />`. Don't reach for AvalonDock or a hand-rolled overlay for that pattern.
- Dialogs go through `MahApps.Metro.Controls.Dialogs.MetroDialogManager` — abstracted behind `IDialogService` (see below).

---

## Code editor (AvalonEdit)

- `AvalonEdit` (`ICSharpCode.AvalonEdit`) is wrapped in a project-internal `Controls/SqlEditor/SqlEditor.xaml` UserControl with two dependency properties: `Text` (string, two-way) and `IsReadOnly` (bool). VMs bind to those.
- **Tripwire: ViewModels never touch `TextDocument` or `TextEditor`.** Don't expose AvalonEdit types from `SqlEditor`'s public surface. The wrapper isolates AvalonEdit so swapping it later is a single-control rewrite.
- SQL syntax highlighting: load `ICSharpCode.AvalonEdit.Highlighting.HighlightingManager` definitions in `App.xaml.cs` once at startup, register the SQL definition, reference it by name from `SqlEditor`.
- **Inferred state from typed SQL.** A VM hosting an `SqlEditor` can parse the typed SQL to drive sibling state (e.g. auto-fill a Root picker from the first `FROM <table>` clause). Use `Quipu.ParameterizationExtractor.Logic.Helpers.SeedQueryParser` for the canonical pattern — pure, stateless, regex-free token scanner; safe to call on every keystroke. Exemplar: `ScriptEditorViewModel.OnSeedQueryChanged` (Seed tab).
- **`WhereFilterEditor` for single-line WHERE clauses.** `Controls/WhereFilterEditor/WhereFilterEditorView.xaml(.cs)` wraps `SqlEditorView` in single-line mode with a `Text` two-way DP. Use it whenever a screen needs an inline WHERE-clause editor (Graph inspector, Extras tab per-row editor). Avoid re-inlining `<editor:SqlEditorView IsSingleLine="True"/>` — the wrapper exists to give the second consumer a stable contract.

---

## Workspace store

- The desktop reads / writes `.bws` workspace files through the `IWorkspaceStore` abstraction in `Services/Workspace/`. The WPF impl is `JsonWorkspaceStore` (System.Text.Json + the engine's shared `JsonOptions.Default`).
- ViewModels depend on `IWorkspaceStore`; never call `JsonSerializer.Deserialize` directly. The store handles `$version` validation, `auth` validation (must be `"windows"` or `"sql"`), and password-at-rest semantics (placeholder until `desktop-connection-management` lands).
- Schema and field-by-field correspondence with the engine's XML formats: [`docs/methodology/workspace-format.md`](./workspace-format.md) ([ADR-009](../../adr/009-workspace-format.md)).
- The store's `WorkspaceModel` embeds the engine's `Package` and `GlobalExtractConfiguration` POCOs directly — when handing them to the engine's pipeline, no translation is needed.

---

## Recent files

- The desktop persists a per-user list of recently-opened workspaces through `IRecentFilesStore` in `Services/RecentFiles/`. The default impl (`JsonRecentFilesStore`) writes to `%APPDATA%\SqlBuldozer\recent-files.json`, capped at `IRecentFilesStore.MaxItems` (= 5), de-duplicated by canonical path (case-insensitive on Windows).
- The store is intentionally **tolerant of disk errors** — recents is a UX cache, not authoritative state. Invalid JSON / IO errors are logged as warnings and treated as an empty list.
- `MainWindowViewModel.OpenWorkspaceAsync` is the single entry that pushes to recents — VMs and views never call `_recents.PushAsync` directly. The CLI-arg path goes through the same entry via `InitializeAsync` delegation.
- Tests use `Tests/Desktop/Fakes/InMemoryRecentFilesStore.cs` to avoid touching the real `%APPDATA%`.

---

## Password protection

The desktop never holds SQL passwords in plaintext on disk. Persisted passwords live in `WorkspaceSource.PasswordEncrypted` as base64-encoded DPAPI ciphertext (`DataProtectionScope.CurrentUser`); `IPasswordProtector` (`Services/Security/`) is the one-way seam VMs use. Plaintext exists only as a runtime field on `ConnectionEditorViewModel` / a transient pass-through in `MainWindowViewModel.EditConnectionAsync`.

**Tripwire: VMs and views never read `WorkspaceSource.PasswordEncrypted` directly — always go through `IPasswordProtector.Unprotect`.**

Decrypt failures (`CryptographicException` from a different user / machine; `FormatException` from non-base64) are caught at the boundary (`MainWindowViewModel.EditConnectionAsync`), logged at warning level, and surface as "no stored credential" — the Edit-connection flow is the recovery path. See [ADR-010](../../adr/010-desktop-password-at-rest.md). Tests use `Tests/Desktop/Fakes/InMemoryPasswordProtector.cs` (base64 round-trip; no real protection — DPAPI binds to the test runner's user account, which we don't want in test output).

---

## Database explorer

The Desktop reads tables and runs preview SELECTs through `IDatabaseExplorer` (`Logic/Schema/`) — never touches `SqlConnection` directly (mirrors the `IConnectionTester` pattern). `MSSqlDatabaseExplorer.PreviewQueryAsync` enforces the row cap by reading `maxRows + 1` (truncation flag) and stringifies all cells via `Convert.ToString(value, InvariantCulture)` — type-aware rendering is deferred. Failures are wrapped in `DatabaseExplorerException`; cancellation propagates as `OperationCanceledException`. Connection-string assembly for explorer calls goes through the shared `WorkspaceConnectionStringBuilder` (`Desktop/Services/Workspace/`).

---

## Graph host

The Desktop visualises the FK subgraph through `IGraphBuilder` (`Logic/Schema/`) — never touches `SqlConnection` directly. `MSSqlGraphBuilder.BuildAsync` BFS-walks the FK graph from a seed-table list (both directions traversed) using the same metadata query that powers `DependencyBuilder`, and returns a `ReachableGraph(Nodes, Edges)` record. Failures use the shared `DatabaseExplorerException`; cancellation propagates.

The render surface is `Controls/GraphHost/GraphHostView`, a thin `UserControl` wrapping `Microsoft.Msagl.WpfGraphControl.GraphViewer` (ADR-012; `AutomaticGraphLayout` 1.1.12 — community Msagl fork). **Tripwire — AGL isolation.** `AutomaticGraphLayout.*` and `Microsoft.Msagl.*` types live ONLY inside `Controls/GraphHost/`. ViewModels see only the engine's `ReachableGraph` record, the `GraphLayoutKind` enum, and `SelectedNodeId` (string). The `Viewer.MouseDown` ↔ `SelectedNodeId` bridge is the only place AGL types are visible — confined to `GraphHostView.xaml.cs`. Mirrors the AvalonEdit isolation in `Controls/SqlEditor/`.

---

## Connection management

The Desktop never opens `SqlConnection` directly (mirrors the .NET tripwire). Connection tests go through `IConnectionTester` (engine-side, in `ParameterizationExtractor.Logic/Connectivity/`); the result is `ConnectionTestResult { Success, TableCount, FkCount, ErrorMessage }`. The reusable `Controls/ConnectionEditor/` UserControl is the only place that builds connection strings (via `SqlConnectionStringBuilder` — exception to the "no `Microsoft.Data.SqlClient` outside Logic" tripwire, because the builder is parameter quoting and does not execute SQL; the call site carries the canonical comment).

The control hosts in `Dialogs/NewWorkspace/NewWorkspaceDialog` for both `New workspace` and `Edit connection` flows. Mode is set via `InitForNew()` / `InitForEdit(WorkspaceSource, plaintextPassword)`; the workspace-name + save-path rows collapse in edit mode. Confirm in New mode writes a skeleton `.bws` via `IWorkspaceStore.SaveAsync`; Confirm in Edit mode returns the updated `WorkspaceSource` to `MainWindowViewModel`, which replaces `Workspace` with `Workspace.WithSource(updated)` and saves. **Edit-connection is the first save-back call site on the desktop.** Save / Save-As as a general feature (with dirty-marker prompts) is still future work.

Cancellable Test: `[RelayCommand(IncludeCancelCommand = true)]` on `TestAsync(CancellationToken)` generates `TestCommand` + `TestCancelCommand`; the engine's `MSSQLConnectionTester` observes the token and propagates `OperationCanceledException` (which the VM catches and renders as `Cancelled` status). Inner connection timeout is 10s; cancellation is the outer channel.

---

## Dialogs / file pickers

- All UI dialogs go through `IDialogService` ([interface spec](#idialogservice--dialogs-and-file-pickers)). ViewModels depend on the interface; the WPF impl lives in `Services/Dialogs/DialogService.cs`. Tripwire: **no `MessageBox.Show` in VMs or services.**
- Confirms / message dialogs use MahApps `MetroDialogManager` (`IDialogCoordinator` injected as a singleton). The impl resolves `Application.Current.MainWindow as MetroWindow` for dialog ownership — service-level access to `Application.Current` is permitted with the canonical one-line comment.
- File pickers use `Microsoft.Win32.OpenFileDialog` / `SaveFileDialog`. The `IDialogService` interface returns the chosen path or `null`.
- File-extension filter for workspaces: `"SQL Buldozer workspace (*.bws)|*.bws|All files (*.*)|*.*"`.

---

## Threading

- ViewModels run on the dispatcher. Long-running work (engine invocation, file IO) goes through `Task.Run` and `await`. Continuation marshals back to the dispatcher automatically.
- **Tripwire: no `Dispatcher.Invoke` in VMs.** If you need explicit dispatcher access, depend on `IUiDispatcher` ([interface spec](#iuidispatcher--ui-thread-marshalling)). WPF impl lives in `Services/Threading/UiDispatcher.cs`.
- Mirrors the .NET tripwire: **no `Thread.Sleep`, `.Result`, `.Wait()`, `async void` outside event handlers.**
- Long-running operations expose a `CancellationToken` and observe it. The shell's main `CancellationTokenSource` ties to `App.OnExit`.

---

## Service abstractions — pre-spec

> **Status: landed.** All recipe pre-spec abstractions are implemented (`IDialogService` since `desktop-startup-and-open-workspace`; `IUiDispatcher` since `desktop-seed-tab`). Each abstraction ships with the first feature that genuinely uses it (see "Implementation timing" at the end of this section). The spec exists so the first implementer doesn't have to redesign the API and so the second feature using the same abstraction doesn't drift from the first.
>
> **Why pre-spec instead of pre-build:** building speculative infra is YAGNI on a one-dev project. Pinning the *shape* in the recipe keeps tripwires (`no MessageBox.Show in VMs`, `no Dispatcher.Invoke in VMs`) enforceable from day one without a feature whose only purpose is plumbing.

### `IUiDispatcher` — UI-thread marshalling

> **Status: implemented.** Lives at `Services/Threading/{IUiDispatcher.cs, UiDispatcher.cs}` since `desktop-seed-tab`. Test fake at `Tests/Desktop/Fakes/FakeUiDispatcher.cs` (runs everything inline). The WPF impl is safe to resolve in non-WPF test hosts (returns inline when `Application.Current` is null).

Lives in `Services/Threading/IUiDispatcher.cs`. Lifetime: **Singleton**. WPF impl wraps `Application.Current.Dispatcher`.

```csharp
internal interface IUiDispatcher
{
    /// True when the calling thread is the UI thread.
    bool CheckAccess();

    /// Marshal an action onto the UI thread. Awaits its completion.
    Task InvokeAsync(Action action);

    /// Marshal a function onto the UI thread. Awaits its result.
    Task<T> InvokeAsync<T>(Func<T> func);
}
```

VMs that mutate observable state from a background `Task.Run` continuation marshal back via `await _ui.InvokeAsync(...)`. If the continuation is already on the UI thread (typical for awaited `Task` chains in CTK.MVVM `[RelayCommand]`), `InvokeAsync` runs inline — no extra hop.

`CheckAccess()` exists for debug assertions, not for branching. Don't write `if (!CheckAccess()) InvokeAsync(...)` — `InvokeAsync` already handles the "already on UI thread" case.

### `IDialogService` — dialogs and file pickers

> **Status: implemented.** Lives at `Services/Dialogs/{IDialogService.cs, DialogService.cs}` since `desktop-startup-and-open-workspace`. Test fake at `Tests/Desktop/Fakes/FakeDialogService.cs`.

Lives in `Services/Dialogs/IDialogService.cs`. Lifetime: **Singleton**. WPF impl uses MahApps `MetroDialogManager` for confirms/messages and `Microsoft.Win32.OpenFileDialog` / `SaveFileDialog` for file pickers. The impl resolves `Application.Current.MainWindow as MetroWindow` for dialog ownership — service-level access to `Application.Current` is allowed (the tripwire forbids it in VMs); the canonical comment marks the call site.

```csharp
internal interface IDialogService
{
    /// Info popup. Returns when the user dismisses.
    Task ShowMessageAsync(string title, string message);

    /// Two-button confirm. Returns true for OK/Yes, false for Cancel/No.
    Task<bool> ConfirmAsync(string title, string message);

    /// OS file-open picker. Returns chosen path or null on cancel.
    /// `filter` uses Win32 syntax: "Workspace (*.bws)|*.bws|All (*.*)|*.*"
    Task<string?> OpenFileAsync(string title, string filter);

    /// OS save-as picker. Returns chosen path or null on cancel.
    Task<string?> SaveFileAsync(string title, string filter, string? defaultName = null);

    /// Modal New-workspace dialog. Returns the path of the freshly-created `.bws`, or null on cancel.
    Task<NewWorkspaceResult?> ShowNewWorkspaceDialogAsync();

    /// Modal Edit-connection dialog (re-uses the New-workspace UI in edit mode).
    /// Returns the updated WorkspaceSource, or null on cancel.
    Task<WorkspaceSource?> ShowEditConnectionDialogAsync(WorkspaceSource current, string? plaintextPassword);
}
```

The interface intentionally does **not** include three-way prompts (Yes/No/Cancel), progress dialogs, or input-text dialogs. Add a method (or a new method overload) when the first feature genuinely needs one — and update this spec in the same commit. Don't fork into per-feature dialog interfaces.

Typed dialog methods (`ShowNewWorkspaceDialogAsync`, `ShowEditConnectionDialogAsync` — landed by `desktop-connection-management`) follow the same pattern: `IDialogService` returns the typed result; the impl constructs the `MetroWindow` via a `Func<NewWorkspaceDialog>` DI factory and wraps `ShowDialog()` in `Task.FromResult`. The factory delegate keeps the service-locator pattern off the table.

### Test-fake convention

Both abstractions ship with a fake under `Tests/Desktop/Fakes/`, alongside the real WPF impl:

- `FakeUiDispatcher` — runs every `InvokeAsync` inline on the calling thread; `CheckAccess()` always returns `true`. Tests never deal with a real `Dispatcher`.
- `FakeDialogService` — queue-driven: `EnqueueConfirmResponse(true)`, `EnqueueOpenFileResponse(@"C:\path.bws")`, etc. Records every call so tests can assert "the user was prompted with title=X, message=Y".

VM tests inject the fakes through DI (test-builder helper) rather than by `new`-ing the VM directly. The fakes live under `Tests/Desktop/Fakes/` so multiple VM-test files share one impl.

### Implementation timing

| Abstraction | First feature that needs it | Status | Why |
|---|---|---|---|
| `IDialogService` | `desktop-startup-and-open-workspace` | **landed** | First file picker / confirm. Extended by `desktop-connection-management` with typed dialog methods. |
| `IConnectionTester` | `desktop-connection-management` | **landed** (engine-side) | Connection test for the new-workspace + edit-connection flows. Lives in `Logic/Connectivity/` so the Desktop never touches `SqlConnection` directly. |
| `IPasswordProtector` | `desktop-connection-management` | **landed** | DPAPI password-at-rest seam (ADR-010). |
| `IUiDispatcher` | `desktop-seed-tab` | **landed** | First async query whose result mutates observable preview rows. |
| `IDatabaseExplorer` | `desktop-seed-tab` | **landed** (engine-side) | First table-list + preview-query consumer in the Seed tab. Lives in `Logic/Schema/` so the Desktop never touches `SqlConnection` directly. |
| `IGraphBuilder` | `desktop-graph-viz` | **landed** (engine-side) | First FK-subgraph builder consumer in the Graph tab. Lives in `Logic/Schema/` so the Desktop never touches `SqlConnection` directly. Uses `IObjectMetaDataProvider`'s FK metadata query under the hood. |
| Click-dispatch pattern (no new abstraction) | `desktop-graph-tab` | **landed** | `SelectedNodeId` + `RightClickedNodeId` DPs on the host; VM owns the per-state dispatch in `OnSelectedNodeIdChanged`. See "Click-dispatch on graph-style controls" below. |
| Cross-VM filtered collection (no new abstraction) | `desktop-extras-tab` | **landed** | `ExtrasViewModel` subscribes to `GraphViewModel.AllNodes.CollectionChanged` and rebuilds its filtered standalone-tables list on each event. See "Cross-VM filtered collections" below. |

When the host feature scaffolds, its step file lists the interface + WPF impl + DI registration + test fake under `What to Build`. The spec above is what the implementer codes against, verbatim.

---

## Testing

- **Framework:** NUnit (the existing `Tests/` project) + **FluentAssertions** ([ADR-008](../../adr/008-desktop-ui-controls.md)).
- VM tests live under `Tests/Desktop/`. Cross-stack tests are encouraged when behaviour spans the desktop and the engine.
- **WPF UI is not unit-tested.** Tests instantiate ViewModels and services, never `Window` or `UserControl`. The walking skeleton's smoke test demonstrates the boundary: it builds the desktop's host and resolves `MainWindowViewModel` without instantiating any `Window`.
- A static helper `DesktopHost.CreateHostBuilder()` (in the desktop project) exposes the host configuration so tests can build and inspect it without going through `App.OnStartup`.

---

## How to add a screen

> **Reference implementations:** `Views/Overview/` (read-only summary; landed by `desktop-shell`), `Views/Welcome/` (services-driven empty-state view; landed by `desktop-startup-and-open-workspace`), `Controls/ConnectionEditor/` (reusable control hosted in dialogs; landed by `desktop-connection-management`), `Views/Seed/` + `Controls/{SqlEditor, TablePicker, RowsPreviewGrid}/` (multi-script editor + foundational reusable controls; landed by `desktop-seed-tab`), and `Views/Graph/` + `Controls/GraphHost/` (read-only FK-subgraph view + third-party graph-library wrapper; landed by `desktop-graph-viz`) are the canonical pairings every later screen copies. Read them first, then follow the steps below.

1. **Create the folder.** `ParameterizationExtractor.Desktop/Views/Xxx/`.
2. **Add the View.** `XxxView.xaml` (a `UserControl` rooted at the project's MahApps theme; XAML namespace declarations matching the rest of the project) + `XxxView.xaml.cs` (only `InitializeComponent()`).
3. **Add the ViewModel.** `XxxViewModel.cs` — `internal sealed partial class XxxViewModel : ObservableObject`. Constructor takes services from DI. No WPF types.
4. **Register the VM in DI.** Add `services.AddSingleton<XxxViewModel>()` (or Transient — see lifetimes table) in `App.xaml.cs`'s `ConfigureServices` block.
5. **Compose into the parent View.** If the screen is a tab, the parent is a `TabHost`-style control on `MainWindow`; bind the tab's `Content` to the resolved VM. If it's a dialog, surface it through `IDialogService`. If it's a Flyout (slide-in pane), declare it under `mah:MetroWindow.Flyouts` and bind its `IsOpen`.
6. **Write a VM test.** `Tests/Desktop/XxxViewModelTests.cs`. Cover the screen's primary behaviours; do not instantiate the View.

If the screen needs a new reusable control (e.g. a custom picker), put it under `Controls/Xxx/` and register on the View, not on the VM.

### Cross-VM filtered collections

A Singleton VM can derive a **filtered view** of another Singleton VM's `ObservableCollection`. Pattern landed by `desktop-extras-tab`:

- The downstream VM (`ExtrasViewModel`) takes the upstream VM (`GraphViewModel`) as a constructor dependency and subscribes to its `INotifyCollectionChanged` event in the ctor.
- The downstream VM holds its own `ObservableCollection` of derived rows; rebuilds it on each upstream collection change.
- The recompute marshals to the UI thread via `IUiDispatcher.InvokeAsync` — never assume the upstream event fired on the UI thread.
- **Graceful fallback** when the upstream collection is empty (not yet hydrated): show all candidates rather than an empty list. Better than confusing emptiness.

Exemplar: `ExtrasViewModel.OnGraphAllNodesChanged` + `RecomputeStandalone`.

### Click-dispatch on graph-style controls

Hosts that render selectable items (graph nodes, tree nodes, list items) often need *contextual* single-click behaviour: the same click does different things depending on item state. Pattern landed by `desktop-graph-tab`:

- The host control exposes a single `SelectedNodeId` (string?) DP — two-way, raised on left-click.
- A separate `RightClickedNodeId` DP is raised on right-click, so a `<ContextMenu>` resource can react without conflating with selection.
- The dispatch logic lives entirely in the VM's `partial void OnSelectedNodeIdChanged(string?)`. Each branch maps a state to an action — e.g. `Pending → ExpandNodeCommand`, `Configured/Excluded → InspectedNode = node`.
- The host stays state-agnostic; the VM owns "what does a click mean here?" — making it easy to add new dispatch branches (or test them) without touching XAML.

Exemplar: `GraphViewModel.OnSelectedNodeIdChanged` + `GraphHostView.OnViewerMouseDown`.

---

## Project shape — quick reference

| Concern | Setting |
|---|---|
| TargetFramework | `net10.0-windows` |
| OutputType | `WinExe` |
| RootNamespace | `Quipu.ParameterizationExtractor.Desktop` |
| WPF flag | `<UseWPF>true</UseWPF>` |
| Nullable | `enable` |
| ImplicitUsings | `enable` |
| ProjectReferences | `ParameterizationExtractor.Common`, `ParameterizationExtractor.Logic` (no DSL, no DSL.Connector) |
| Build | `dotnet build "SQL Buldozer.sln"` |
| Test | `dotnet test "SQL Buldozer.sln"` |
| Run | `dotnet run --project ParameterizationExtractor.Desktop` |

---

## References

- [ADR-002 — Module layering is one-way](../../adr/002-module-layering.md)
- [ADR-005 — Freeze the F# DSL](../../adr/005-freeze-fsharp-dsl.md)
- [ADR-006 — DI container is `Microsoft.Extensions.DependencyInjection`](../../adr/006-msdi-container.md)
- [ADR-007 — Desktop UI stack — WPF on .NET 10 with CommunityToolkit.Mvvm and Generic Host](../../adr/007-desktop-wpf-stack.md)
- [ADR-008 — Desktop UI controls — MahApps.Metro chrome, AvalonEdit code editor, no docking lib initially](../../adr/008-desktop-ui-controls.md)
- Sibling recipes: [`dotnet-cli.md`](./dotnet-cli.md), [`fsharp-dsl.md`](./fsharp-dsl.md)
- Component inventory: [`docs/design/desktop-ui/components.md`](../design/desktop-ui/components.md)
