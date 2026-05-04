# 02 — desktop-skeleton — Stack recipe + CLAUDE.md tripwires

## Goal

Make WPF a first-class stack in the methodology framework. Author the stack recipe `docs/methodology/wpf-desktop.md` as the opinionated "how" reference for routine WPF work, and add the `### WPF` tripwires section to `CLAUDE.md`. Mirror byte-identical to `.github/copilot-instructions.md`.

This recipe is the document I (the AI) will read every time I touch the desktop project. It must be opinionated and concrete, not aspirational.

## Track

`cross-cutting` (docs / methodology).

## What Exists

- `docs/methodology/dotnet-cli.md` — pattern exemplar: composition / configuration / logging / module layering / testing / project shape sections. The new recipe mirrors this shape.
- `docs/methodology/fsharp-dsl.md` — second sibling exemplar; shorter, more rule-list style.
- `CLAUDE.md` — already has `## Stack-Specific Tripwires` with `### .NET / C#` and `### F#` subsections. Add `### WPF` between them or after — author's call inside the step.
- `.github/copilot-instructions.md` — byte-identical mirror of `CLAUDE.md`. Verified by `verify-bootstrap.ps1` SHA256 check.
- `prompts/add-stack.md` (if present) — process guide for introducing a new stack post-bootstrap. Read first.
- `ADR-007` and `ADR-008` from step 01 — the recipe cites them, doesn't restate decisions.
- The component map at `docs/design/desktop-ui/components.md` — recipe references it as the View / UserControl inventory (recipe doesn't *contain* the inventory; it points to it).

## What to Build

- `docs/methodology/wpf-desktop.md` covering, at minimum:
  - **Composition** — Generic Host pattern; where `App.xaml.cs` builds the host; how `MainWindow` is resolved out of DI; how `IHostedService.StopAsync` interlocks with `Application.Exit`.
  - **MVVM conventions** — CTK.MVVM `[ObservableObject]` / `[ObservableProperty]` / `[RelayCommand]`. ViewModel naming: `XxxView.xaml(.cs)` ↔ `XxxViewModel.cs` paired in `Views/Xxx/`. ViewModels are `internal sealed`. ViewModels never reference WPF types (no `Window`, no `Visibility`, no `Brush` — use enums + value converters).
  - **Code-behind discipline** — `.xaml.cs` contains only `InitializeComponent()` plus framework overrides that demonstrably *cannot* live in a behaviour. No `Click=` handlers, no logic. If a behaviour is needed, use `Microsoft.Xaml.Behaviors.Wpf` or a CTK `[RelayCommand]`.
  - **DI lifetimes** — VMs default to `Transient` unless the screen is genuinely a single instance for app lifetime (then `Singleton`). Services default to `Singleton` unless they hold per-operation state.
  - **Configuration** — `appsettings.json` shipped with the desktop project, read via `Microsoft.Extensions.Configuration` in the host builder. POCO binding only; no `IConfiguration["..."]` lookups in business code (mirrors the CLI tripwire).
  - **Logging** — Serilog, same shape as the CLI. UI sink (in-memory observable buffer for the Run log) is a future feature; recipe names the seam.
  - **Theming / chrome** — MahApps.Metro resource dictionaries loaded from `App.xaml`. Document the canonical `<Application.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries>` block.
  - **Code editor** — when a multi-line SQL editor is needed, wrap AvalonEdit in a `UserControl` (`SqlEditor`) and bind through a dependency property — never bind to AvalonEdit's `TextDocument` directly from VMs (it leaks WPF types).
  - **Dialogs / file pickers** — `IDialogService` abstraction (named in `feature-architecture.md` § 5). VMs depend on the interface; the WPF impl uses MahApps `MetroDialog` for confirms and `Microsoft.Win32.OpenFileDialog` for files.
  - **Threading** — VMs run on the dispatcher. Long-running work goes through `Task.Run` and marshals back via `await`. No `Dispatcher.Invoke` in VMs; if needed, an `IUiDispatcher` abstraction.
  - **Testing** — VMs are unit-testable; views are not. NUnit + FluentAssertions for VM tests. Tests live under `Tests/Desktop/` (new folder). No automated UI tests in this stack.
  - **Project shape** — quick-reference table: `ParameterizationExtractor.Desktop` / `net10.0-windows` / `Quipu.ParameterizationExtractor.Desktop` / `<UseWPF>true</UseWPF>`.
  - **How to add a screen** — five-step recipe: create `Views/Xxx/` folder, add `XxxView.xaml(.cs)` + `XxxViewModel.cs`, register VM in DI, add to parent's composition, write VM test. Each step one paragraph.
- `CLAUDE.md` — add `### WPF` tripwires under `## Stack-Specific Tripwires`. Tripwires (proposed; finalize during step):
  - WPF code lives only in `ParameterizationExtractor.Desktop`. Logic / Common / DSL / DSL.Connector never reference WPF or PresentationCore.
  - No logic in `.xaml.cs` code-behind beyond `InitializeComponent()` and necessary framework overrides.
  - ViewModels never reference WPF types (`Window`, `Visibility`, `Brush`, `Dispatcher`, etc.). Use enums + value converters or an `IUiDispatcher` abstraction.
  - No `MessageBox.Show` in VMs or services — use `IDialogService`.
  - No `IConfiguration["..."]` lookups in VMs or services. Bind to POCOs in `App.xaml.cs` only.
  - Theme dictionaries loaded once in `App.xaml`. Do not load MahApps resources per-window.
  - AvalonEdit is wrapped in the `SqlEditor` UserControl. VMs never see `TextDocument` or `TextEditor` types.
  - No `Thread.Sleep`, `.Result`, `.Wait()`, `async void` outside event handlers (mirrors the .NET tripwire — call out as still in force on this stack).
  - No `Console.WriteLine` (mirrors the .NET tripwire — still in force).
- `CLAUDE.md` — routing-table row added for `WPF Desktop module recipe → docs/methodology/wpf-desktop.md`.
- `.github/copilot-instructions.md` — byte-identical copy of the updated `CLAUDE.md`.

## Acceptance Criteria

- [ ] `docs/methodology/wpf-desktop.md` exists and covers every section in the "What to Build" list above.
- [ ] Recipe explicitly cites ADR-007 and ADR-008 in a `## References` section at the end.
- [ ] `CLAUDE.md` has a routing-table row pointing at `docs/methodology/wpf-desktop.md`.
- [ ] `CLAUDE.md` has `### WPF` tripwires section under `## Stack-Specific Tripwires`, with at least the tripwires listed in "What to Build".
- [ ] `.github/copilot-instructions.md` is byte-identical to `CLAUDE.md`. SHA256 verified (script if present, otherwise manual `Get-FileHash` compare).
- [ ] No code changes; `dotnet build` and `dotnet test` still 0 / 33 (sanity).
- [ ] Manual review: user reads `wpf-desktop.md` end-to-end and confirms it matches how they want me to work in this stack.

## References

- Methodology siblings: `docs/methodology/dotnet-cli.md`, `docs/methodology/fsharp-dsl.md`
- ADRs: `adr/007-desktop-wpf-stack.md`, `adr/008-desktop-ui-controls.md` (from step 01)
- Bootstrap process: `prompts/add-stack.md` (if present)
- Component inventory: `docs/design/desktop-ui/components.md`
- Depends on: step 01 must be `[x]` first (recipe cites the ADRs).
