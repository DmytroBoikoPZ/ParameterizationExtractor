# Desktop UI Skeleton

## Goal

Stand up the foundation for the future desktop UI: lock the WPF stack via ADRs, register WPF as a recognised stack in the methodology framework, and scaffold a launchable empty `ParameterizationExtractor.Desktop` project that boots a Generic Host. **Zero UI work, zero workspace work** — only the load-bearing scaffold so subsequent UI features have something to build on.

## Scope

- **In scope:**
  - Two ADRs: ADR-007 (desktop WPF stack) and ADR-008 (desktop UI controls).
  - New stack recipe `docs/methodology/wpf-desktop.md`.
  - `CLAUDE.md` updates: add WPF row to routing table; add `### WPF` tripwires section. Mirror byte-identical to `.github/copilot-instructions.md`.
  - New project `ParameterizationExtractor.Desktop` (`net10.0-windows`, WPF), referencing `Logic` + `Common`.
  - Generic Host composition (symmetric with CLI / ADR-006). Single empty `MainWindow` (MahApps `MetroWindow`) + a registered `MainWindowViewModel`.
  - Smoke test in `Tests/` that builds the host and resolves the main view model.
  - Cross-doc updates: `docs/architecture/overview.md` § 2 and § 7; `adr/readme.md` index.
- **Out of scope:**
  - Any UI views beyond an empty `MainWindow` (mockups M1–M8 are deferred to follow-on features).
  - Workspace JSON format and engine consumer (deferred to `desktop-workspace-format` feature).
  - Connection management / DPAPI / credential storage (deferred to `desktop-connection-management`).
  - Graph visualisation library (deferred to `desktop-graph-viz`).
  - Live dry-run / direct execution from the UI (deferred to `desktop-dry-run-and-execute`).
  - Engine behaviour changes — the Logic module is untouched.
- **Dependencies:**
  - ADR-006 (MS.DI is the container) — Generic Host composition extends the same pattern.
  - The mockups in [`docs/design/desktop-ui/`](../docs/design/desktop-ui/readme.md) — define what the skeleton must eventually host. Skeleton itself does not implement them.

## Architecture

See [feature-architecture.md](./desktop-skeleton/feature-architecture.md) for the layer map (View / ViewModel / Service / Engine boundary), Generic Host composition story, and named extension points where the deferred features will plug in.

## Steps

- [x] [01 — ADRs (WPF stack + UI controls)](./desktop-skeleton/01-desktop-skeleton-adrs.md)
- [x] [02 — Stack recipe + CLAUDE.md tripwires](./desktop-skeleton/02-desktop-skeleton-recipe-and-tripwires.md)
- [x] [03 — Walking skeleton scaffold (project + Generic Host + smoke test)](./desktop-skeleton/03-desktop-skeleton-walking-skeleton.md)
- [x] [04 — Cross-doc updates (architecture overview + ADR index)](./desktop-skeleton/04-desktop-skeleton-cross-doc-updates.md)

## Notes

### 2026-05-02 — Step 01 (ADRs) complete

- `adr/007-desktop-wpf-stack.md` — Status `Accepted`. Locks WPF + `net10.0-windows`, CommunityToolkit.Mvvm 8.x, Generic Host (Microsoft.Extensions.Hosting), Serilog, NUnit + FluentAssertions, `Desktop → Common, Logic` layering. Cites ADR-002, ADR-005, ADR-006.
- `adr/008-desktop-ui-controls.md` — Status `Accepted`. Locks MahApps.Metro 2.4.x, AvalonEdit, no docking lib initially. Open follow-ups: graph viz library (deferred to `desktop-graph-viz`), password-at-rest (deferred to `desktop-connection-management`).
- `adr/readme.md` — index rows added for 007 and 008.
- Sanity: `dotnet build` 0 errors, 9 pre-existing warnings (FParsec architecture, SYSLIB obsoletes — unrelated). `dotnet test` 33/33 pass.
- Manual-review AC pending user approval.

### 2026-05-02 — Step 02 (recipe + tripwires) complete

