# 04 — desktop-connection-management — NewWorkspaceDialog + Edit-connection flow + wire-up

## Goal

Wrap `ConnectionEditor` (step 03) in a modal `NewWorkspaceDialog` (`MetroWindow`); extend `IDialogService` with two typed methods (`ShowNewWorkspaceDialogAsync` + `ShowEditConnectionDialogAsync`); flip Welcome's `[ New workspace... ]` and the File menu's `New workspace...` from disabled to live; replace the static `ConnectionIndicator` `TextBlock` in the toolbar with a clickable button that opens the dialog in **edit mode**; on edit-confirm, save back to the loaded `.bws`. End of step: feature is fully usable; the disabled buttons from `desktop-startup-and-open-workspace` are gone.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- `IConnectionTester`, `IPasswordProtector`, `ConnectionEditor` control + VM (steps 01-03).
- [`ParameterizationExtractor.Desktop/Services/Dialogs/IDialogService.cs`](../../ParameterizationExtractor.Desktop/Services/Dialogs/IDialogService.cs) + impl — current shape: `ShowMessageAsync` / `ConfirmAsync` / `OpenFileAsync` / `SaveFileAsync`.
- [`ParameterizationExtractor.Desktop/Views/Welcome/WelcomeViewModel.cs`](../../ParameterizationExtractor.Desktop/Views/Welcome/WelcomeViewModel.cs) — `IsNewWorkspaceEnabled` returns `false` (locked). `[ New workspace... ]` button bound to `IsNewWorkspaceEnabled` + `NewWorkspaceTooltip`.
- [`ParameterizationExtractor.Desktop/MainWindow.xaml`](../../ParameterizationExtractor.Desktop/MainWindow.xaml) — File menu `New workspace... IsEnabled="False"`. Toolbar `ConnectionIndicator` is a `TextBlock` bound to `ConnectionDisplay`.
- [`ParameterizationExtractor.Desktop/MainWindowViewModel.cs`](../../ParameterizationExtractor.Desktop/MainWindowViewModel.cs) — has `OpenWorkspaceAsync`, `OpenCommand`, `CloseCommand`, `ExitCommand`. Workspace replacement strategy already triggers `[NotifyPropertyChangedFor]` chain.
- [`ParameterizationExtractor.Desktop/Services/Workspace/IWorkspaceStore.cs`](../../ParameterizationExtractor.Desktop/Services/Workspace/IWorkspaceStore.cs) has `SaveAsync`.
- [`Tests/Desktop/Fakes/FakeDialogService.cs`](../../Tests/Desktop/Fakes/FakeDialogService.cs) — extend.

## What to Build

### `Dialogs/NewWorkspace/NewWorkspaceDialog.xaml(.cs)`

A `MetroWindow` (NOT a `UserControl`) — separate window, owner = main, `WindowStartupLocation = CenterOwner`, `ShowInTaskbar = false`, `ResizeMode = NoResize`, `WindowStyle = ToolWindow`-ish via MahApps options.

- `internal partial class NewWorkspaceDialog : MetroWindow`.
- Layout (matches [M2 mockup](../../docs/design/desktop-ui/02-new-workspace.md)):
  - Top section (collapsed in edit mode):
    - "Workspace name:" textbox bound to `WorkspaceName`.
    - "Save to:" file-path picker (inline: textbox + browse button; `Browse` is a `[RelayCommand]` on the dialog VM that calls `IDialogService.SaveFileAsync` with the `.bws` filter).
  - Separator + "Source connection" group:
    - Embedded `<connEditor:ConnectionEditorView />` bound to `ConnectionEditor` child VM.
  - Bottom button row:
    - `Cancel` button → `CancelCommand` → sets `Result = null`, closes.
    - `Create workspace` (or `Save` in edit mode) button → `ConfirmCommand`.

- Code-behind: only `InitializeComponent()` + `protected override void OnClosed` to dispose the VM if needed (transient — likely not needed).

### `Dialogs/NewWorkspace/NewWorkspaceDialogViewModel.cs`

`internal sealed partial class NewWorkspaceDialogViewModel : ObservableObject`. Constructor takes `ConnectionEditorViewModel editor, IWorkspaceStore store, IPasswordProtector protector, IDialogService dialog, ILogger<...> log`.

