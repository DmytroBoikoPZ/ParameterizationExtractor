# 03 — desktop-connection-management — ConnectionEditor reusable control + VM

## Goal

Land the reusable `ConnectionEditor` UserControl + ViewModel that the New-workspace and Edit-connection dialogs (step 04) host. The control owns the connection-string assembly, the `IConnectionTester` invocation, and the cancellable test-status state machine. **Not yet hosted on a window** — step 04 wraps it in `NewWorkspaceDialog`. Step 03 produces a control that's instantiable + testable in isolation.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- `IConnectionTester` + `ConnectionTestResult` (from step 01).
- `IPasswordProtector` (from step 02).
- [`ParameterizationExtractor.Desktop/Views/Welcome/`](../../ParameterizationExtractor.Desktop/Views/Welcome/) — pattern exemplar for `View` ↔ `ViewModel` pairing (services-driven, command-bearing). The `ConnectionEditor` lives under `Controls/` (not `Views/`) because it's reusable across dialogs / tabs per [`docs/design/desktop-ui/components.md`](../../docs/design/desktop-ui/components.md).
- [`docs/design/desktop-ui/02-new-workspace.md`](../../docs/design/desktop-ui/02-new-workspace.md) — Pass 1 mockup (Windows-auth + SQL-auth variants).
- `CommunityToolkit.Mvvm` 8.4 source-gen `[RelayCommand(IncludeCancelCommand = true)]` — see [docs](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand).

## What to Build

### `Controls/ConnectionEditor/ConnectionEditorViewModel.cs`

`internal sealed partial class ConnectionEditorViewModel : ObservableObject`. Constructor takes `IConnectionTester` + `IPasswordProtector` + `ILogger<ConnectionEditorViewModel>`.

State enum (file-scoped or nested):

```csharp
internal enum ConnectionTestStatus { Idle, Testing, Success, Failure, Cancelled }
```

Observable members:

| Member | Type | Notes |
|---|---|---|
| `Server` | `string` | bound textbox; default `""` |
| `Database` | `string` | bound textbox (free-form v1); default `""` |
| `IsWindowsAuth` | `bool` | radio binding; setting it auto-clears `User`/`Password` and sets `IsSqlAuth = false` |
| `IsSqlAuth` | `bool` | radio binding; mutually exclusive with `IsWindowsAuth` |
| `User` | `string` | bound textbox (visible iff `IsSqlAuth`) |
| `Password` | `string` | bound `PasswordBox`-via-attached-behaviour; the VM holds plaintext in memory only |
| `StoreCredentials` | `bool` | default `true`; visible iff `IsSqlAuth` |
| `TestStatus` | `ConnectionTestStatus` | default `Idle`; `[ObservableProperty]` |
| `StatusMessage` | `string` | default `""`; populated after Test |
| `IsTesting` | computed `bool` | `TestStatus == Testing`; drives button `Test ↔ Cancel` morph |

Public methods:

- `void Populate(WorkspaceSource source, string? plaintextPassword)` — pre-fill all observable fields. Used by step 04 in edit mode.
- `WorkspaceSource ToWorkspaceSource()` — read all fields; if `StoreCredentials && IsSqlAuth && !string.IsNullOrEmpty(Password)`, encrypt via `IPasswordProtector.Protect(Password)` and write to `PasswordEncrypted`; else `PasswordEncrypted = null`. Returns a fresh `WorkspaceSource`.
- `string BuildConnectionString()` — uses `SqlConnectionStringBuilder` from `Microsoft.Data.SqlClient`. **Tripwire exception**: this builder type is NOT a SqlConnection / command and does not execute SQL — it's parameter quoting. Add a one-line comment marking the exception. Alternative considered: build the string in `Logic` and pass field-by-field; rejected as overkill for a half-dozen properties. **Single call site, Desktop-internal.**

Commands:

