# Desktop Shell — Architecture Overview

> First feature to render real UI on top of `IWorkspaceStore`. Establishes the View ↔ ViewModel pairing pattern that subsequent UI features replicate.

---

## 1. Pipeline / Integration

```
App.OnStartup (existing)
  → DesktopHost.CreateApplicationBuilder(args).Build()                (existing)
  → host.StartAsync()                                                  (existing)
  → resolve MainWindow + MainWindowViewModel                           (existing — DI-based)
  → MainWindowViewModel.InitializeAsync(path)                          (NEW)
       └── if path: IWorkspaceStore.LoadAsync(path) → Workspace        (NEW)
       └── if !path: Workspace stays null (empty-shell mode)           (NEW)
  → set DataContext + window.Show()                                    (existing — but DataContext is now richer)

User sees:
  → Workspace == null  → empty-shell placeholder ("Pass a .bws path to load…")
  → Workspace != null  → tab strip + Overview tab content + chrome bound to workspace fields
```

| Stage | What changes | Where |
|-------|--------------|-------|
| Startup | `MainWindowViewModel` ctor signature gains `IWorkspaceStore`; `InitializeAsync` introduced | `MainWindowViewModel.cs` |
| Composition root | `App.OnStartup` reads `e.Args[0]` as workspace path; awaits `InitializeAsync` before `Show()` | `App.xaml.cs` |
| Window content | `<Grid />` replaced by toolbar + tab strip + status bar; tab content is mostly placeholder text except Overview | `MainWindow.xaml` |
| Tab content | Overview is a real `UserControl` + `ViewModel` pair | `Views/Overview/OverviewView.{xaml,xaml.cs}` + `OverviewViewModel.cs` |
| Sample asset | `examples/sample.bws` copied to Desktop output for `dotnet run -- examples/sample.bws` | `ParameterizationExtractor.Desktop.csproj` |

---

## 2. Component Diagram

```
+----------------------------------------------+
|  ParameterizationExtractor.Desktop           |
|                                              |
|  App (OnStartup) ──────────────────┐         |
|                                    │         |
|                       resolve / init↓        |
|                                    │         |
|                  +-----------------+-+       |
|                  |  MainWindow       |       |
|                  |  (MetroWindow)    |       |
|                  +---┬------┬--------+       |
|        DataContext   │      │  Content       |
|                      ↓      ↓                |
|             +-------------+ +----------------+
|             | MainWindow- | | TabControl   |↑
|             | ViewModel   | | (5 tabs)     ||
|             +------┬------+ +-+----+----+--+|
|                    │          │    ...    | |
|                    │          ↓ Overview  | |
|                    │     +---------------+| |
|                    │     | OverviewView  || |
|                    │     | UserControl   || |
|                    │     +-------┬-------+| |
|                    │ DataContext ↓        | |
|                    │     +---------------+| |
|                    │     | OverviewVM    || |
|                    │     +---------------+| |
|                    │             ↑        | |
|                    └─── owns ────┘        | |
|  IWorkspaceStore (DI Singleton)           | |
|                                           | |
+-------------------------------------------+ |
                                              |
+-----------------------------------------+   |
|  ParameterizationExtractor.Logic        |   |
|  WorkspaceModel embeds:                 |   |
|    - GlobalExtractConfiguration         |   |
|    - Package                            |   |
+-----------------------------------------+
```

No new project, no new project references. The Desktop project still references `Common` and `Logic` only.

---

## 3. View ↔ ViewModel pairing pattern (canonical)

This feature is the first to follow the pairing pattern locked in [`docs/methodology/wpf-desktop.md` § How to add a screen](../../docs/methodology/wpf-desktop.md). Subsequent UI features replicate:

```
Views/Overview/
  ├── OverviewView.xaml            # UserControl, x:ClassModifier="internal"
  ├── OverviewView.xaml.cs         # only InitializeComponent()
  └── OverviewViewModel.cs         # internal sealed partial class : ObservableObject
```

The View's `DataContext` is set by the parent (here: `MainWindow.xaml` binds the Overview tab `Content` to `Overview` property on `MainWindowViewModel`). The VM is registered as Singleton in DI and exposed by `MainWindowViewModel` as a child property.

**This is the template the desktop-seed-tab / desktop-graph-tab / desktop-extras-tab / desktop-run-tab features will copy.**

---

## 4. Threading & Initialization

