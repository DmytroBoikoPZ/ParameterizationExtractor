# 05 — desktop-seed-tab — SeedViewModel (multi-script container) + WorkspaceConnectionStringBuilder

## Goal

Land `SeedViewModel` — the multi-script container that hydrates from `Workspace.Package.Scripts`, manages the `ObservableCollection<ScriptEditorViewModel>`, and surfaces Add/Remove commands. Also lift `BuildConnectionString` from `ConnectionEditorViewModel` into a shared static helper so both consumers (connection editor + seed tab) share the call. **Still no view, no `MainWindowViewModel` changes** — step 06.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- `ScriptEditorViewModel` (step 04).
- [`Controls/ConnectionEditor/ConnectionEditorViewModel.cs`](../../ParameterizationExtractor.Desktop/Controls/ConnectionEditor/ConnectionEditorViewModel.cs) — currently owns `BuildConnectionString`. Refactor target.
- [`Views/Welcome/WelcomeViewModel.cs`](../../ParameterizationExtractor.Desktop/Views/Welcome/WelcomeViewModel.cs) — pattern exemplar for the `Bind(Func<...>)` parent-handler pattern.

## What to Build

### `Services/Workspace/WorkspaceConnectionStringBuilder.cs` (extraction)

```csharp
namespace Quipu.ParameterizationExtractor.Desktop.Services.Workspace;

internal static class WorkspaceConnectionStringBuilder
{
    /// <summary>
    /// Composes a SQL Server connection string from a workspace's <see cref="WorkspaceSource"/>
    /// and the (decrypted) plaintext password. Single call site for all desktop consumers
    /// (connection editor + seed tab) so the assembly logic stays consistent.
    /// </summary>
    public static string Build(WorkspaceSource source, string? plaintextPassword)
    {
        // Exception to the "no Microsoft.Data.SqlClient outside Logic" tripwire:
        // SqlConnectionStringBuilder is parameter quoting, not SQL execution. Single call site.
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = source.Server,
            InitialCatalog = source.Database,
            TrustServerCertificate = true,
        };
        if (string.Equals(source.Auth, "windows", StringComparison.OrdinalIgnoreCase))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = source.User ?? string.Empty;
            builder.Password = plaintextPassword ?? string.Empty;
        }
        return builder.ConnectionString;
    }
}
```

### Refactor `ConnectionEditorViewModel.BuildConnectionString` → delegate

`ConnectionEditorViewModel.BuildConnectionString` now reads:

```csharp
public string BuildConnectionString()
{
    var pseudoSource = new WorkspaceSource
    {
        Server = Server,
        Database = Database,
        Auth = IsWindowsAuth ? "windows" : "sql",
        User = IsWindowsAuth ? null : User,
    };
    return WorkspaceConnectionStringBuilder.Build(pseudoSource, IsWindowsAuth ? null : Password);
}
```

Existing tests for `ConnectionEditorViewModel.BuildConnectionString` continue to pass (the helper produces the same string).

### `Views/Seed/SeedViewModel.cs`

`internal sealed partial class SeedViewModel : ObservableObject`. Constructor:

```csharp
public SeedViewModel(
    IDatabaseExplorer explorer,
    IUiDispatcher ui,
    IDialogService dialog,
    ILoggerFactory loggerFactory)
```

Why `ILoggerFactory` — the container creates `ScriptEditorViewModel` instances dynamically and each needs its own `ILogger<ScriptEditorViewModel>`. Resolve via `loggerFactory.CreateLogger<ScriptEditorViewModel>()` once at construction (Factory pattern is OK — `ILoggerFactory` is the standard MS DI shape).

**Members:**
- `public ObservableCollection<ScriptEditorViewModel> Scripts { get; } = new();`
- `[ObservableProperty] private ScriptEditorViewModel? _selectedScript;`
- `public bool HasScripts => Scripts.Count > 0;` — wire INPC via `Scripts.CollectionChanged += (_,_) => OnPropertyChanged(nameof(HasScripts));`
- Internal mutable state:
  - `private WorkspaceModel? _workspace;`
  - `private string? _connectionString;`
  - `private Func<Task>? _saveCallback;`

**`Bind(Func<Task> saveCallback)`** — parent (MainWindowViewModel in step 06) wires the save handler before calling `Hydrate`. Mirrors `WelcomeViewModel.Bind`.

**`public void Hydrate(WorkspaceModel? workspace, string? plaintextPassword)`** — primary public entry:
1. Cancel any pending child saves (defensive — though the children own their own debounce).
2. `Scripts.Clear(); SelectedScript = null;`
3. If `workspace == null`: `_workspace = null; _connectionString = null;` and return.
4. `_workspace = workspace; _connectionString = WorkspaceConnectionStringBuilder.Build(workspace.Source, plaintextPassword);`
5. For each `s in workspace.Package.Scripts`: build a `ScriptEditorViewModel(s, _explorer, _ui, _saveCallback ?? throw, _loggerFactory.CreateLogger<ScriptEditorViewModel>())`; call `child.HydrateFromSource(); child.Initialize(_connectionString);`; `Scripts.Add(child);`.
6. `SelectedScript = Scripts.FirstOrDefault();`

**Commands:**

`[RelayCommand(CanExecute = nameof(CanMutate))] private void AddScript()`:
- `if (_workspace is null) return;`
- `var s = new SourceForScript { ScriptName = NextScriptName() }; _workspace.Package.Scripts.Add(s);`
- Build `ScriptEditorViewModel`; hydrate + initialize; `Scripts.Add(vm); SelectedScript = vm;`
- Trigger save: `_ = _saveCallback!();` (parent decides how to handle async errors).