- `[RelayCommand(IncludeCancelCommand = true)]` on `private async Task TestAsync(CancellationToken ct)`:
  1. `TestStatus = ConnectionTestStatus.Testing; StatusMessage = "Testing…"`.
  2. `var connStr = BuildConnectionString();`
  3. `try { var result = await _tester.TestAsync(connStr, ct); ... }`
     - On `result.Success`: `TestStatus = Success; StatusMessage = $"OK Connected — {result.TableCount} tables, {result.FkCount} FKs";`.
     - On `!result.Success`: `TestStatus = Failure; StatusMessage = result.ErrorMessage ?? "Connection failed";`.
  4. `catch (OperationCanceledException) { TestStatus = Cancelled; StatusMessage = "Cancelled"; }`
  5. `_log.LogInformation("Connection test result {Status} for {Server}/{Database}", TestStatus, Server, Database);` — never log credentials.

Field-coupling partial methods (CTK.MVVM source-gen):

- `partial void OnIsWindowsAuthChanged(bool value)` — when set true, clear `User`, `Password`; set `IsSqlAuth = false`. Reverse on `IsSqlAuth`.

VMs never reference WPF types (mirror tripwire — no `using System.Windows*`).

### `Controls/ConnectionEditor/ConnectionEditorView.xaml` + `.xaml.cs`