- `App.OnStartup` is an `async void` framework override (allowed by the WPF tripwire).
- It awaits `MainWindowViewModel.InitializeAsync(path)` **before** calling `window.Show()`. So the window appears with content already populated — no async-update flicker, no need for `IUiDispatcher` in this feature.
- Future features that fetch metadata from SQL Server *will* need `IUiDispatcher` for marshalling result rows back to the UI thread; that abstraction is named in the recipe pre-spec and ships with `desktop-seed-tab` per the recipe's implementation-timing table.

---

## 5. State model

`MainWindowViewModel` properties:

| Property | Type | Source |
|----------|------|--------|
| `Workspace` | `WorkspaceModel?` | Loaded by `InitializeAsync` |
| `WindowTitle` | computed `string` | `"SQL Buldozer"` if no workspace, `"SQL Buldozer — {Name}{*?}"` otherwise |
| `IsDirty` | `bool` | Always `false` in this feature (no edits yet); slot exists so the title-bar binding works |
| `Overview` | `OverviewViewModel` | Singleton VM; its own properties recompute when `Workspace` changes |
| `LastSavedDisplay` | computed `string` | `"never"` until save logic lands |
| `ConnectionDisplay` | computed `string` | `"<no workspace>"` or `"{database} @ {server}"` |

`OverviewViewModel` properties:

| Property | Type | Source |
|----------|------|--------|
| `WorkspaceName` | `string` | `Workspace.Name` |
| `SourceSummary` | `string` | `"{database} @ {server}"` |
| `SeedSummary` | `string` | `"<seed not configured>"` until seed support lands; today: empty |
| `PathSummary` | `string` | `"{N} tables configured"` from `Workspace.Package.Scripts.SelectMany(...)` count |
| `ExtrasSummary` | `string` | always `"0 standalone tables · 0 scripts"` until those concepts exist on `WorkspaceModel`; placeholder |
| `LastDryRunSummary` | `string` | always `"never"` in this feature |
| `PendingWarning` | `string?` | `null` for now (no per-node decisions yet); slot for future |

Most "summary" lines are placeholders by intent — the workspace data model doesn't yet carry every concept the mockup shows. As later features add nodes / pending state / dry-run history to `WorkspaceModel`, these properties get richer.

---

## 6. Extension Points

| Seam | Status after this feature | Next consumer |
|------|---------------------------|---------------|
| `MainWindowViewModel.Overview` (child VM) | implemented | every later UI feature adds a sibling child VM (`Seed`, `Graph`, `Extras`, `Run`) |
| Tab strip in `MainWindow.xaml` | populated | each tab gets a real `<UserControl>` instead of placeholder text |
| `IWorkspaceStore` | first consumer landed | dialogs / save flow extend usage |
| `IDialogService` | not yet | `desktop-startup-and-open-workspace` brings it |
| `IUiDispatcher` | not yet | `desktop-seed-tab` brings it |

---

## 7. Risks & Open Questions

- **DI scope of child VMs.** `OverviewViewModel` is registered Singleton (matches recipe DI-lifetimes table). When the VM grows to hold transient observable collections (e.g. node-state per node), Singleton is still correct because the shell holds one workspace at a time (single-document model, L9). Keep singletons unless evidence forces a change.
- **`MainWindowViewModel` constructor injection of `OverviewViewModel`.** This is the recipe-preferred shape but creates a soft coupling: `MainWindowViewModel` must constructor-inject every tab VM. As more tabs gain real VMs, the ctor argument list grows. Acceptable for ~5 tabs; revisit if it balloons.
- **Empty-shell rendering when `Workspace == null`.** The placeholder XAML lives inside `MainWindow.xaml` and disappears when `desktop-startup-and-open-workspace` introduces the proper M1 welcome view. Plan to extract into a `WorkspaceWelcomeView` UserControl at that time, not now.
- **Title-bar binding gotcha.** WPF's `Window.Title` binding fires before `DataContext` is set if the binding is in static XAML. We sidestep this by initializing `Workspace` *before* `Show()` — the binding evaluates once with correct state.

---

## 8. What This Feature Does *Not* Build

- No File / Open / New / Save UX. Toolbar buttons are visible but disabled.
- No tab content beyond Overview (placeholder text in Seed / Graph / Extras / Run).
- No connection editor; no graph; no run pane; no dialogs.
- No persistence of "last opened workspace" between launches; no recent-files list.
- No keyboard shortcuts.
- No app icon, no installer.
- No engine invocation.