| Member | Behaviour |
|---|---|
| `ConnectionEditor` | child VM (constructor-injected) |
| `WorkspaceName` | `[ObservableProperty]` |
| `SavePath` | `[ObservableProperty]` (full path including filename) |
| `Mode` | enum `New / Edit` (set via init method) |
| `IsNewMode` / `IsEditMode` | computed from `Mode` |
| `Title` | `"New workspace" / "Edit connection"` |
| `ConfirmButtonText` | `"Create workspace" / "Save"` |
| `Result` | `NewWorkspaceResult? / WorkspaceSource?` (typed by mode; expose as `object?` field, two typed accessors) |
| `BrowseSavePathCommand` | `[RelayCommand]` — calls `_dialog.SaveFileAsync("Save workspace as", ".bws filter", defaultName: WorkspaceName + ".bws")` |
| `ConfirmCommand` | `[RelayCommand]` — see flow below |
| `CancelCommand` | `[RelayCommand]` — closes the dialog with no Result |

`ConfirmCommand` flow:

```
(For New mode)
  if string.IsNullOrWhiteSpace(WorkspaceName) || string.IsNullOrWhiteSpace(SavePath):
      ShowMessageAsync inline error → return
  Run a Test if it hasn't been run yet (TestStatus == Idle):
      await ConnectionEditor.TestCommand.ExecuteAsync(null)
      if TestStatus != Success:
          → don't proceed; user must Test successfully
  Build WorkspaceModel with Source from ConnectionEditor.ToWorkspaceSource(),
        empty Package, default GlobalExtractConfiguration
  await _store.SaveAsync(model, SavePath)
  Result = new NewWorkspaceResult(SavePath)
  CloseDialog(true)

(For Edit mode)
  Run a Test if it hasn't been run yet — same gate.
  Result = ConnectionEditor.ToWorkspaceSource()
  CloseDialog(true)
```

`InitForNew()` and `InitForEdit(WorkspaceSource current, string? plaintextPassword)` methods set `Mode` and pre-populate.

### Extend `IDialogService`

```csharp
internal interface IDialogService
{
    // existing 4 methods
    Task ShowMessageAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> OpenFileAsync(string title, string filter);
    Task<string?> SaveFileAsync(string title, string filter, string? defaultName = null);

    // NEW
    Task<NewWorkspaceResult?> ShowNewWorkspaceDialogAsync();
    Task<WorkspaceSource?> ShowEditConnectionDialogAsync(WorkspaceSource current, string? plaintextPassword);
}
```

`record NewWorkspaceResult(string Path);` — lives next to the interface.

`DialogService` impl:

- Both `Show*Async` methods construct `NewWorkspaceDialog` from DI (a factory delegate `Func<NewWorkspaceDialog>` — registered as a transient + factory in `DesktopHost.cs`). Set the appropriate `Init*` mode + owner = `Application.Current.MainWindow`. Call `ShowDialog()` (synchronous; per WPF). Wrap in a `Task.FromResult` of the `Result` (or null on cancel).

Alternative: register `NewWorkspaceDialog` as transient in DI; `IDialogService` resolves via a `IServiceProvider`/`IServiceScopeFactory`. **Pick the factory delegate approach** — keeps the service-locator anti-pattern off the table.

### `MainWindowViewModel` extensions

