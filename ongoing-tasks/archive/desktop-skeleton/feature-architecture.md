# Desktop Skeleton — Architecture Overview

> Foundation only. Locks how the desktop project is composed, where the View / ViewModel boundary sits, and which seams subsequent features will plug into. **No UI views are built in this feature** beyond an empty placeholder `MainWindow`.

---

## 1. Pipeline / Integration

The desktop becomes a third consumer of the engine, alongside the CLI. It does not change how the engine is built, walked, or rendered — it provides a launchable shell that *will* host the UI in later features.

```
Existing:
  ParameterizationExtractor (CLI)
        │
        ├── Microsoft.Extensions.Hosting (Generic Host)
        ├── Microsoft.Extensions.DependencyInjection
        ├── Microsoft.Extensions.Configuration  (appsettings.json + env)
        ├── Serilog                              (Console + File sinks)
        │
        └── Logic.PackageProcessor / SqlBuldozerApp pipeline → .sql files

After this feature:
  ParameterizationExtractor.Desktop (NEW)
        │
        ├── Microsoft.Extensions.Hosting (Generic Host)   — symmetric with CLI
        ├── Microsoft.Extensions.DependencyInjection
        ├── CommunityToolkit.Mvvm                         — VM source generators
        ├── MahApps.Metro                                 — chrome / dialogs
        ├── AvalonEdit                                    — code editor (wired in later features)
        ├── Serilog                                       — file + UI sink (UI sink in later features)
        │
        └── (later) Logic.PackageProcessor — direct invocation for dry-run / execute
```

| Stage | Where | What it does at the end of this feature |
|-------|-------|------------------------------------------|
| Process start | `App.xaml.cs` | Builds Generic Host; registers MVVM services; resolves `MainWindow` |
| Composition root | `DesktopHostBuilder` (or equivalent) in `App.xaml.cs` | Configures DI, logging, configuration |
| Window | `MainWindow.xaml` (MahApps `MetroWindow`) | Renders an empty placeholder titled "SQL Buldozer" |
| ViewModel | `MainWindowViewModel.cs` | `[ObservableObject]`, registered `Singleton`. No-op placeholder. |
| Engine boundary | not invoked yet | Project ref to `Logic` + `Common` exists so future features can `services.AddBuldozerEngine()` symmetrically with the CLI |

---

## 2. Component Diagram

```
+--------------------------------------------------+
|  ParameterizationExtractor.Desktop  (NEW)        |
|  net10.0-windows · WPF                           |
|                                                  |
|  +--------------------+   +-------------------+  |
|  | App.xaml(.cs)      |-->| Generic Host      |  |
|  | (composition root) |   | IServiceProvider  |  |
|  +--------------------+   +---------+---------+  |
|                                     |            |
|                          resolves   v            |
|                           +-------------------+  |
|                           | MainWindow (View) |  |
|                           +---------+---------+  |
|                                     |            |
|                       data context  v            |
|                           +-------------------+  |
|                           | MainWindowVM      |  |
|                           +-------------------+  |
+--------------------------------------------------+
              |                       |
              | ProjectReference      | ProjectReference
              v                       v
       Logic (existing)        Common (existing)
       (not invoked yet)       (not invoked yet)
```

Dependency direction (extends the existing layering tripwire — see ADR-002):

```
CLI            -> Common, Logic, DSL, DSL.Connector
Desktop  (new) -> Common, Logic                       <-- this feature
DSL.Connector  -> DSL, Logic
DSL            -> Logic
Logic          -> Common
Common         -> (nothing)
```

The Desktop module **does not** reference DSL or DSL.Connector. F# is frozen (ADR-005); the desktop authoring surface will be the workspace JSON, not the DSL.

---

## 3. Data Flow

### 3.1 Inbound / Ingestion

End of this feature: none. The MainWindow is empty. The composition root resolves a placeholder VM and shows the window.

Future features will add inputs at clearly named seams (this section names the seams so later features know where to plug in):

