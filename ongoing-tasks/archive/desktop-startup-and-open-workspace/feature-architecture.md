# Desktop Startup and Open Workspace — Architecture Overview

> First feature to ship `IDialogService` per the recipe pre-spec. Replaces the empty-shell stub with a Welcome view (mockup [M1](../../docs/design/desktop-ui/01-startup.md)) and introduces a per-user Recent files store.

---

## 1. Pipeline / Integration

```
App.OnStartup (existing — touched only by the recents-push at the end)
  → DesktopHost.CreateApplicationBuilder(args).Build()                    (existing)
  → host.StartAsync()                                                      (existing)
  → resolve MainWindow + MainWindowViewModel                               (existing)
  → MainWindowViewModel.InitializeAsync(path)                              (existing)
       └── if path: IWorkspaceStore.LoadAsync(path) → Workspace            (existing)
       └── if !path: Workspace stays null                                  (existing)
  → if path && Workspace != null: _recentFiles.PushAsync(path)             (NEW)
  → set DataContext + window.Show()                                        (existing)

User sees:
  → Workspace == null  → WelcomeView (NEW; replaces empty-shell placeholder)
                         ├── [ New workspace... ]      (disabled until desktop-connection-management)
                         ├── [ Open workspace... ]     → IDialogService.OpenFileAsync → OpenWorkspaceAsync
                         └── Recent: list              → click → OpenWorkspaceAsync(path)
  → Workspace != null  → MainWindow toolbar + TabControl + status bar     (existing — unchanged)

OpenWorkspaceAsync(path) (NEW; on MainWindowViewModel):
  → _store.LoadAsync(path)
       ├── success: Workspace = ...; _recentFiles.PushAsync(path)
       └── failure: IDialogService.ShowMessageAsync("Failed to open workspace", ex.Message)
                    Workspace stays as it was
```

| Stage | What changes | Where |
|-------|--------------|-------|
| Composition root | `App.OnStartup` calls `_recentFiles.PushAsync(path)` after a successful command-line load | `App.xaml.cs` |
| Empty-state UI | `MainWindow.xaml` swaps content based on `IsWorkspaceLoaded` — Welcome view when false, existing chrome when true | `MainWindow.xaml` |
| Welcome screen | New `Views/Welcome/` pairing — first feature to consume `IDialogService` | `Views/Welcome/{WelcomeView.xaml(.cs), WelcomeViewModel.cs}` |
| Open workflow | `MainWindowViewModel.OpenWorkspaceAsync(string)` is the single entry point — Welcome's Open command, recent-item click, and File menu's Open all funnel into it | `MainWindowViewModel.cs` |
| File menu | Added `<Menu>` row to `MainWindow.xaml`; bound to `OpenCommand`, `CloseCommand`, recent items collection | `MainWindow.xaml`, `MainWindowViewModel.cs` |
| Dialogs | First WPF impl of the recipe pre-spec; lifetime Singleton | `Services/Dialogs/{IDialogService.cs, DialogService.cs}` |
| Recent files | Persisted JSON list; `%APPDATA%\SqlBuldozer\recent-files.json` | `Services/RecentFiles/{IRecentFilesStore.cs, JsonRecentFilesStore.cs, RecentFile.cs}` |

---

## 2. Component Diagram

```
+------------------------------------------------------+
|  ParameterizationExtractor.Desktop                   |
|                                                      |
|  App (OnStartup)                                     |
|     │                                                |
|     │ resolve                                        |
|     ↓                                                |
|  +--------------------+      +---------------------+ |
|  | MainWindow         |◄────►| MainWindowViewModel | |
|  | (MetroWindow)      |      |                     | |
|  | + Menu (File…)     |      |  Workspace?         | |
|  +--------------------+      |  Welcome (child VM) | |
|        ▲       │             |  Overview (child VM)| |
|        │       │             |  RecentFiles[]      | |
|        │       ↓             |  OpenCommand        | |
|        │   ContentControl    |  CloseCommand       | |
|        │   (visibility on    |  OpenWorkspaceAsync | |
|        │    IsWorkspaceLoaded)+----+----------------+ |
|        │       │                  │                |
|   Workspace ≠ null               ▼                |
|        │       │             +---------------------+ |
|        │       │             | IWorkspaceStore     | |
|        │       │             | (existing)          | |
|        │       │             +---------------------+ |
|        │       │             +---------------------+ |
|        │       │             | IDialogService      | |
|        │       │             | (NEW — Singleton)   | |
|        │       │             +---------------------+ |
|        │       │             +---------------------+ |
|        │       │             | IRecentFilesStore   | |
|        │       │             | (NEW — Singleton)   | |
|        │       │             +---------------------+ |
|        │       │                                     |
|   ┌────┴────┐  └─► WelcomeView (NEW UserControl)   |
|   │         │     DataContext = Welcome             |
|   ▼         ▼                                        |
| Toolbar  TabControl  (existing chrome)              |
|         (5 tabs)                                     |
+------------------------------------------------------+

%APPDATA%\SqlBuldozer\recent-files.json   (NEW; created on first push)
```

