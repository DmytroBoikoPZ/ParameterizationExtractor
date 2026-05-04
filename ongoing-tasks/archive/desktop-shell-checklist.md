# Desktop Shell

## Goal

Replace the empty `Grid` inside `MainWindow.xaml` with the tabbed shell from mockup [M3](../docs/design/desktop-ui/03-shell.md) — toolbar, tab strip (`Overview` / `Seed` / `Graph` / `Extras` / `Run`), Overview tab content bound to a `WorkspaceModel`, status bar, and dirty-state title-bar marker. Foundation that hosts every subsequent UI feature.

This feature is the first user-visible surface to consume `IWorkspaceStore` ([from `desktop-workspace-format`](./archive/desktop-workspace-format-checklist.md)). It's also the first feature to introduce `View` ↔ `ViewModel` pairing as a real pattern (vs. the empty `MainWindow` from `desktop-skeleton`).

## Scope

- **In scope:**
  - **Shell chrome** in `MainWindow.xaml`: title-bar with workspace name + dirty marker (`*`); toolbar with `[Run]` / `[Dry-run]` / `[Save]` buttons (disabled in this feature) + connection-indicator pill (read-only label); tab strip with 5 tabs; status bar with dirty marker + last-save timestamp.
  - **Overview tab** — first-class view with VM. `Views/Overview/OverviewView.xaml(.cs)` + `OverviewViewModel.cs`. Content per mockup M3: workspace name, source identity, seed query summary, path / extras / scripts counts, last dry-run timestamp (placeholder `null` → "never"), and a pending-nodes warning slot.
  - **Placeholder tabs** for `Seed` / `Graph` / `Extras` / `Run` — each shows a single inline `TextBlock` (e.g. *"Seed tab — implemented in `desktop-seed-tab`"*). No UserControls created for these yet; their owning features will scaffold them.
  - **`MainWindowViewModel` refactor** — gains an `IWorkspaceStore` dependency, an observable `Workspace` property (`WorkspaceModel?`), an `InitializeAsync(string? path)` entry point, and child VMs (just `OverviewViewModel` for now). Existing `WindowTitle` becomes a derived computed property of the workspace name + dirty marker.
  - **Workspace loading at startup** — `App.OnStartup` reads the first command-line argument as a workspace path (if present), and calls `MainWindowViewModel.InitializeAsync(path)` before `window.Show()`. If no argument, `InitializeAsync(null)` runs and the shell renders an "empty-shell" placeholder. The proper *welcome* / *open* / *new* UX is **out of scope** — `desktop-startup-and-open-workspace` owns it.
  - **Sample workspace runtime asset** — `examples/sample.bws` is copied to the Desktop output via `<Link>Examples\sample.bws</Link>` in `ParameterizationExtractor.Desktop.csproj`, mirroring the Tests setup. Used for manual smoke launches: `dotnet run -- examples/sample.bws`.
  - **VM tests** — `OverviewViewModelTests` and a `MainWindowViewModelTests` that covers the workspace-load → state-population path. Tests don't instantiate any `Window` (per recipe).
  - **Cross-doc updates** — `docs/architecture/overview.md` § 2 (Components) Desktop row gains a sentence about the shell; `docs/methodology/wpf-desktop.md` § "How to add a screen" is verified against the actual Overview implementation (this feature is the first concrete reference for the recipe).
- **Out of scope:**
  - **Welcome / Open / New flows** — empty-shell placeholder is a stub; full UX lands in `desktop-startup-and-open-workspace`.
  - **`IDialogService` / `IUiDispatcher`** — neither is needed (no dialogs, no async UI mutation that crosses thread boundaries; `InitializeAsync` runs on the dispatcher before `Show`).
  - **Save / Run / Dry-run actions** — toolbar buttons are visible but disabled. Logic lands in `desktop-dry-run-and-execute` and the (not-yet-named) "save" feature.
  - **Edit-connection / connection switching** — connection-indicator is a read-only label. `desktop-connection-management` owns the editable flow.
  - **Tab content for Seed / Graph / Extras / Run** — placeholder text only.
  - **Recent-files menu / drag-drop / `.bws` file association** — `desktop-startup-and-open-workspace` territory.
  - **Workspace mutations** — read-only display. No "set as seed", no node-strategy editing, no extras editing.
  - **Engine invocation / dry-run** — no `Logic.PackageProcessor` calls.
- **Dependencies:**
  - [`desktop-skeleton`](./archive/desktop-skeleton-checklist.md) — Generic Host + DI + empty `MainWindow` exist.
  - [`desktop-workspace-format`](./archive/desktop-workspace-format-checklist.md) — `IWorkspaceStore`, `WorkspaceModel`, `examples/sample.bws` exist; this feature is their first UI consumer.
  - Mockup [`docs/design/desktop-ui/03-shell.md`](../docs/design/desktop-ui/03-shell.md) — the visual contract this feature renders.