- `docs/methodology/wpf-desktop.md` — new recipe covering composition (Generic Host shape with concrete code snippet), MVVM conventions (CTK.MVVM source generators, View/VM pairing rules), code-behind discipline, DI lifetimes table, configuration POCO binding, Serilog logging, MahApps theme bootstrap location, AvalonEdit `SqlEditor` wrapper rules, `IDialogService` boundary, threading rules, NUnit + FluentAssertions testing, project shape, "how to add a screen" 6-step procedure. References ADRs 002, 005, 006, 007, 008.
- `CLAUDE.md` — routing-table row added (`WPF Desktop module recipe → docs/methodology/wpf-desktop.md`); new `### WPF (Desktop)` section under `## Stack-Specific Tripwires` with 13 bullets (no WPF refs in non-Desktop modules; no DSL ref from Desktop; no code-behind logic; VMs never touch WPF types; no hand-rolled INPC; no `MessageBox.Show` in VMs/services; no `IConfiguration[..]` lookups in business code; no `Console.WriteLine`; theme dictionaries loaded once in App.xaml; AvalonEdit isolated behind `SqlEditor`; no service locator; no `TODO`/`FIXME`; no `Thread.Sleep`/`.Result`/`.Wait()`/`async void`).
- `.github/copilot-instructions.md` — byte-identical mirror; SHA256 `ed2ce2e1b0db2959ea1baf8dd68362b836dc04d070897fad00878150195da9b6` matches CLAUDE.md.
- Sanity: `dotnet build` 0 errors after clearing a stale docfx incremental cache (`ParameterizationExtractor/obj/.cache`). The cache flake was unrelated to step 02 — earlier-in-session build had succeeded against the same docfx config; clearing the cache restored green. `dotnet test` 33/33 pass.

### 2026-05-02 — Step 03 (walking skeleton scaffold) complete

**New project `ParameterizationExtractor.Desktop`:**

- `ParameterizationExtractor.Desktop.csproj` — `net10.0-windows`, `<UseWPF>true</UseWPF>`, `<OutputType>WinExe</OutputType>`, root namespace `Quipu.ParameterizationExtractor.Desktop`, `Nullable enable`, `ImplicitUsings enable`. `<InternalsVisibleTo Include="Tests" />` so the smoke test can resolve the `internal` `MainWindowViewModel`.
- Package refs: `CommunityToolkit.Mvvm 8.4.0`, `MahApps.Metro 2.4.10`, `Microsoft.Extensions.Hosting 10.0.0`, `Serilog 4.2.0` + `Serilog.Extensions.Hosting 9.0.0` + `Serilog.Settings.Configuration 9.0.0` + `Serilog.Sinks.Console 6.0.0` + `Serilog.Sinks.File 6.0.0`. No AvalonEdit yet (skeleton; lands when `SqlEditor` is built).
- Project refs: `ParameterizationExtractor.Common`, `ParameterizationExtractor.Logic`. **No** ref to DSL or DSL.Connector (per ADR-005, ADR-007).
- `App.xaml` — MahApps theme dictionaries (Controls, Fonts, Light.Blue) merged once at `Application.Resources`.
- `App.xaml.cs` — `OnStartup` builds host via `DesktopHost.CreateApplicationBuilder`, starts it, resolves `MainWindow`, sets `DataContext`, shows. `OnExit` stops + disposes the host. `public partial` (WPF startup expects this).
- `DesktopHost.cs` — `internal static` helper exposing `HostApplicationBuilder` for the smoke test. Registers Serilog from `IConfiguration`, `MainWindow` (Singleton), `MainWindowViewModel` (Singleton).
- `MainWindow.xaml(.cs)` — `mah:MetroWindow` with `Title="{Binding WindowTitle}"`, `Height=600`, `Width=900`, empty `Grid`. `x:ClassModifier="internal"`. Code-behind contains only `InitializeComponent()`.
- `MainWindowViewModel.cs` — `internal sealed partial class : ObservableObject`. One `string WindowTitle => "SQL Buldozer"`. No WPF imports (verified: only `CommunityToolkit.Mvvm.ComponentModel`).
- `appsettings.json` — minimal Serilog block (Console + rolling File sinks). Marked `<None Update>` with `CopyToOutputDirectory=PreserveNewest`. **Not gitignored** — the repo's gitignore lines target `ParameterizationExtractor/appsettings.json` and `Tests/appsettings.test.json` specifically, so the new desktop file is tracked by default (no un-ignore line needed, contrary to the original step note).

**Tests project changes:**

- TFM bumped from `net10.0` to `net10.0-windows` (NU1201 — Tests already runs Windows-only via the SQL Server CharacterisationTests, so this is a wash).
- New `<ProjectReference>` to the Desktop project.
- Added `FluentAssertions 7.2.0` (last fully-OSS line; v8+ went commercial).
- New `Tests/Desktop/HostCompositionTests.cs` — single test `DesktopHost_BuildsAndResolvesMainWindowViewModel`: builds the host without `Application` running, resolves `MainWindowViewModel`, asserts non-null + `WindowTitle == "SQL Buldozer"`. Does **not** instantiate any `Window` (would require a UI thread / `Application` instance).

**Solution:**

- `dotnet sln add ParameterizationExtractor.Desktop/ParameterizationExtractor.Desktop.csproj` registered the project.

**Verification:**

- `dotnet build "SQL Buldozer.sln"` — 0 errors. 2 warnings (pre-existing FParsec architecture mismatch).
- `dotnet test "SQL Buldozer.sln"` — **34/34 pass** (was 33; +1 = the new smoke test). All 5 CharacterisationTests still pass — goldens unchanged.
- Manual smoke launch: `Start-Process ParameterizationExtractor.Desktop.exe` opened the MetroWindow, stayed open for 3s, `CloseMainWindow()` shut it down cleanly with exit code 0.