No new project; no new project references. Desktop still references `Common` and `Logic` only.

---

## 3. Data Flow

### 3.1 Inbound

- **Command-line arg** (existing): `e.Args.FirstOrDefault()` → `MainWindowViewModel.InitializeAsync(path)` (unchanged in this feature; we only **add** a recents-push when it succeeds).
- **Open dialog** (new): user clicks `[ Open workspace… ]` → `IDialogService.OpenFileAsync("Open workspace", "SQL Buldozer workspace (*.bws)|*.bws|All files (*.*)|*.*")` returns `string?` → if non-null, `OpenWorkspaceAsync(path)`.
- **Recent list click** (new): item bound `Command="{Binding DataContext.OpenCommand, RelativeSource=…}" CommandParameter="{Binding Path}"` → `OpenWorkspaceAsync(path)`.
- **File menu** (new): `File → Open workspace…` reuses `OpenCommand`; `File → Recent` is a dynamically-populated submenu sourced from `RecentFiles` collection.

### 3.2 Processing

```
OpenWorkspaceAsync(path):

  try
    var ws = await _store.LoadAsync(path)
    Workspace = ws                       // [ObservableProperty] setter → OnWorkspaceChanged
    await _recentFiles.PushAsync(path)
    Welcome.Show(_recentFiles.GetAsync())  // refresh recent list (or VM observes the store)
  catch (FileNotFoundException ex)
    await _dialog.ShowMessageAsync("Workspace not found", ex.Message)
  catch (JsonException ex)
    await _dialog.ShowMessageAsync("Workspace is not valid JSON", ex.Message)
  catch (InvalidOperationException ex)   // store throws this for unknown $version / bad auth
    await _dialog.ShowMessageAsync("Workspace cannot be opened", ex.Message)
```

The catch list is **closed** — only the three exception types `JsonWorkspaceStore.LoadAsync` documents. Any other exception escapes (per the .NET tripwire: no swallowing exceptions).

### 3.3 Outbound (persistence)

- **Recent files JSON** — `JsonRecentFilesStore.PushAsync(path)`:
  1. Loads current list from disk (empty if file absent or unreadable — see decision below).
  2. Removes any existing entry for the same path (case-insensitive on Windows).
  3. Prepends the new entry with `LastOpenedUtc = DateTime.UtcNow`.
  4. Trims to 5 entries.
  5. Writes back atomically via temp-file + `File.Move(temp, target, overwrite: true)`.

  File shape:
  ```json
  {
    "items": [
      { "path": "C:\\Workspaces\\patient-export.bws", "lastOpenedUtc": "2026-05-02T14:32:11Z" },
      { "path": "C:\\Workspaces\\employee-clearing.bws", "lastOpenedUtc": "2026-05-01T08:14:02Z" }
    ]
  }
  ```

  Uses the engine's shared `JsonOptions.Default` (camelCase, comments-skip, trailing-commas) — same primitive `IWorkspaceStore` already uses.

**Read-error policy.** If the file exists but is unreadable / invalid JSON, the store treats it as empty and **silently overwrites on the next write**. Rationale: this is a per-user UX cache, not authoritative data. The .NET tripwire on swallowing exceptions applies to business code; recents is a true UI cache. We log a warning via `ILogger<JsonRecentFilesStore>` so the situation is observable.

---

## 4. Data Stores

| Store | Technology | Purpose | Owner |
|-------|------------|---------|-------|
| `recent-files.json` | Local JSON file at `%APPDATA%\SqlBuldozer\recent-files.json` | Per-user list of recently-opened workspace paths (max 5) | `JsonRecentFilesStore` (Desktop) |

