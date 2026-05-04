# 007 — Desktop UI stack — WPF on .NET 10 with CommunityToolkit.Mvvm and Generic Host

## Status

Accepted

## Context

SQL Buldozer is adding a desktop UI as a third operator-facing surface alongside the CLI. The `ExtractConfig.xml` map is the working format today; the F# DSL is frozen ([ADR-005](./005-freeze-fsharp-dsl.md)). The desktop will be the primary authoring surface going forward.

We need a single decision that locks the load-bearing pieces of this new stack — target framework, MVVM library, app composition / DI, logging, and testing — so subsequent UI features build on a settled foundation. Each piece below was discussed and rejected several alternatives; this ADR records the conclusions, not the deliberation.

The work is AI-driven: I (the AI) write nearly all of the desktop code. Stack picks are biased toward strong AI affinity — explicit, type-checked, source-generated patterns over magic-string conventions; first-party Microsoft documentation over third-party blog posts; large open-source training corpus; stable, current APIs.

## Decision

We will build the desktop UI as a new project `ParameterizationExtractor.Desktop`, with:

- **Target framework:** `net10.0-windows`, `<UseWPF>true</UseWPF>`, `<OutputType>WinExe</OutputType>`. Root namespace `Quipu.ParameterizationExtractor.Desktop`. Windows-only by design — there is no portability requirement for this tool.
- **UI framework:** WPF (built into the .NET SDK).
- **MVVM library:** **CommunityToolkit.Mvvm 8.x.** Source generators (`[ObservableObject]`, `[ObservableProperty]`, `[RelayCommand]`) generate the `INotifyPropertyChanged` / `ICommand` boilerplate at compile time.
- **App composition / DI:** **`Microsoft.Extensions.Hosting` (Generic Host)** with the standard `Microsoft.Extensions.DependencyInjection` container. The host is built in `App.xaml.cs` `OnStartup`; the main window is resolved out of the resulting `IServiceProvider`. This keeps the desktop on the same DI primitives the CLI uses ([ADR-006](./006-msdi-container.md)) — same `IServiceCollection`, same constructor injection, same `Logging` / `Configuration` extension shape — even though the CLI today uses its custom `AppBuilder` wrapper. Generic Host is the path ADR-006 explicitly named as the right direction for new composition surfaces.
- **Logging:** Serilog via `Serilog.Extensions.Hosting`, configured from `appsettings.json`. Same shape as the CLI.
- **Testing:** NUnit (the existing `Tests/` project) plus **FluentAssertions** as a test-time-only dependency. ViewModel-level tests only — WPF UI is not unit-tested.
- **Module layering:** the desktop project references **`ParameterizationExtractor.Common` and `ParameterizationExtractor.Logic` only.** It does **not** reference `ParameterizationExtractor.DSL` or `ParameterizationExtractor.DSL.Connector`. The F# DSL is frozen ([ADR-005](./005-freeze-fsharp-dsl.md)) — its only legal C# consumer remains the CLI; the desktop's authoring surface will be the workspace JSON file, not the DSL. The new ProjectReference graph extension is `Desktop → Common, Logic`. The one-way layering tripwire ([ADR-002](./002-module-layering.md)) is unchanged.

Alternatives considered:

- **WinUI 3** — plausible modern alternative. Rejected because WPF has a vastly larger AI training corpus, mature third-party control libraries, and stable tooling on .NET 10. WinUI 3 still has rough edges and a smaller example base.
- **Avalonia** — adds cross-platform reach we do not need for a developer CLI tool whose author works on Windows. Smaller corpus than WPF.
- **Caliburn.Micro** for MVVM — convention-based binding (`Save()` method binds to `<Button x:Name="Save"/>`). Rejected for AI-friendliness reasons: magic-string conventions are invisible at compile time; smaller and more fragmented training corpus than CTK.MVVM (v2 / v3 / v4 answers don't interchange); doesn't compose cleanly with Generic Host.
- **ReactiveUI** for MVVM — powerful but a different mental model (Rx-first). Smaller corpus than CTK.MVVM. AI tends to over-engineer with Rx.
- **Hand-rolled `INotifyPropertyChanged`** — too much boilerplate per property; AI-written code stops being short and idiomatic.
- **Prism** for app composition — region/module orchestration is overkill for a single-window IDE.
- **Electron / Tauri / Blazor Hybrid** — second language stack or awkward dev loop. Cost not worth it.

## Consequences

- **Easier:** the desktop and the CLI share the MS.DI primitives, the Configuration binding shape, and the Serilog layout — the only divergence is the host shape (Generic Host on the desktop, custom `AppBuilder` on the CLI). AI-written code stays short because the source generators handle the boilerplate. Test approach matches the rest of the repo (NUnit). MahApps + AvalonEdit choices ([ADR-008](./008-desktop-ui-controls.md)) compose cleanly with the picks here.
- **Harder:** Windows-only is now load-bearing. If the project ever wants to ship on Mac/Linux, the desktop project gets rewritten — but the engine (`Logic`, `Common`) does not. CTK.MVVM source generators add a build-time dependency on Roslyn analyzers; `dotnet build` must support them (it does on .NET 10). Generic Host's lifecycle (`IHostedService.StopAsync`) and WPF's `Application.Exit` need to interlock cleanly — handled in the recipe (`docs/methodology/wpf-desktop.md`) and verified by the smoke test in step 03.
- **Open follow-ups:**
  - The CLI's custom `AppBuilder` could later migrate to Generic Host for full composition symmetry. Not imposed by this ADR; called out as a candidate refactor in [ADR-006](./006-msdi-container.md). The desktop's success with Generic Host gives evidence either way.
  - When/if a CTK.MVVM 9.x lands and changes source-generator output, this ADR may need a `Status: Superseded by NNN` if the upgrade is non-trivial. Not anticipated in the near term.
