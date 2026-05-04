# 03 — desktop-skeleton — Walking skeleton scaffold

## Goal

A launchable empty WPF app: new project, Generic Host wired up, MahApps `MetroWindow` displayed, `MainWindowViewModel` resolved out of DI. One smoke test that proves the host can be built and the VM resolved without instantiating any `Window`.

## Track

`desktop` (new stack — first executable step on it).

## What Exists

- ADR-007 (locked in step 01) — defines the target framework, MVVM library, composition pattern.
- ADR-008 (locked in step 01) — defines the UI-controls library set.
- `docs/methodology/wpf-desktop.md` (locked in step 02) — defines project shape, MVVM conventions, theming bootstrap.
- `CLAUDE.md` `### WPF` tripwires (locked in step 02) — enforced from this step onward.
- `SQL Buldozer.sln` — solution file. New project must be added.
- `ParameterizationExtractor/AppBootstrap.cs` — pattern exemplar for Generic Host configuration in this repo.
- `ParameterizationExtractor/Common/SqlBuldozerApp.cs` — exemplar for `IServiceCollection` / DI conventions.
- `Tests/Tests.csproj` — single test project; this step adds a smoke test in a new `Tests/Desktop/` folder.

## What to Build

- **New project** `ParameterizationExtractor.Desktop/ParameterizationExtractor.Desktop.csproj`:
  - `<TargetFramework>net10.0-windows</TargetFramework>`
  - `<UseWPF>true</UseWPF>`
  - `<OutputType>WinExe</OutputType>`
  - `<RootNamespace>Quipu.ParameterizationExtractor.Desktop</RootNamespace>`
  - `<Nullable>enable</Nullable>` (alignment with modern stack)
  - `<ImplicitUsings>enable</ImplicitUsings>`
  - PackageReferences:
    - `Microsoft.Extensions.Hosting` 10.0.0
    - `CommunityToolkit.Mvvm` 8.x (latest 8.x compatible with net10)
    - `MahApps.Metro` 2.4.x
    - `Serilog.Extensions.Hosting` (matching version family in repo)
  - ProjectReferences: `ParameterizationExtractor.Logic`, `ParameterizationExtractor.Common`. **No** ref to DSL or DSL.Connector.
- **Solution** `SQL Buldozer.sln` — add the new project.
- **`App.xaml`** — `MahApps.Metro` resource dictionaries merged at `Application.Resources`. No code-behind logic.
- **`App.xaml.cs`** — `OnStartup`: build Generic Host with:
  - `ConfigureAppConfiguration` (load `appsettings.json` + env)
  - `ConfigureLogging` via Serilog (Console + File, mirroring the CLI's defaults)
  - `ConfigureServices`: register `MainWindow` (Singleton), `MainWindowViewModel` (Singleton)
  - On startup, resolve `MainWindow` from the host, set its `DataContext` to the resolved VM, call `Show()`.
  - `OnExit`: dispose the host.
- **`appsettings.json`** in the desktop project — minimal Serilog block + empty placeholder for future use. Include this file as `Content` with `CopyToOutputDirectory=PreserveNewest`. (Note: `appsettings.json` is gitignored repo-wide; ship it as a real file in source. If gitignore conflicts, add an explicit `!ParameterizationExtractor.Desktop/appsettings.json` un-ignore line.)
- **`MainWindow.xaml`** — `mah:MetroWindow` titled "SQL Buldozer". Empty `Grid` body. No code-behind beyond `InitializeComponent()`.
- **`MainWindowViewModel.cs`** — `[ObservableObject]` partial class. One read-only property `WindowTitle => "SQL Buldozer"`. No commands.
- **Smoke test** — new `Tests/Desktop/HostCompositionTests.cs`:
  - One test: builds the desktop's host (extract host-builder logic into a static `DesktopHost.CreateHostBuilder()` so it can be tested without `Application` running), resolves `MainWindowViewModel`, asserts non-null and `WindowTitle == "SQL Buldozer"`.
  - Test does not instantiate `MainWindow` (avoids needing an `Application` instance and a UI thread).
  - Add `Tests/Tests.csproj` `<ProjectReference>` to the new desktop project.

## Acceptance Criteria

- [ ] `dotnet build "SQL Buldozer.sln"` — 0 errors. Warnings acceptable only if pre-existing (FParsec architecture warning is known).
- [ ] `dotnet test "SQL Buldozer.sln"` — pre-existing 33 + new 1 = **34** tests pass; characterisation goldens unchanged.
- [ ] Manual: running `ParameterizationExtractor.Desktop.exe` opens a MahApps `MetroWindow` titled "SQL Buldozer", with no console output errors. Window closes cleanly via the close box; process exits with code 0.
- [ ] `ParameterizationExtractor.Desktop.csproj` does **not** reference DSL or DSL.Connector (verified by inspection / `grep "<ProjectReference" ParameterizationExtractor.Desktop/*.csproj`).
- [ ] `MainWindowViewModel` does not reference any WPF type (verified by inspection — no `using System.Windows*` or `PresentationCore` imports).
- [ ] No `Click=` handlers, no logic in `MainWindow.xaml.cs` beyond `InitializeComponent()`.
- [ ] Recipe (`docs/methodology/wpf-desktop.md`) references match the actual scaffolded shape — if any drift, update the recipe rather than the code (recipe is binding policy, but the first concrete pass is allowed to refine wording).

## References

- ADRs: `adr/007-desktop-wpf-stack.md`, `adr/008-desktop-ui-controls.md`
- Methodology: `docs/methodology/wpf-desktop.md`, `docs/methodology/dotnet-cli.md` (for Generic Host pattern exemplar)
- Existing pattern exemplars in code: `ParameterizationExtractor/AppBootstrap.cs`, `ParameterizationExtractor/Common/SqlBuldozerApp.cs`
- Layering: `adr/002-module-layering.md`
- Depends on: steps 01 and 02 must be `[x]` first.
