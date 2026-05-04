# Desktop Startup and Open Workspace

## Goal

Replace the empty-shell stub from [`desktop-shell`](./archive/desktop-shell-checklist.md) with a real Welcome view (mockup [M1](../docs/design/desktop-ui/01-startup.md)) shown when no workspace is loaded. Wire the **Open workspace…** flow through `IDialogService` (first feature to ship the recipe pre-spec) and a per-user **Recent files** list backed by `%APPDATA%\SqlBuldozer\recent-files.json`. The app stops being "raw" — operators can launch it, see what's there, and open a workspace from the UI rather than the command line.

## Scope

- **In scope:**
  - **`IDialogService` + WPF impl** — `Services/Dialogs/{IDialogService.cs, DialogService.cs}` per the recipe pre-spec ([`docs/methodology/wpf-desktop.md` § Service abstractions](../docs/methodology/wpf-desktop.md)). MahApps `MetroDialogManager` for confirms / messages; `Microsoft.Win32.OpenFileDialog` / `SaveFileDialog` for file pickers. Test fake at `Tests/Desktop/Fakes/FakeDialogService.cs` (queue-driven per recipe).
  - **`IRecentFilesStore` + JSON impl** — `Services/RecentFiles/{IRecentFilesStore.cs, JsonRecentFilesStore.cs}`. Reads/writes `%APPDATA%\SqlBuldozer\recent-files.json`. Capped at **5** entries (most-recent first), de-duplicated by full path (case-insensitive on Windows). API: `Task<IReadOnlyList<RecentFile>> GetAsync()`, `Task PushAsync(string path)`, `Task ClearAsync()`. `RecentFile` carries `Path`, `LastOpenedUtc`. Test fake at `Tests/Desktop/Fakes/InMemoryRecentFilesStore.cs`.
  - **`Views/Welcome/{WelcomeView.xaml(.cs), WelcomeViewModel.cs}`** — mockup M1: centred "No workspace open" panel with `[ New workspace... ]` (disabled — tooltip "Available with desktop-connection-management"), `[ Open workspace... ]` (calls `IDialogService.OpenFileAsync` + delegates load to parent), and a `Recent:` list (clickable items; "(empty)" placeholder when none).
  - **`MainWindow.xaml` content swap** — when `Workspace == null`, show `<welcome:WelcomeView DataContext="{Binding Welcome}"/>`; otherwise show the existing toolbar / TabControl / status bar. Implementation via a `DataTrigger` or two `ContentControl`s with `Visibility` driven by `IsWorkspaceLoaded`. The `MainWindowViewModel` exposes a `Welcome` child VM (Singleton) and a computed `bool IsWorkspaceLoaded => Workspace is not null`.
  - **Open workflow** — `MainWindowViewModel` gains an `OpenWorkspaceAsync(string path)` method (used by both Welcome's Open command and recent-item activation). On success: `_store.LoadAsync` → `Workspace = ...` → `_recentFiles.PushAsync(path)`. On failure (file missing, JSON invalid): `IDialogService.ShowMessageAsync("Failed to open workspace", message)`; `Workspace` stays as it was.
  - **Startup recents seeding** — `App.OnStartup` calls `await _recentFiles.PushAsync(path)` after a successful command-line `InitializeAsync(path)` so the next launch has it in the recent list.
  - **File menu** — added to `MainWindow.xaml` chrome via a `Menu` row: `File → New workspace... (disabled) / Open workspace... / Recent ▸ {dynamic} / Close workspace (disabled when none) / Exit`. Tripwire-friendly: menu items bind to `RelayCommand`s on `MainWindowViewModel`. No `Click=` handlers.
  - **VM tests** — `WelcomeViewModelTests` (initial state, recent-files binding, OpenCommand calls dialog → propagates path), `MainWindowViewModelTests` extensions (open success → workspace + recents updated, open failure → dialog shown + workspace unchanged), `JsonRecentFilesStoreTests` (cap-at-5, de-dup, persistence round-trip via a `tempPath` injected for tests), `FakeDialogService` & fake-store usage in tests.
  - **Cross-doc updates** — `docs/architecture/overview.md` § 2 (Desktop row gains "Welcome view + recent files + IDialogService"); `docs/methodology/wpf-desktop.md` § Service abstractions (mark `IDialogService` as **Implemented**, point at file paths) and § Dialogs (point at the now-real impl); `docs/roadmap.md` (move feature to Completed; promote `desktop-connection-management` to Proposed next).

- **Out of scope:**
  - **New workspace creation flow (mockup [M2](../docs/design/desktop-ui/02-new-workspace.md))** — full New dialog requires a connection editor + Test connection + metadata fetch + DPAPI password storage. All of that is `desktop-connection-management`'s territory. The `[ New workspace... ]` button in M1 is rendered disabled with a tooltip; clicking it does nothing.
  - **Drag-and-drop `.bws` files onto the welcome view** — deferred (mockup M1 open question, "Pass 3").
  - **`.bws` file association in Windows Explorer** — deferred (no installer story yet).
  - **Recent-list configurable size** — locked at 5 in this feature (mockup M1 open question; revisit if we ship more than two installs).
  - **Save / Save As / Close-workspace logic** — Save is owned by a future feature; Close is wired to clear `Workspace` only (no dirty-check prompt yet — there's nothing to dirty-mark in this feature).
  - **`IUiDispatcher`** — not needed yet. `OpenWorkspaceAsync` runs on the dispatcher; load is fast enough that we don't introduce dispatcher marshalling speculatively. `desktop-seed-tab` brings `IUiDispatcher`.
  - **Edit / View / Workspace / Tools / Help menus** from the M1 mockup chrome — only `File` lands here. Others are owned by their respective features.

- **Dependencies:**
  - [`desktop-skeleton`](./archive/desktop-skeleton-checklist.md) — Generic Host + DI + MahApps theming.
  - [`desktop-workspace-format`](./archive/desktop-workspace-format-checklist.md) — `IWorkspaceStore` + `WorkspaceModel` + `examples/sample.bws`.
  - [`desktop-shell`](./archive/desktop-shell-checklist.md) — `MainWindow.xaml` chrome, `MainWindowViewModel`, `Views/Overview/` reference pairing, sample-bws runtime asset.
  - Mockup [`docs/design/desktop-ui/01-startup.md`](../docs/design/desktop-ui/01-startup.md).
  - Recipe pre-spec [`docs/methodology/wpf-desktop.md` § Service abstractions](../docs/methodology/wpf-desktop.md).

## Architecture

See [feature-architecture.md](./desktop-startup-and-open-workspace/feature-architecture.md) for the empty-state vs. loaded composition, the `IDialogService` / `IRecentFilesStore` shapes, and the recents-store on-disk format.

## Steps

- [x] [01 — IDialogService + DialogService impl + test fake](./desktop-startup-and-open-workspace/01-desktop-startup-and-open-workspace-dialog-service.md)
- [x] [02 — IRecentFilesStore + JSON impl](./desktop-startup-and-open-workspace/02-desktop-startup-and-open-workspace-recent-files-store.md)
- [x] [03 — Welcome view + Open workflow + File menu](./desktop-startup-and-open-workspace/03-desktop-startup-and-open-workspace-welcome-and-open.md)
- [x] [04 — Cross-doc updates](./desktop-startup-and-open-workspace/04-desktop-startup-and-open-workspace-cross-docs.md)

## Notes

### 2026-05-03 — Step 01 (IDialogService + WPF impl + FakeDialogService) complete

**New files (Desktop):**

- `Services/Dialogs/IDialogService.cs` — verbatim from the recipe pre-spec (4 methods).
- `Services/Dialogs/DialogService.cs` — `internal sealed`. Ctor takes `IDialogCoordinator` + `ILogger<DialogService>`. `ShowMessageAsync` / `ConfirmAsync` resolve `Application.Current.MainWindow as MetroWindow` (with the recipe-canonical comment); when MainWindow isn't available yet they log a warning and return `Task.CompletedTask` / `false` — pragmatic guard for service-resolution-before-show paths. `OpenFileAsync` / `SaveFileAsync` use `Microsoft.Win32` dialogs synchronously wrapped in `Task.FromResult`.

**New files (Tests):**

- `Tests/Desktop/Fakes/FakeDialogService.cs` — queue-driven; throws `InvalidOperationException` on missing enqueued response (per spec). `DialogCall` record with `Method/Title/Message/Filter`.
- `Tests/Desktop/Fakes/FakeDialogServiceTests.cs` — 5 tests, all green.
- `Tests/Desktop/DialogServiceTests.cs` — 1 DI smoke test (resolves to `DialogService`).

**Modified files:**

- `ParameterizationExtractor.Desktop/DesktopHost.cs` — registered `IDialogCoordinator` (singleton via `DialogCoordinator.Instance`) + `IDialogService` (singleton).
- `Tests/CharacterisationTests/Harness/SqlNormalizer.cs` — **unrelated pre-existing flake**: regex required `\d{2}` for hour, but `DateTime.Now.ToString()` on uk-UA / ru-RU drops the leading zero (`03.05.2026 9:10:16`). Tightened to `\d{1,2}`. 10 characterisation tests were failing on the new day (2026-05-03) because of this — not introduced by this feature. Fix is one regex change + one new regression test (`Header_Style_Timestamp_With_Single_Digit_Hour_Is_Masked`).
- `Tests/CharacterisationTests/Harness/SqlNormalizerTests.cs` — added the regression test.

**Verification:**

- `dotnet build` — 0 errors (2 pre-existing warnings: docfx + NU1510 on `System.Text.Json` in Logic).
- `dotnet test` — **71/71 pass** (was 64; +6 step-01 tests + 1 normaliser regression test). Step file projected 70 — actual is 71 because of the unrelated flake fix.
- Tripwire grep over the 4 new files: 0 hits for `MessageBox.Show`, `IConfiguration[`, `Console.WriteLine`, `Thread.Sleep`, `.Result`, `.Wait()`, `Dispatcher.Invoke`, `TODO`, `FIXME`. Service-level access to `Application.Current` only in `DialogService.cs` (one occurrence of the canonical comment).

### 2026-05-03 — Step 02 (IRecentFilesStore + JsonRecentFilesStore) complete

**New files (Desktop):**

- `Services/RecentFiles/RecentFile.cs` — `internal sealed record RecentFile(string Path, DateTime LastOpenedUtc)`.
- `Services/RecentFiles/IRecentFilesStore.cs` — interface with `GetAsync` / `PushAsync` / `ClearAsync` and `public const int MaxItems = 5`.
- `Services/RecentFiles/JsonRecentFilesStore.cs` — production + test ctors. State record uses `[JsonPropertyName("items")]` to work with `JsonOptions.Default`'s camelCase. Atomic write via `{path}.tmp` + `File.Move(tmp, target, overwrite: true)`. Reuses `JsonOptions.Default` from the engine.

**New files (Tests):**

- `Tests/Desktop/Fakes/CapturingLogger.cs` — minimal in-memory `ILogger<T>` capturing `Entry(Level, Message, Exception?)`.
- `Tests/Desktop/Fakes/InMemoryRecentFilesStore.cs` — mirrors the JSON store's cap / de-dup / canonicalisation semantics for VM tests.
- `Tests/Desktop/RecentFiles/JsonRecentFilesStoreTests.cs` — 10 tests, all green.

**Modified files:**

- `ParameterizationExtractor.Desktop/DesktopHost.cs` — added singleton registration for `IRecentFilesStore` + `using` for `Services.RecentFiles`.
- `Tests/Desktop/HostCompositionTests.cs` — added `DesktopHost_ResolvesIRecentFilesStore`.

**Verification:**

- `dotnet build` — 0 errors.
- `dotnet test` — **82/82 pass** (was 71; +11 = 10 store tests + 1 DI smoke). Step file projected 81; actual is 82 because of step-01's normaliser regression test bumping the baseline.
- Recurring docfx incremental-cache flake hit again on first run; cleared `ParameterizationExtractor/obj/.cache` + `ParameterizationExtractor/Docs/_site` and re-ran cleanly. Same workaround as `desktop-skeleton` / `desktop-workspace-format` / `desktop-shell`.
- Tripwire grep: 0 hits for `MessageBox.Show`, `IConfiguration[`, `Console.WriteLine`, `Thread.Sleep`, `.Result`, `.Wait()`, `Dispatcher.Invoke`, `TODO`, `FIXME` in the new files.

### 2026-05-03 — Step 03 (Welcome view + Open workflow + File menu) complete

**New files (Desktop):**

- `Views/Welcome/{WelcomeView.xaml(.cs), WelcomeViewModel.cs}` — second canonical View ↔ ViewModel pair (after `Views/Overview/`). Welcome panel centred via `Border` with rounded corners; uses MahApps theme dynamic resources. New / Open buttons + dynamic recent list (with `(empty)` placeholder via `InverseBoolToVis`). VM uses `Bind(Func<string, Task>)` to receive the open handler from `MainWindowViewModel` (avoids circular DI as called out in feature-architecture § 10).
- `Converters/InverseBoolToVisibilityConverter.cs` — IValueConverter, registered as `InverseBoolToVis` in `App.xaml`.

**Modified files (Desktop):**

- `App.xaml` — added namespace import + `BoolToVis` and `InverseBoolToVis` resources at app scope. (Previously `BoolToVis` was declared inside `OverviewView.xaml`; moving it up means both views share one instance — matches the recipe's "loaded once" theme rule.)
- `MainWindowViewModel.cs` — full rewrite. Ctor now takes 6 params (`IWorkspaceStore`, `OverviewViewModel`, `WelcomeViewModel`, `IDialogService`, `IRecentFilesStore`, `ILogger<MainWindowViewModel>`). New `IsWorkspaceLoaded` (computed); new `OpenCommand` / `CloseCommand` / `ExitCommand`; new shared entry `OpenWorkspaceAsync(path)` with closed-set catch (`FileNotFoundException` / `JsonException` / `InvalidDataException` — corrected from spec's `InvalidOperationException`; `JsonWorkspaceStore` actually throws `InvalidDataException`). `InitializeAsync` delegates to `OpenWorkspaceAsync` for non-null paths. `OnWorkspaceChanged` propagates to both `Overview.Show` and (when null) `Welcome.RefreshAsync`. Service-level `Application.Current?.Shutdown()` in `ExitCommand` — has the recipe-canonical comment.
- `MainWindow.xaml` — added Menu row at top (`File → New (disabled) / Open / Recent ▸ {dynamic} / Close / Exit`). Body wraps the existing TabControl + a new `WelcomeView` in a single Grid; visibility on `IsWorkspaceLoaded` swaps between them. Toolbar + status bar collapsed when no workspace.
- `DesktopHost.cs` — added `WelcomeViewModel` Singleton.

**Modified files (Tests):**

- `Tests/Desktop/HostCompositionTests.cs` — slimmed to DI smokes only (5 tests). Added `DesktopHost_ResolvesWelcomeViewModel`. Removed the three `InitializeAsync_*` tests that asserted on `_Throws`/state — those now live in `MainWindowViewModelOpenTests.cs` with manual VM construction (real DialogService can't run without `Application.Current.MainWindow`, and we'd otherwise pollute the real `%APPDATA%` recents file).
- `Tests/Desktop/WelcomeViewModelTests.cs` (new) — 7 tests (initial empty state, Refresh, Open with path, Open with null, OpenRecent, IsNewWorkspaceEnabled regression guard, unbound-throws guard).
- `Tests/Desktop/MainWindowViewModelOpenTests.cs` (new) — 7 tests (load valid + recents pushed, missing-file dialog, invalid-JSON dialog, Close clears + welcome refresh, dedup on twice-load, InitializeAsync missing-file dialog, InitializeAsync null-path no-dialog).

**Verification:**

- `dotnet build` — 0 errors.
- `dotnet test` — **94/94 pass** (was 82; +12 net: +1 host smoke + 7 Welcome + 7 OpenWorkspace − 3 from HostCompositionTests). Step file projected 92; actual is 94 because of two extra guard tests (Bind-not-called, InitializeAsync null-path).
- Process smoke (programmatic):
  - `ParameterizationExtractor.Desktop.exe` (no args) → process alive after 2s → killed cleanly. Welcome view should be visible.
  - `ParameterizationExtractor.Desktop.exe Examples\sample.bws` → process alive after 2s → killed cleanly. Sample workspace path verified copied to output. Window should show Overview tab + populated chrome.
- **Pending**: full visual verification (click Open dialog, pick sample, see Overview load + recent populate, close → Welcome shows recent, click recent → reload, exit). Process-smoke proves no crashes; visual proof needs the user.
- Tripwire grep across new/modified files: 0 hits for `MessageBox.Show`, `IConfiguration[`, `Console.WriteLine`, `Thread.Sleep`, `.Result`, `.Wait()`, `Dispatcher.Invoke`, `TODO`, `FIXME`. `Application.Current` use is in `MainWindowViewModel.Exit` (canonical comment) and `DialogService.cs` (canonical comment) — nowhere else.
- `WelcomeViewModel.Bind` mechanism documented in source comment + checked by the unbound-throws regression test.
- Catch list deviation from spec (`InvalidOperationException` → `InvalidDataException`) noted because that's what `JsonWorkspaceStore.LoadAsync` actually throws for `$version` mismatch / bad auth (verified by reading `JsonWorkspaceStore.cs`). Spec was wrong.

### 2026-05-03 — Step 04 (cross-doc updates) complete

- `docs/architecture/overview.md` § 2 — Desktop row Purpose extended (Welcome view + File menu + recent-files path).
- `docs/architecture/overview.md` § 4 — added `recent-files.json` row.
- `docs/architecture/overview.md` § 5 — DI line mentions `IDialogService` + `IRecentFilesStore` Singletons.
- `docs/methodology/wpf-desktop.md` § Service abstractions — header status changed to `partially landed`; `IDialogService` subsection marked **implemented** with file pointers; implementation-timing table updated (gained Status column; `IDialogService` row points at `desktop-startup-and-open-workspace (landed)`).
- `docs/methodology/wpf-desktop.md` § Dialogs / file pickers — clarified `Application.Current` permission for the service-level call site.
- `docs/methodology/wpf-desktop.md` — new § "Recent files" between "Workspace store" and "Dialogs / file pickers".
- `docs/methodology/wpf-desktop.md` § How to add a screen — pointer expanded to list both `Views/Overview/` and `Views/Welcome/` as reference implementations.
- `docs/roadmap.md` — `desktop-startup-and-open-workspace` moved to ✅ Completed; `desktop-connection-management` promoted to Proposed next; description amended to note the deferred M2 dialog.
- `CLAUDE.md` SHA256 verified — `ac93dadc6c4bf047ffbb3b680fae09e5373627f7ebe542d000c87165f5b73d46` (unchanged from `desktop-shell`); equal to `.github/copilot-instructions.md`. No new tripwire surfaced.
- `dotnet test` 94/94 (sanity).

## Final summary

The `desktop-startup-and-open-workspace` feature is complete: 4/4 steps, 94/94 tests green.

**Cumulative changes:**

- **9 new source files** (Desktop): `Services/Dialogs/{IDialogService.cs, DialogService.cs}`, `Services/RecentFiles/{IRecentFilesStore.cs, JsonRecentFilesStore.cs, RecentFile.cs}`, `Views/Welcome/{WelcomeView.xaml, WelcomeView.xaml.cs, WelcomeViewModel.cs}`, `Converters/InverseBoolToVisibilityConverter.cs`.
- **5 modified files** (Desktop): `App.xaml` (resource registrations), `MainWindow.xaml` (Menu + content swap), `MainWindowViewModel.cs` (full rewrite — 6-param ctor + `OpenWorkspaceAsync` + `OpenCommand`/`CloseCommand`/`ExitCommand`), `DesktopHost.cs` (3 new singletons), `MainWindow.xaml.cs` unchanged.
- **5 new test files** (Tests/Desktop): `Fakes/{FakeDialogService.cs, FakeDialogServiceTests.cs, InMemoryRecentFilesStore.cs, CapturingLogger.cs}`, `RecentFiles/JsonRecentFilesStoreTests.cs`, `WelcomeViewModelTests.cs`, `MainWindowViewModelOpenTests.cs`, `DialogServiceTests.cs`.
- **1 modified test file** (Tests/Desktop/HostCompositionTests.cs): slimmed to DI smokes only; the workspace-load tests moved to `MainWindowViewModelOpenTests.cs` to avoid `%APPDATA%` pollution and DialogService-without-MainWindow gotchas.
- **1 unrelated bug-fix**: `SqlNormalizer` regex hardened from `\d{2}` to `\d{1,2}` for the hour digit (uk-UA / ru-RU drop the leading zero).
- **3 modified doc files**: `docs/architecture/overview.md`, `docs/methodology/wpf-desktop.md`, `docs/roadmap.md`.

**Net test delta:** 64 → 94 (+30: 6 dialog/fake + 11 recent-files + 12 welcome/main-vm + 1 normaliser regression).

**Pattern continued:** `Views/Welcome/` is the second canonical View ↔ ViewModel pair; the recipe now points at both Overview and Welcome.

**Service abstractions:** `IDialogService` is now landed; `IUiDispatcher` remains pre-spec, ships with `desktop-seed-tab`.

**Open follow-ups (deliberately deferred):**

- `desktop-connection-management` — first feature to use the freshly-landed `IDialogService` for Test-connection error popups; ships M2 New-workspace dialog and DPAPI password-at-rest.
- `desktop-seed-tab` — first feature to use `IUiDispatcher`.

**Pending action:** archive needs your nod (per `prompts/execution.md` § Completion). Pair to move: `ongoing-tasks/desktop-startup-and-open-workspace-checklist.md` + `ongoing-tasks/desktop-startup-and-open-workspace/` → `ongoing-tasks/archive/`.

**Visual smoke pending:** the process-smokes proved the app launches and stays alive in both modes (no-args → Welcome view; with sample.bws → Overview view), but full UI interaction (open dialog, pick sample, see Overview load + recent populate, Close → Welcome, click recent → reload) needs your eyeball verification.

Free-form log of decisions, blockers, links to PRs. Dated entries preferred (YYYY-MM-DD).