- `UserControl`, `x:ClassModifier="internal"`, code-behind only `InitializeComponent()`.
- Layout matches [M2 mockup](../../docs/design/desktop-ui/02-new-workspace.md) "Source connection" group:
  - Server textbox (bound to `Server`).
  - Database textbox (bound to `Database`; v1 free-form).
  - Auth `RadioButton` pair bound to `IsWindowsAuth` / `IsSqlAuth`.
  - `User` + `Password` rows visible via `Visibility="{Binding IsSqlAuth, Converter={StaticResource BoolToVis}}"`.
  - `Password` field uses a `PasswordBox` plus a small attached behaviour for two-way binding (WPF's default `PasswordBox.Password` isn't bindable). Add `Controls/ConnectionEditor/PasswordBoxBindingBehavior.cs` — minimal attached property pattern. Tested by inclusion (no separate test file).
  - "Store credentials (DPAPI-encrypted in the workspace file)" `CheckBox` bound to `StoreCredentials`; visible only in SQL-auth.
  - Test button: content morphs `"Test connection" ↔ "Cancel"` via a value converter on `IsTesting` (or a `Style.Triggers` block — author's call).
  - Status label bound to `StatusMessage`; foreground driven by `TestStatus` enum + value converter (`Success` → green, `Failure` → red, `Cancelled` → muted, default → opacity 0.7).

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- `services.AddTransient<ConnectionEditorViewModel>();` — **transient**, NOT singleton. Each dialog opens a fresh editor; previous state must not leak. (Override per recipe lifetime table for "Dialog ViewModels (one-shot)".)

Add `Controls/ConnectionEditor/PasswordBoxBindingBehavior.cs` is auto-discovered by the SDK; no csproj edit.

### Tests

Per `prompts/execution.md` — CODE: TDD cycle.

New file `Tests/Desktop/ConnectionEditorViewModelTests.cs`. Use `FakeConnectionTester` (NEW — see below) + `InMemoryPasswordProtector` (from step 02).

#### `Tests/Desktop/Fakes/FakeConnectionTester.cs` (new)

- `internal sealed class FakeConnectionTester : IConnectionTester`.
- Queue-driven response list (mirrors `FakeDialogService`): `EnqueueResult(ConnectionTestResult)`, `EnqueueException(Exception)` (so cancellation can be simulated without an actual `OperationCanceledException` race).
- Records every call: `List<(string ConnectionString, bool Cancelled)> Calls`.
- `TestAsync(connStr, ct)`:
  - If `Calls`-coupled exception is enqueued, throw it after a `Task.Yield()`.
  - Else, observe `ct` if requested via a `[Test] await Task.Delay(...)`-pattern flag, then return the next result.
  - Throw if neither queue has anything (mirrors `FakeDialogService` discipline).

#### Tests

1. `Default_State_IsIdle_WindowsAuthOff_SqlAuthOff` — fresh VM has `TestStatus == Idle`, both auth flags `false`. (The dialog initialises one of them on Populate.)
2. `IsWindowsAuth_True_HidesUserAndPasswordViaConverters` — VM-level: assert `IsSqlAuth == false` after `IsWindowsAuth = true`. (View visibility is asserted indirectly through this VM signal; we don't instantiate the View.)
3. `Populate_WithSqlAuthSource_DecryptsAndPreFills` — pass a `WorkspaceSource { Auth="sql", User="sa", PasswordEncrypted="..." }` + plaintext via the explicit param; assert all fields populated.
4. `ToWorkspaceSource_StoreCredentialsTrue_EncryptsPassword` — set fields, call `ToWorkspaceSource`; assert `PasswordEncrypted != null` and round-trips back to the plaintext via `InMemoryPasswordProtector.Unprotect`.
5. `ToWorkspaceSource_StoreCredentialsFalse_LeavesPasswordEncryptedNull` — same but unchecked.
6. `ToWorkspaceSource_WindowsAuth_LeavesUserPasswordEmpty` — ensures Windows-auth path doesn't accidentally store credentials.
7. `TestCommand_Success_SetsStatusToSuccess_PopulatesCounts` — enqueue `ConnectionTestResult(true, 100, 200, null)`; execute; assert status enum + status message contains "100 tables, 200 FKs".
8. `TestCommand_Failure_SetsStatusToFailure_UsesErrorMessage` — enqueue `(false, 0, 0, "Login failed")`; assert status + message.
9. `TestCommand_Cancellation_SetsStatusToCancelled` — enqueue `OperationCanceledException`; execute; assert status `Cancelled`.
10. `TestCommand_BuildsConnectionStringFromFields` — assert `FakeConnectionTester.Calls[0].ConnectionString` contains `"Data Source=…"` matching `Server`. (Light coupling — we just check the builder's output isn't empty; finer assertions are brittle.)
11. `BuildConnectionString_DoesNotIncludePasswordWhenWindowsAuth` — assert no `Password=` token in the output for Windows-auth.

DI smoke (existing `HostCompositionTests.cs`): `DesktopHost_ResolvesConnectionEditorViewModel` — Transient, so resolve twice and assert different instances.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-02 106 + (11 editor + 1 DI smoke) = **118 tests**, all green.
- **Manual smoke** — not yet (no host). The control is exercised manually in step 04 once it's wrapped in `NewWorkspaceDialog`.

## Acceptance Criteria

- [ ] `Controls/ConnectionEditor/{ConnectionEditorView.xaml(.cs), ConnectionEditorViewModel.cs, PasswordBoxBindingBehavior.cs}` exist.
- [ ] `ConnectionEditorViewModel` is `internal sealed partial`. No `using System.Windows*` (grep verifies).
- [ ] `Populate` and `ToWorkspaceSource` round-trip a SQL-auth source with `StoreCredentials = true`.
- [ ] `TestAsync` is `[RelayCommand(IncludeCancelCommand = true)]`; CTK.MVVM generates both `TestCommand` and `TestCancelCommand`.
- [ ] `IConnectionTester` + `IPasswordProtector` are constructor-injected.
- [ ] `BuildConnectionString` uses `SqlConnectionStringBuilder` with the canonical exception comment; no manual concat.
- [ ] DI registration: `ConnectionEditorViewModel` Transient.
- [ ] All 11 ConnectionEditor VM tests + 1 DI smoke test pass.
- [ ] `FakeConnectionTester` + tests for it (mirror the `FakeDialogService` shape).
- [ ] No `MessageBox.Show`, no `IConfiguration[..]`, no `Console.WriteLine`, no `Dispatcher.Invoke`, no `Application.Current` in the VM.
- [ ] `dotnet build` 0 errors; `dotnet test` 118/118 pass.

## References

- ADR: [`adr/010-desktop-password-at-rest.md`](../../adr/010-desktop-password-at-rest.md).
- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § How to add a screen, § Service abstractions.
- Mockup: [`docs/design/desktop-ui/02-new-workspace.md`](../../docs/design/desktop-ui/02-new-workspace.md), components map [ConnectionEditor](../../docs/design/desktop-ui/components.md#connectioneditor).
- Pattern exemplars in code: [`Views/Welcome/`](../../ParameterizationExtractor.Desktop/Views/Welcome/), [`Tests/Desktop/Fakes/FakeDialogService.cs`](../../Tests/Desktop/Fakes/FakeDialogService.cs).
- Depends on: [`01 — engine seam`](./01-desktop-connection-management-engine-seam-and-adr.md), [`02 — DPAPI`](./02-desktop-connection-management-dpapi.md).