## Architecture

See [feature-architecture.md](./desktop-shell/feature-architecture.md) for the VM hierarchy, the View ↔ ViewModel pairing pattern this feature establishes, the dispatcher / threading model, and what each later UI feature plugs into.

## Steps

- [x] [01 — Shell chrome + Overview view + VMs](./desktop-shell/01-desktop-shell-vm-and-overview-view.md)
- [x] [02 — Workspace loading at startup + sample asset + smoke](./desktop-shell/02-desktop-shell-workspace-loading.md)
- [x] [03 — Cross-doc updates](./desktop-shell/03-desktop-shell-cross-docs.md)

## Notes

### 2026-05-02 — Step 01 (shell chrome + Overview VM/view) complete

**New files:**

- `Views/Overview/OverviewView.xaml(.cs)` — `internal sealed UserControl`, `x:ClassModifier="internal"`, code-behind only `InitializeComponent()`. Built-in `BooleanToVisibilityConverter` keyed `BoolToVis`. Layout: workspace name as 20pt header, a 5-row label/value grid (Source / Seed / Path / Extras / Last dry-run), a `PendingWarning` slot, and an italic empty-state message bound to `IsEmpty`.
- `Views/Overview/OverviewViewModel.cs` — `internal sealed partial : ObservableObject`. Nine `[ObservableProperty]` fields (the 7 summary strings from the architecture's State Model § 5 plus `IsEmpty` and `EmptyStateMessage`). Single public method `Show(WorkspaceModel?)` resets every field for null input and populates from workspace fields otherwise.
- `Tests/Desktop/OverviewViewModelTests.cs` — 4 tests (planned 3 + a transition test that proves Show(workspace) → Show(null) cleanly resets state).

**Modified files:**

- `MainWindowViewModel.cs` — full rewrite. Constructor injects `OverviewViewModel`; exposes `Workspace` (`WorkspaceModel?` `[ObservableProperty]` with `[NotifyPropertyChangedFor(nameof(WindowTitle))] [NotifyPropertyChangedFor(nameof(ConnectionDisplay))]`); computed `WindowTitle`, `LastSavedDisplay`, `ConnectionDisplay`; `IsDirty` slot (always false); `partial void OnWorkspaceChanged(WorkspaceModel?)` propagates to `Overview.Show(value)`. Constructor calls `Overview.Show(null)` so the child VM starts in the empty state without races.
- `MainWindow.xaml` — `<Grid />` replaced with a 3-row Grid: toolbar (Run / Dry-run / Save buttons all `IsEnabled="False"`, plus a connection-indicator `TextBlock`); `TabControl` with 5 `TabItem`s (Overview hosts the Overview UserControl with `DataContext="{Binding Overview}"`; the other 4 hold inline placeholder `TextBlock`s referencing their owning future feature); status bar with `last save:` + `LastSavedDisplay`. Window grew from 600×900 to 700×1100.
- `DesktopHost.cs` — `services.AddSingleton<OverviewViewModel>()` registered. Resolution order in `App.OnStartup` unchanged.
- `Tests/Desktop/HostCompositionTests.cs` — 2 new tests: `DesktopHost_ResolvesOverviewViewModel`, `MainWindowViewModel_NoWorkspaceLoaded_TitleIsBranded`.

**Verification:**

- `dotnet build` 0 errors; `dotnet test` 61/61 pass (was 55; +6).
- Manual smoke launch: window opens with the tab strip; Overview tab shows "<no workspace>" header + placeholder summary lines + the italic "Pass a .bws path on the command line…" empty-state message. `CloseMainWindow()` → exit code 0.
- Tripwire compliance: grep over the new files + `MainWindowViewModel.cs` returns 0 hits for `MessageBox.Show`, `IConfiguration[`, `Console.WriteLine`, `Thread.Sleep`, `.Result`, `.Wait()`, `Dispatcher.Invoke`, `TODO`, `FIXME`. `OverviewViewModel` has no `using System.Windows*`.

### 2026-05-02 — Step 02 (workspace loading at startup) complete

**Modified files:**

- `MainWindowViewModel.cs` — constructor gains `IWorkspaceStore store` (first parameter); new `public async Task InitializeAsync(string? path, CancellationToken ct = default)` — null/empty path leaves `Workspace = null`, valid path delegates to `_store.LoadAsync`. `OnWorkspaceChanged` partial method (from step 01) keeps propagating to `Overview.Show`.
- `App.xaml.cs` — `OnStartup` gains `e.Args.FirstOrDefault()` parsing and awaits `vm.InitializeAsync(path)` between `host.StartAsync()` and `window.Show()`. Order matters: window paints with workspace state already populated, no async-update flicker.
- `ParameterizationExtractor.Desktop.csproj` — `<None Include="..\examples\sample.bws"><Link>Examples\sample.bws</Link><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>` mirrors the Tests project's setup. `bin/Debug/net10.0-windows/Examples/sample.bws` is now available at runtime.
- `Tests/Desktop/HostCompositionTests.cs` — 3 new tests: `MainWindowViewModel_InitializeAsync_WithNullPath_LeavesWorkspaceNull`, `MainWindowViewModel_InitializeAsync_WithSampleBws_LoadsWorkspaceAndPropagatesToOverview`, `MainWindowViewModel_InitializeAsync_WithMissingFile_Throws` (`FileNotFoundException`).

**Verification:**

- `dotnet build` — 0 errors after clearing a stale docfx incremental cache (same flake as in `desktop-skeleton` and `desktop-workspace-format`; unrelated). `dotnet test` — **64/64 pass** (was 61; +3).
- Manual smoke launch with sample workspace: `Start-Process ParameterizationExtractor.Desktop.exe -ArgumentList "<path>\Examples\sample.bws"` → window opens, title reads "SQL Buldozer — sample-clearing", Overview tab shows source `budzdorov_Core @ 129.212.168.210,1433` and `2 tables in package`. Closes with exit 0.
- Manual smoke launch without args: window opens with empty-shell placeholder; closes with exit 0.
- Tripwire compliance unchanged.

### 2026-05-02 — Step 03 (cross-doc updates) complete

- `docs/architecture/overview.md` § 2 — Desktop row Purpose extended to mention M3 tabbed shell + Overview implementation status.
- `docs/methodology/wpf-desktop.md` § How to add a screen — added a "Reference implementation" pointer at the top of the section pointing at `Views/Overview/`.
- `docs/roadmap.md` — `desktop-shell` moved from `## Proposed next` and `## Desktop UI track` to `## Completed`; `desktop-startup-and-open-workspace` is now the proposed next item.
- `CLAUDE.md` — no change (no new tripwire surfaced). SHA256 sync with `.github/copilot-instructions.md` verified — `ac93dadc6c4bf047ffbb3b680fae09e5373627f7ebe542d000c87165f5b73d46` (unchanged from `desktop-skeleton`).
- `dotnet build` 0 errors; `dotnet test` 64/64 (sanity).

## Final summary

The `desktop-shell` feature is complete: 3/3 steps, 64/64 tests green.

**Cumulative changes:**

- **3 new source files** (Desktop): `Views/Overview/{OverviewView.xaml, OverviewView.xaml.cs, OverviewViewModel.cs}` — the canonical View ↔ ViewModel pairing reference.
- **3 modified files** (Desktop): `MainWindowViewModel.cs` (full rewrite — Workspace property, child VM, computed display fields, `InitializeAsync`), `MainWindow.xaml` (Grid → toolbar / TabControl / status bar), `App.xaml.cs` (CLI args parsing + `await InitializeAsync` before `Show()`), `DesktopHost.cs` (+1 DI registration), `ParameterizationExtractor.Desktop.csproj` (+sample.bws link as runtime asset).
- **2 modified test files** (Tests/Desktop): `HostCompositionTests.cs` (+5 tests), and `OverviewViewModelTests.cs` (new, 4 tests).
- **3 modified doc files**: `docs/architecture/overview.md`, `docs/methodology/wpf-desktop.md`, `docs/roadmap.md`.

**Net test delta:** 55 → 64 (+9: 4 OverviewVM, 5 host-composition).

**Pattern established:** `Views/Xxx/{XxxView.xaml, XxxView.xaml.cs, XxxViewModel.cs}` — every later UI feature copies this layout. The recipe (`docs/methodology/wpf-desktop.md` § How to add a screen) now points at `Views/Overview/` as the canonical reference.

**Known stubs that the next feature replaces:**

- Empty-shell placeholder text in `OverviewView` is a temporary stub — `desktop-startup-and-open-workspace` introduces the proper M1 welcome view in front of the shell.
- 4 tabs (Seed / Graph / Extras / Run) hold inline `TextBlock` placeholders — each tab's owning feature replaces with a real `Views/{Tab}/` pairing.
- Toolbar buttons (Run / Dry-run / Save) are visible but `IsEnabled="False"` — `desktop-dry-run-and-execute` and the (not-yet-named) save feature wire them.

**Open follow-ups (deliberately deferred):**

- `desktop-startup-and-open-workspace` — first feature to ship `IDialogService`, replaces the empty-shell stub with the M1 welcome view.
- `desktop-connection-management` — replaces the read-only `ConnectionDisplay` label with an editable connection editor.
- All remaining mockups (M4–M8) get their own features.

**Pending action:** archive needs your nod (per `prompts/execution.md` § Completion). Pair to move: `ongoing-tasks/desktop-shell-checklist.md` + `ongoing-tasks/desktop-shell/` → `ongoing-tasks/archive/`.