`NextScriptName()`: produces `"NewScript"`, `"NewScript (2)"`, `"NewScript (3)"`, …  unique against current `Scripts` names.

`[RelayCommand(CanExecute = nameof(CanRemoveSelected))] private async Task RemoveSelectedScriptAsync()`:
- `var target = SelectedScript; if (target is null || _workspace is null) return;`
- (No confirm dialog v1 — keep scope tight; deferred follow-up.)
- `_workspace.Package.Scripts.Remove(target.SourceForScriptRef);` — but `ScriptEditorViewModel` would need to expose the underlying `SourceForScript`. Decision: add `internal SourceForScript Source => _source;` getter to `ScriptEditorViewModel` for this single use case (also makes `WriteThroughToSource` more transparent for tests). Update step 04's spec via `## Notes` deviation log.
- `Scripts.Remove(target);`
- `SelectedScript = Scripts.FirstOrDefault();`
- `await _saveCallback!();`

`bool CanMutate() => _workspace is not null;`
`bool CanRemoveSelected() => _workspace is not null && SelectedScript is not null;`

Notify CanExecute via `[NotifyCanExecuteChangedFor]` on `_selectedScript` and the workspace setter (well, `_workspace` is a private field — so manually call `AddScriptCommand.NotifyCanExecuteChanged()` and `RemoveSelectedScriptCommand.NotifyCanExecuteChanged()` inside `Hydrate` after assigning `_workspace`).

**Renaming:** done via `SelectedScript.ScriptName` two-way binding from the view's ListBox item template (no separate command needed; the ScriptEditorVM's save-on-blur path handles persistence).

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- `services.AddSingleton<SeedViewModel>();`

`MainWindowViewModel` will inject this in step 06.

### Tests

Per `prompts/execution.md` — CODE: TDD cycle.

New file `Tests/Desktop/SeedViewModelTests.cs`:

Setup: builds `SeedViewModel` with `FakeDatabaseExplorer`, `FakeUiDispatcher`, `FakeDialogService`, and a `LoggerFactory.Create(b => {})` for the `ILoggerFactory`. A captured `Func<Task>` for the save callback increments a counter.

1. `Default_State_NoScripts_HasScriptsFalse`.
2. `Bind_BeforeHydrate_NoErrors`.
3. `Hydrate_NullWorkspace_ClearsScripts`.
4. `Hydrate_WithEmptyPackage_NoScripts_SelectedScriptNull`.
5. `Hydrate_WithTwoScripts_PopulatesCollection_SelectsFirst`.
6. `Hydrate_TwiceWithDifferentWorkspaces_RebuildsCollection`.
7. `AddScript_AppendsToWorkspaceAndCollection_AndSelectsNew`.
8. `AddScript_GeneratesUniqueDefaultName` — call Add twice; assert names are `"NewScript"` and `"NewScript (2)"`.
9. `AddScript_TriggersSaveCallback`.
10. `AddScript_NoWorkspaceLoaded_CanExecuteFalse`.
11. `RemoveSelectedScript_RemovesFromBoth_UpdatesSelection_TriggersSave`.
12. `RemoveSelectedScript_NoSelection_CanExecuteFalse`.
13. `Hydrate_BuildsConnectionStringAndPassesToChildren` — assert `FakeDatabaseExplorer.ConnectionStringsCalled` is non-empty after a child runs `Preview` (chains through `child.Initialize`).

**`WorkspaceConnectionStringBuilder` tests** — small file:

New file `Tests/Desktop/Workspace/WorkspaceConnectionStringBuilderTests.cs`:

14. `Build_WindowsAuth_IncludesIntegratedSecurity_ExcludesPasswordToken`.
15. `Build_SqlAuth_IncludesUserAndPasswordTokens`.
16. `Build_NullPassword_TreatedAsEmpty`.

DI smoke (existing `HostCompositionTests.cs`):
17. `DesktopHost_ResolvesSeedViewModel`.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-04 N + 17 = N + 17, all green.
- All existing `ConnectionEditorViewModelTests` continue to pass (the `BuildConnectionString` refactor is behaviour-preserving).

## Acceptance Criteria

- [ ] `Services/Workspace/WorkspaceConnectionStringBuilder.cs` exists; `internal static`; carries the canonical SqlClient-exception comment.
- [ ] `ConnectionEditorViewModel.BuildConnectionString` delegates to the helper. Existing tests still pass.
- [ ] `Views/Seed/SeedViewModel.cs` exists; `internal sealed partial`; `Bind` + `Hydrate` + `AddScriptCommand` + `RemoveSelectedScriptCommand`.
- [ ] `ScriptEditorViewModel.Source` getter (internal) added so the container can remove by reference. Logged in checklist Notes as a deviation from step 04's published spec.
- [ ] `SeedViewModel` registered Singleton.
- [ ] `Hydrate` builds the connection string via the new helper and propagates to each child via `Initialize`.
- [ ] `AddScript` generates unique names against the current collection.
- [ ] All 17 new tests pass.
- [ ] No tripwires introduced.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § How to add a screen.
- Pattern exemplars: `Views/Welcome/WelcomeViewModel.cs` (Bind pattern), `Controls/ConnectionEditor/ConnectionEditorViewModel.cs` (BuildConnectionString refactor target).
- Depends on: [04 — ScriptEditorViewModel](./04-desktop-seed-tab-script-editor-vm.md).