- Constructor gains nothing — `IDialogService` is already injected.
- New commands:
  - `[RelayCommand] private async Task NewWorkspaceAsync()`:
    1. `var result = await _dialog.ShowNewWorkspaceDialogAsync();`
    2. `if (result is not null) await OpenWorkspaceAsync(result.Path);`
  - `[RelayCommand(CanExecute = nameof(CanEditConnection))] private async Task EditConnectionAsync()`:
    1. Decrypt the current password if stored: `var pwd = !string.IsNullOrEmpty(Workspace!.Source.PasswordEncrypted) ? _protector.Unprotect(Workspace.Source.PasswordEncrypted) : null;` (with try/catch on `CryptographicException` → `null`).
    2. `var updated = await _dialog.ShowEditConnectionDialogAsync(Workspace.Source, pwd);`
    3. `if (updated is null) return;`
    4. `Workspace = Workspace.WithSource(updated);` (or new instance with copied properties — see step's "Add `WithSource` helper" item below).
    5. `await _store.SaveAsync(Workspace, _currentPath);`
  - `bool CanEditConnection() => IsWorkspaceLoaded;`
- Add `_currentPath` field — needed for `SaveAsync`. Wire its assignment in `OpenWorkspaceAsync` (right after a successful load): `_currentPath = path;`. Reset to `null` on `Close`.
- Inject `IPasswordProtector` to ctor (1 new param).

### `WorkspaceModel.WithSource` helper

In `ParameterizationExtractor.Desktop/Services/Workspace/WorkspaceModel.cs`:

```csharp
internal WorkspaceModel WithSource(WorkspaceSource source) =>
    new()
    {
        Version = this.Version,
        Name = this.Name,
        Source = source,
        Global = this.Global,
        Package = this.Package
    };
```

**Decision recap from feature-architecture § 9:** record-with-mutation is preferred over property setting; the helper keeps the call site terse without converting `WorkspaceModel` to a record (which would risk INPC churn).

### `WelcomeViewModel` flips

- Change `IsNewWorkspaceEnabled` from `=> false` to a real `[ObservableProperty]` initialised `true`.
- Remove `NewWorkspaceTooltip` (binding now no-ops or display "Create a new workspace and connect to a database"). Pick: keep a friendly tooltip, just rephrase.
- Add `[RelayCommand] private async Task NewWorkspaceAsync()` — same body as `MainWindowViewModel.NewWorkspaceAsync` but routes through the existing `_openHandler` path:
  ```csharp
  EnsureBound();
  var result = await _dialog.ShowNewWorkspaceDialogAsync();
  if (result is not null) await _openHandler!(result.Path);
  ```
- Bind the [ New workspace... ] button: `Command="{Binding NewWorkspaceCommand}"` (replace `IsEnabled` binding).

### `MainWindow.xaml` — content tweaks

- File menu's `New workspace...` MenuItem: remove `IsEnabled="False"`, add `Command="{Binding NewWorkspaceCommand}"`.
- Toolbar `ConnectionIndicator`: replace the `<TextBlock>` with a `<Button>` styled to look like a pill (no chrome by default; use a subtle border or the MahApps `MetroFlatButton` style). Bind `Command="{Binding EditConnectionCommand}"`. Hover shows a tooltip "Edit connection". Visible only when `IsWorkspaceLoaded` (already gated by the parent toolbar).

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- `services.AddTransient<NewWorkspaceDialog>();`
- `services.AddTransient<NewWorkspaceDialogViewModel>();`

### Tests

Per `prompts/execution.md` — CODE: TDD cycle.

#### Extend `FakeDialogService`

Add `EnqueueNewWorkspaceResponse(NewWorkspaceResult?)` + `EnqueueEditConnectionResponse(WorkspaceSource?)` queues. Update `Calls` records to include the call type.

#### `Tests/Desktop/NewWorkspaceCommandTests.cs` (new)

1. `WelcomeViewModel_NewWorkspaceCommand_DialogReturnsResult_BubblesPathToOpenHandler` — fake returns `NewWorkspaceResult("C:\\new.bws")`; assert `_openCalls` contains that path.
2. `WelcomeViewModel_NewWorkspaceCommand_DialogReturnsNull_DoesNotCallHandler`.
3. `MainWindowViewModel_NewWorkspaceCommand_DialogReturnsResult_OpensWorkspace` — fake returns a valid path that contains a real `.bws` (use a temp file); assert `Workspace` is non-null afterwards.
4. `MainWindowViewModel_NewWorkspaceCommand_DialogReturnsNull_LeavesWorkspaceUnchanged`.

#### `Tests/Desktop/EditConnectionFlowTests.cs` (new)

5. `EditConnectionCommand_NoWorkspace_CanExecuteFalse` — start with `Workspace = null`; assert `EditConnectionCommand.CanExecute(null)` is false.
6. `EditConnectionCommand_DialogReturnsUpdated_WorkspaceSourceReplacedAndSaved` — load sample.bws → trigger Edit → fake returns updated `WorkspaceSource { Server = "new.host" }` → assert `Workspace.Source.Server == "new.host"` AND `_store.SaveAsync` was called with the current path. (Capture via a small fake `IWorkspaceStore` test double that records the last `SaveAsync` call.)
7. `EditConnectionCommand_DialogReturnsNull_NoSaveCalled`.
8. `EditConnectionCommand_StoredPasswordPresent_DecryptsBeforeOpeningDialog` — load workspace whose `PasswordEncrypted` is the in-memory protector's encoding of `"hunter2"`; trigger Edit; assert `FakeDialogService.Calls` last `EditConnection` entry was constructed with `plaintextPassword == "hunter2"`. (Add a "captured plaintext" field to the fake's call record.)
9. `EditConnectionCommand_DecryptThrowsCryptographic_PassesNullPlaintextToDialog` — store an arbitrary corrupt ciphertext; assert plaintextPassword passed to dialog is `null`; assert the open path didn't crash.