Path resolution: `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` + `\SqlBuldozer\recent-files.json`. Directory is created on first write.

For tests, the store accepts an explicit path via constructor (`internal JsonRecentFilesStore(string filePath, ILogger<JsonRecentFilesStore> log)`) — DI default uses the `%APPDATA%` location; tests inject a temp path. Mirrors the pattern already in `JsonWorkspaceStore`'s test surface.

No SQL Server, no engine connection. The Welcome / open / recents flow is entirely local.

---

## 5. Service abstractions — first impls

| Abstraction | Status before | Status after | Lifetime | Impl |
|---|---|---|---|---|
| `IDialogService` | pre-spec only (recipe) | **Implemented** | Singleton | `Services/Dialogs/DialogService.cs` (MahApps `MetroDialogManager` + `Microsoft.Win32.OpenFileDialog` / `SaveFileDialog`) |
| `IRecentFilesStore` | did not exist | **Implemented** | Singleton | `Services/RecentFiles/JsonRecentFilesStore.cs` |
| `IUiDispatcher` | pre-spec only (recipe) | still pre-spec | — | first impl ships with `desktop-seed-tab` |

Test fakes (`Tests/Desktop/Fakes/`):

- **`FakeDialogService`** — queue-driven per recipe spec. `EnqueueOpenFileResponse(string?)`, `EnqueueConfirmResponse(bool)`, etc. Records every call as a `(method, title, message)` tuple so tests can assert "the user was prompted with title=X".
- **`InMemoryRecentFilesStore`** — `IRecentFilesStore` impl backed by `List<RecentFile>`; lets tests skip the file-system round-trip when not under test.

`JsonRecentFilesStoreTests` does exercise the real on-disk path (using a `Path.GetTempFileName()`-derived path) so the persistence round-trip is covered.

---

## 6. View ↔ ViewModel pairing pattern (continued)

`Views/Welcome/` is the second pair to follow the pattern established by `Views/Overview/` (per [`docs/methodology/wpf-desktop.md` § How to add a screen](../../docs/methodology/wpf-desktop.md)):

```
Views/Welcome/
  ├── WelcomeView.xaml            # UserControl, x:ClassModifier="internal"
  ├── WelcomeView.xaml.cs         # only InitializeComponent()
  └── WelcomeViewModel.cs         # internal sealed partial class : ObservableObject
```

`WelcomeViewModel` shape:

| Member | Type | Source |
|---|---|---|
| `Recents` | `ObservableCollection<RecentFileItem>` | refreshed from `IRecentFilesStore.GetAsync()` |
| `HasRecents` | computed `bool` | `Recents.Count > 0` (drives "(empty)" vs. list) |
| `OpenCommand` | `IAsyncRelayCommand` | calls `IDialogService.OpenFileAsync` then bubbles the chosen path to the parent |
| `OpenRecentCommand` | `IAsyncRelayCommand<string>` | bubbles a path to the parent |
| `RefreshAsync()` | `Task` method | reloads `Recents` from the store |

Welcome's commands don't load the workspace themselves — they raise an event / call back into a parent-supplied delegate that points at `MainWindowViewModel.OpenWorkspaceAsync(string)`. **Decision:** the simplest mechanism is a constructor-injected `Func<string, Task>` "open handler" that `MainWindowViewModel` provides at construction. This keeps the cross-VM call explicit and avoids `IMessenger` for a single-route signal. We re-evaluate if a third VM ever needs the same signal.

---

## 7. State model

`MainWindowViewModel` gets these new members on top of what `desktop-shell` left:

| Member | Type | Behaviour |
|---|---|---|
| `Welcome` | `WelcomeViewModel` (Singleton, ctor-injected) | refreshed when `Workspace` becomes null |
| `IsWorkspaceLoaded` | computed `bool` | `Workspace is not null` — drives the visibility swap in `MainWindow.xaml` |
| `OpenCommand` | `IAsyncRelayCommand` | calls `IDialogService.OpenFileAsync` → `OpenWorkspaceAsync(path)` |
| `CloseCommand` | `IRelayCommand` | sets `Workspace = null`; refreshes `Welcome` |
| `OpenWorkspaceAsync(string path)` | `Task` | shared entry point — used by `OpenCommand`, recent-item activation, and (already-existing) `InitializeAsync` |

`InitializeAsync` is refactored to delegate to `OpenWorkspaceAsync(path)` for non-null paths so the open + recents-push behaviour is unified.