- **Workspace open / save** — `IWorkspaceStore` (interface + JSON impl) registered in DI. Lives in `Services/Workspace/`. Named here, not implemented.
- **Source connection** — `ISourceConnectionFactory` (uses Logic's existing `IUnitOfWorkFactory`). Lives in `Services/Connections/`. Named here, not implemented.
- **Engine invocation (dry-run / generate / execute)** — `IExtractionRunner` wrapping `Logic.PackageProcessor`. Lives in `Services/Run/`. Named here, not implemented.

### 3.2 Processing / Event Handling

End of this feature: VMs receive no inputs. The `MainWindowViewModel` exposes a static title string; nothing else.

### 3.3 Query / Serving

End of this feature: none. The desktop doesn't expose anything to other processes.

---

## 4. Data Stores Summary

| Store | Technology | Purpose | Status at end of feature |
|-------|------------|---------|--------------------------|
| `appsettings.json` (desktop) | JSON | Logging defaults, app-level config | Created; minimal Serilog block |
| `%APPDATA%\SqlBuldozer\` | Filesystem | Recent-files list, app prefs (later) | Path reserved in recipe; no code yet |
| Workspace files (`.bws` JSON) | Filesystem | Per-workspace persistence | **Deferred** to `desktop-workspace-format` |

---

## 5. Extension Points

These are the seams where deferred features attach. Naming them now keeps later features small.

| Seam | Interface (proposed) | Lives in | Used by |
|------|----------------------|----------|---------|
| Workspace persistence | `IWorkspaceStore` | `Services/Workspace/` | `desktop-workspace-format` |
| Source connection | `ISourceConnectionFactory` | `Services/Connections/` | `desktop-connection-management` |
| Engine runner | `IExtractionRunner` | `Services/Run/` | `desktop-dry-run-and-execute` |
| Graph layout | `IGraphLayoutEngine` | `Views/Graph/` | `desktop-graph-viz` |
| Dialog / file pickers | `IDialogService` | `Services/Dialogs/` | every UI feature; tested via fakes |

None of these are *implemented* in this feature. They're named so the recipe and ADRs can reference them without ambiguity.

---

## 6. Security & Isolation

- **Trust model unchanged.** The desktop is a developer-side tool, same trust zone as the CLI. No network ingress.
- **Credential management** — workspace JSON will (later) hold a connection block. Password-at-rest strategy is an open question with its own ADR slot in `desktop-connection-management`. End of this feature: no credentials are read or written.
- **LLM output trust** — N/A; the desktop does not consume LLM output.
- **Code-behind discipline** — tripwire in `CLAUDE.md` after step 02 will forbid logic in code-behind. ViewModels never reference WPF types.

---

## 7. Risks & Open Questions

- **Generic Host shutdown story** — WPF's `Application.Exit` and `IHostedService.StopAsync` need to interlock cleanly. Step 03 must demonstrate clean shutdown (no zombie processes).
- **MahApps theme bootstrapping** — themes load from XAML resource dictionaries; placement matters. Recipe must spell out the canonical location (likely `App.xaml`).
- **Test boundary** — WPF UI is not unit-testable; only ViewModels are. The recipe locks this and the smoke test exercises only the host + VM resolution path, not any `Window` instance.
- **CommandLineParser interaction** — the existing CLI's argument parsing lives in `AppArgs`. The desktop does not parse CLI args in this feature. If a "open this workspace from explorer" flow is wanted later, it goes through `App.xaml.cs` `OnStartup(StartupEventArgs)` and is a separate small feature.

---

## 8. What This Feature Does *Not* Build

For clarity (these are the boundaries reviewers should hold the work to):

- No screens from mockups M1–M8.
- No workspace JSON schema, no JSON loader, no engine JSON consumer.
- No connection editor, no test-connection logic.
- No graph view, no graph library reference.
- No dry-run, no execution path.
- No icon, no installer, no signing.
- No keyboard shortcuts, no menu items beyond the default `File → Exit`.