#### `NewWorkspaceDialogViewModelTests.cs` (new)

10. `Confirm_NewMode_TestNotRun_RunsTestFirst_AbortsOnFailure` — `FakeConnectionTester` enqueues failure; assert `Result` stays null and dialog stays open.
11. `Confirm_NewMode_AfterSuccessfulTest_WritesSkeletonAndReturnsPath` — `FakeConnectionTester` enqueues success; valid name + temp path; assert file is written + `Result.Path == path`.
12. `Confirm_EditMode_AfterSuccessfulTest_ReturnsUpdatedSource` — pre-populate via `InitForEdit`; tweak fields; confirm; assert `Result` is the updated `WorkspaceSource`.

#### DI smokes

13. `DesktopHost_ResolvesNewWorkspaceDialog` (Transient — resolve twice, assert different).
14. `DesktopHost_ResolvesNewWorkspaceDialogViewModel`.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-03 118 + ~14 new = **~132 tests**, all green. (Exact count depends on which behavioural splits surface during impl.)
- **Manual smoke (mandatory)**:
  - Launch desktop, no args → Welcome view. Click `[ New workspace... ]` → dialog opens. Fill name + path + connection (use the test DB's connection details from `appsettings.test.json`); click Test → status shows table/FK counts; click Create workspace → dialog closes, MainWindow loads the new workspace, Overview tab populates.
  - In the loaded workspace, click the ConnectionIndicator pill → dialog opens in Edit mode (name/path collapsed, fields pre-populated). Change the password → Save → dialog closes; `ConnectionDisplay` text refreshes; `.bws` file modification time updates.
  - Cancel during a Test → status reverts to "Cancelled"; button reverts to "Test".
  - Close + reopen the same `.bws` → password still works (DPAPI round-trip).

## Acceptance Criteria

- [ ] `Dialogs/NewWorkspace/{NewWorkspaceDialog.xaml(.cs), NewWorkspaceDialogViewModel.cs}` exist.
- [ ] `IDialogService` extended with `ShowNewWorkspaceDialogAsync` + `ShowEditConnectionDialogAsync` + `record NewWorkspaceResult`.
- [ ] `DialogService` impls open the dialog via a constructor-injected `Func<NewWorkspaceDialog>` factory (no `IServiceProvider` lookups in the impl body).
- [ ] `WelcomeViewModel.IsNewWorkspaceEnabled = true`; `NewWorkspaceCommand` bound; tooltip rephrased.
- [ ] `MainWindowViewModel` gains `NewWorkspaceCommand`, `EditConnectionCommand`, `_currentPath`; `EditConnectionCommand.CanExecute` gated on `IsWorkspaceLoaded`.
- [ ] `MainWindow.xaml` File menu `New workspace...` no longer disabled; `ConnectionIndicator` is a clickable button.
- [ ] `WorkspaceModel.WithSource` helper exists.
- [ ] On edit-confirm: `_store.SaveAsync` is called with the current path; `Workspace` is replaced with a new instance so INPC fires.
- [ ] `FakeDialogService` extended; `FakeWorkspaceStore` exists for save-call capture.
- [ ] All new tests pass.
- [ ] No `MessageBox.Show`, no `IConfiguration[..]`, no `Console.WriteLine`, no `Dispatcher.Invoke`, no `TODO`/`FIXME` introduced.
- [ ] Manual smoke verified: Create new workspace + connection test + Edit connection + DPAPI round-trip across launches. Logged in checklist `## Notes`.
- [ ] `dotnet build` 0 errors; `dotnet test` ~132/~132 pass.

## References

- ADR: [`adr/010-desktop-password-at-rest.md`](../../adr/010-desktop-password-at-rest.md).
- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § Dialogs / file pickers.
- Mockups: [M2](../../docs/design/desktop-ui/02-new-workspace.md), [M3 ConnectionIndicator](../../docs/design/desktop-ui/03-shell.md).
- Pattern exemplars in code: [`Views/Welcome/`](../../ParameterizationExtractor.Desktop/Views/Welcome/), [`MainWindowViewModel.cs`](../../ParameterizationExtractor.Desktop/MainWindowViewModel.cs).
- Depends on: [`01 — engine seam`](./01-desktop-connection-management-engine-seam-and-adr.md), [`02 — DPAPI`](./02-desktop-connection-management-dpapi.md), [`03 — ConnectionEditor`](./03-desktop-connection-management-connection-editor.md).