`OnWorkspaceChanged(WorkspaceModel?)` (already exists from `desktop-shell`) gains a call to `Welcome.RefreshAsync()` when the new value is null, so closing a workspace returns the user to a fresh recent list.

---

## 8. Threading & Initialization

- `App.OnStartup` is `async void` (allowed by tripwire) and remains the single dispatcher entry.
- `OpenWorkspaceAsync` runs on the dispatcher; `_store.LoadAsync` is fast (small JSON file) so we don't introduce `IUiDispatcher` speculatively. The recipe's implementation-timing table still has `IUiDispatcher` arriving with `desktop-seed-tab`.
- `JsonRecentFilesStore` IO uses `await using var stream = File.Open(...)` + async serialization via `JsonSerializer.SerializeAsync` / `DeserializeAsync` so it composes with `OpenCommand`'s async path.

---

## 9. Extension Points

| Seam | After this feature | Next consumer |
|---|---|---|
| `MainWindowViewModel.Welcome` (child VM) | exists | replaced/augmented by future welcome variants if needed (currently: stable) |
| `IDialogService` | first impl | `desktop-connection-management` (Test connection error popup), `desktop-dry-run-and-execute` (confirm before execute), Save flow |
| `IRecentFilesStore` | first impl | extends to "recent connections" if desired (probably not — connection is workspace-embedded); revisit only with evidence |
| `MainWindowViewModel.OpenWorkspaceAsync` | exists as the single entry | future drag-drop handler will call it; future "reopen last on launch" setting will call it |

---

## 10. Risks & Open Questions

- **Path canonicalisation in recents.** Two paths pointing at the same file (e.g. `C:\foo\..\foo\x.bws` vs. `C:\foo\x.bws`) would create duplicate entries. Decision: normalise via `Path.GetFullPath(path)` before push and compare. UNC paths use the same primitive.
- **Stale recents (file deleted / moved).** When the user clicks a recent that no longer exists, `JsonWorkspaceStore.LoadAsync` throws `FileNotFoundException` — caught by `OpenWorkspaceAsync`'s standard handler and surfaced via `IDialogService.ShowMessageAsync`. Decision: **do not** auto-prune on read; the user's correction (e.g. mounting a network share) shouldn't lose the entry. We could add `[ Remove from list ]` later if pain emerges.
- **WelcomeViewModel ownership of Open.** Pattern picked: `WelcomeViewModel` invokes a `Func<string, Task>` "open handler" supplied by `MainWindowViewModel`. Alternatives considered: (a) inject `MainWindowViewModel` into `WelcomeViewModel` (circular smell), (b) `IMessenger` (overkill for one route), (c) raise an event on `WelcomeViewModel` that `MainWindowViewModel` subscribes to (cycles with `INotifyPropertyChanged`-flavoured plumbing). The handler-`Func` is the cleanest.
- **`%APPDATA%` test isolation.** Tests must not write to the real `%APPDATA%`. Solution: all `JsonRecentFilesStore` tests construct the store with an explicit temp path; the DI registration uses a small `RecentFilesPathProvider` (or a static helper) that resolves the production path. Tests bypass DI for store-direct tests; integration tests inject `InMemoryRecentFilesStore`.
- **Accessibility / keyboard nav of the Welcome screen.** Buttons + list — default WPF tab navigation should suffice. No special handling planned.

---

## 11. Security & Isolation

- **No new trust boundaries.** Local file IO + dialogs only.
- **Workspace path validation.** Open dialog returns absolute paths under user control; `JsonWorkspaceStore` already validates `$version` and `auth`. We do not additionally sanitise the path beyond `Path.GetFullPath` for the recents key (no path traversal concern since the file is opened by `JsonWorkspaceStore` regardless of its location — the operator chose it).
- **No remote IO, no engine invocation, no SQL Server connection** in this feature. (Source connection editing lands with `desktop-connection-management`.)

---

## 12. What This Feature Does *Not* Build

- No New workspace dialog (M2).
- No drag-drop, no file association, no installer.
- No keyboard shortcuts (`Ctrl+O`, `Ctrl+W`, etc.) — defer until shortcut layout is settled with all menus.
- No per-recent context menu ("Remove from list", "Pin", "Open file location").
- No "reopen last workspace on launch" setting.
- No engine invocation, no SQL connection, no metadata fetch.
- No theme switching, no app icon, no installer.