**ACs:** all met except "manual user review of the running window's appearance" — that's the user's gate.

### 2026-05-02 — Step 04 (cross-doc updates) complete

- `docs/architecture/overview.md`:
  - § 1 — topology diagram redrawn to show CLI **and** Desktop as two operator-facing entry points sharing the engine. TFM labels and `System.Data.SqlClient` reference dropped/updated (the diagram now says `Microsoft.Data.SqlClient`); body prose mentions `.bws` workspace as the Desktop's input alongside `ExtractConfig.xml` for the CLI.
  - § 2 — Desktop row added to components table (`net10.0-windows`, WPF, WinExe). Tests row TFM updated to `net10.0-windows`. Reference graph block lists `Desktop → Common, Logic` and explicitly notes Desktop does NOT reference DSL / DSL.Connector (cites ADR-005).
  - § 5 — DI container bullet updated to cover both entry points: CLI uses custom `IAppBuilder` (ADR-006), Desktop uses Generic Host (ADR-007). Both register through `services.AddSingleton<...>()` / `AddTransient<...>()`.
  - § 7 — added rows for ADR-007 and ADR-008. Reordered to chronological (002, 003, 004, 005, 006, 007, 008).
  - § 8 — per-stack recipes pointer extended with `wpf-desktop.md`.
- `CLAUDE.md` `## Repository Shape`:
  - Modules table — added Desktop row; updated Tests row to `net10.0-windows`; updated CLI / Tests TFM in the lead-in paragraph from `net6.0` to `net10.0`/`net10.0-windows`.
  - `### .NET / C#` tripwires — module-layering bullet updated to include `Desktop → Common, Logic` plus the explicit "no DSL refs from Desktop" note (cross-references ADR-005 and ADR-007).
- `.github/copilot-instructions.md` — byte-identical mirror; SHA256 `ac93dadc6c4bf047ffbb3b680fae09e5373627f7ebe542d000c87165f5b73d46` matches CLAUDE.md.
- `adr/readme.md` — index rows for 007 / 008 already added in step 01; verified consistent with actual ADR titles + `Accepted` status.
- Sanity: `dotnet build` 0 errors, `dotnet test` 34/34 pass. Goldens unchanged.

## Final summary

The `desktop-skeleton` feature is complete: 4/4 steps, 34/34 tests green.

**Cumulative changes:**

- **2 new ADRs:** ADR-007 (WPF + .NET 10 + CTK.MVVM + Generic Host) and ADR-008 (MahApps.Metro + AvalonEdit + no docking lib). Both `Accepted`.
- **1 new stack recipe:** `docs/methodology/wpf-desktop.md` — opinionated, concrete, 13-section "how" guide for routine WPF work.
- **CLAUDE.md (mirrored to copilot-instructions.md)** — new routing-table row, new `### WPF (Desktop)` tripwires section (13 bullets), updated `### .NET / C#` layering tripwire, updated Repository Shape lead-in + table.
- **1 new project:** `ParameterizationExtractor.Desktop` — `net10.0-windows`, WPF, refs `Common` + `Logic`. Generic Host composition in `App.xaml.cs` via `DesktopHost.CreateApplicationBuilder()`. Empty `mah:MetroWindow` titled "SQL Buldozer". Runs and closes cleanly with exit code 0.
- **Tests project:** TFM bumped to `net10.0-windows`, `FluentAssertions 7.2.0` added, new `Tests/Desktop/HostCompositionTests.cs` (1 test).
- **`docs/architecture/overview.md`** — § 1 / § 2 / § 5 / § 7 / § 8 reflect the two-entry-point topology.

**Open follow-ups (deliberately deferred to subsequent features):**

- `desktop-workspace-format` — JSON schema, `.bws` extension, engine consumer.
- `desktop-connection-management` — DPAPI password storage; ADR for password-at-rest.
- `desktop-graph-viz` — graph library pick (Msagl candidate); FK graph rendering.
- `desktop-dry-run-and-execute` — wires the Run-tab UI to `Logic.PackageProcessor`.
- UI features per `docs/design/desktop-ui/` — driven by mockups M1–M8, sequenced after the format/connection/graph features land.
- Stale facts in `dotnet-cli.md` § Testing and § Project shape (NUnit version, framework targets, FluentCommandLineParser legacy reference) — pre-existing tech debt, not surfaced in this feature.
- ADR-006 candidate refactor: CLI's custom `AppBuilder` could migrate to Generic Host now that the Desktop validates the pattern. Not imposed.

**Pending action:** archive needs your nod (per execution.md § Completion). Pair to move: `ongoing-tasks/desktop-skeleton-checklist.md` + `ongoing-tasks/desktop-skeleton/` → `ongoing-tasks/archive/`.
