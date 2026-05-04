# Desktop Connection Management

## Goal

Land the connection-management surface that the prior features deferred: DPAPI-encrypted password storage with an opt-out checkbox (ADR-010), a reusable `ConnectionEditor` control, the [M2 New-workspace dialog](../docs/design/desktop-ui/02-new-workspace.md), and an Edit-connection flow that writes back to the loaded `.bws`. The disabled `[ New workspace... ]` button in Welcome and the `New workspace...` File menu item both go live; the read-only ConnectionIndicator pill on the toolbar becomes clickable. First feature to talk to SQL Server through `Logic` (no `SqlConnection` ever crosses the Desktop boundary — see CLAUDE.md tripwire).

## Scope

- **In scope:**
  - **ADR-010 — password-at-rest** — DPAPI `DataProtectionScope.CurrentUser` on save with an opt-out checkbox; locked at L6 of the design pass, formalised here. Records: status, context (regulatory / threat model), decision, consequences (no shared-machine portability, must re-enter on machine change).
  - **Engine connection-test seam** — `IConnectionTester` + `MSSQLConnectionTester` in `ParameterizationExtractor.Logic/Connectivity/`. Method: `Task<ConnectionTestResult> TestAsync(string connectionString, CancellationToken ct)`; result is `record ConnectionTestResult(bool Success, int TableCount, int FkCount, string? ErrorMessage)`. The Desktop never opens a `SqlConnection` itself — it builds a connection string and hands it to the engine. Connection timeout 10s (inner bound); `ct` is the outer cancellation channel.
  - **DPAPI password protector** — `Services/Security/{IPasswordProtector.cs, DpapiPasswordProtector.cs}` in Desktop. `Protect(plaintext) → base64`, `Unprotect(base64) → plaintext`. Uses `System.Security.Cryptography.ProtectedData.Protect/Unprotect` under `DataProtectionScope.CurrentUser`. `WorkspaceSource.PasswordEncrypted` (today's placeholder field) becomes the canonical storage; on load, decrypt to a runtime `Password` shadow property; on save, re-encrypt unless opt-out is set (in which case leave `PasswordEncrypted = null` and prompt every open).
  - **`Controls/ConnectionEditor/{ConnectionEditorView.xaml(.cs), ConnectionEditorViewModel.cs}`** — reusable UserControl per [`docs/design/desktop-ui/components.md`](../docs/design/desktop-ui/components.md#connectioneditor): server / database (textbox v1; `sys.databases` discovery deferred) / auth radio (Windows / SQL) / user / password / "Store credentials (DPAPI-encrypted in the workspace file)" checkbox / Test connection button + status. Auth radio drives field visibility (Windows hides user/password/store-checkbox). `[RelayCommand(IncludeCancelCommand = true)]` on `TestAsync` gives `TestCommand` + `TestCancelCommand`; the button morphs (Test ↔ Cancel) via a binding to `TestCommand.IsRunning`. Test-status states: idle / testing / success / failure (enum + value converter in the View).
  - **`NewWorkspaceDialog` (M2)** — modal `MetroWindow` hosting `ConnectionEditor` + workspace-name textbox + save-path picker (`PathPicker`-shaped — same components-map entry; trivial inline impl is fine, no extra UserControl unless we reuse). "Create workspace" runs the connection test (re-uses `TestCommand`'s path), then writes a skeleton `.bws` (workspace name + populated `WorkspaceSource` + empty `Package` + default `GlobalExtractConfiguration`) via `IWorkspaceStore.SaveAsync`, then loads it via `MainWindowViewModel.OpenWorkspaceAsync(path)`. "Cancel" closes without writing.
  - **`IDialogService.ShowNewWorkspaceDialogAsync(...)`** — typed extension to the recipe pre-spec. Returns `Task<NewWorkspaceResult?>` where `record NewWorkspaceResult(string Path)` is the path of the freshly-created `.bws` (or `null` on cancel). The `IDialogService` interface header note in the recipe gets updated in step 05 — don't fork into a per-feature dialog interface.
  - **Edit-connection flow** — clickable `ConnectionIndicator` pill in `MainWindow.xaml` toolbar (replaced from a static `TextBlock` to a styled `Button` bound to a new `EditConnectionCommand`). Opens the same `ConnectionEditor` dialog in **edit mode** (workspace-name + save-path fields read-only / collapsed; dialog title "Edit connection"; bottom button is "Save" not "Create workspace"). On confirm: in-place updates `Workspace.Source` fields, then `_store.SaveAsync(workspace, currentPath)`. **First save-back path on the desktop** — call out in Notes. No dirty-marker, no undo; the save is the confirm.
  - **Wire-up** — `WelcomeViewModel.IsNewWorkspaceEnabled` flips from a hardcoded `false` to a real bool (`true`); the [ New workspace... ] button gets a `NewWorkspaceCommand` (calls `_dialog.ShowNewWorkspaceDialogAsync` then bubbles via the existing `_openHandler`). File menu's `New workspace...` MenuItem `IsEnabled="False"` is removed and bound to the same command. ConnectionIndicator becomes a clickable button bound to `EditConnectionCommand` (only enabled when `IsWorkspaceLoaded`).
  - **Workspace-source change propagation** — when an edit replaces `Workspace.Source` content, `MainWindowViewModel.ConnectionDisplay` must re-fire. Decision: replace `Workspace` with a fresh `WorkspaceModel` instance (new `Source`) so the existing `[NotifyPropertyChangedFor]` chain fires. Keeps the VM observable contract clean.
  - **Tests** — engine: `MSSQLConnectionTesterTests` (success path with the test DB, failure path with bad credentials, cancellation observed). Desktop: `DpapiPasswordProtectorTests` (round-trip, tamper detection, scope is CurrentUser), `ConnectionEditorViewModelTests` (auth-radio toggles field visibility flags, Test triggers TesterFake, Cancel cancels, status transitions), `NewWorkspaceCommandTests` (Welcome's command opens dialog → on success bubbles path through openHandler), `EditConnectionFlowTests` (dialog returns mutated source → workspace replaced → SaveAsync called with current path), DI smoke for the three new services.
  - **Cross-doc updates** — `adr/readme.md` (index ADR-010); `docs/methodology/wpf-desktop.md` (new § "Security / password protection"; § Service abstractions IDialogService gains the `ShowNewWorkspaceDialogAsync` method; new tripwire — VMs never read `WorkspaceSource.PasswordEncrypted` directly, always through `IPasswordProtector`); `docs/architecture/overview.md` (Desktop row mentions DPAPI; new § 5 row for `IConnectionTester`); `docs/roadmap.md` (move feature to Completed; promote next item to Proposed next).

- **Out of scope:**
  - **`sys.databases` dropdown discovery** for the Database field — free-form textbox v1; ride-in with a future feature if pain emerges.
  - **Save / Save-As / Save-As… / dirty-marker / unsaved-changes prompt** as a general feature. The save-back here is scoped to "edit connection confirmed" only.
  - **Connection presets / connection history** — recents are workspace-scoped (already in `desktop-startup-and-open-workspace`), not connection-scoped.
  - **`IUiDispatcher`** — still pre-spec; first impl ships with `desktop-seed-tab`. The connection-test continuation is on the awaited path; no marshalling needed.
  - **Migrating workspaces created without DPAPI** — the placeholder workspaces (`examples/sample.bws` and any test fixtures) keep `passwordEncrypted: null`; on load we treat null as "no stored credential, prompt-on-open" (out of scope: the prompt itself — for now, leaving it as a known gap surfaces no error because the engine just rejects with bad-auth).
  - **Re-encrypt on machine change** — DPAPI `CurrentUser` decrypt fails silently after a Windows-account change / machine move. We surface this through the existing dialog path (Test fails → user re-enters). No automatic "your credentials need refresh" UX.

- **Dependencies:**
  - [`desktop-skeleton`](./archive/desktop-skeleton-checklist.md) — Generic Host + DI + MahApps theming.
  - [`desktop-workspace-format`](./archive/desktop-workspace-format-checklist.md) — `IWorkspaceStore.SaveAsync`, `WorkspaceSource.PasswordEncrypted` placeholder.
  - [`desktop-shell`](./archive/desktop-shell-checklist.md) — `MainWindow.xaml` toolbar with the static ConnectionIndicator label + `MainWindowViewModel.ConnectionDisplay`.
  - [`desktop-startup-and-open-workspace`](./archive/desktop-startup-and-open-workspace-checklist.md) — `IDialogService` (we extend it), `WelcomeViewModel.IsNewWorkspaceEnabled` placeholder, File menu `New workspace...` placeholder.
  - Mockups [M2 New-workspace](../docs/design/desktop-ui/02-new-workspace.md) + [M3 shell](../docs/design/desktop-ui/03-shell.md) (ConnectionIndicator).
  - [`docs/design/desktop-ui/components.md`](../docs/design/desktop-ui/components.md) — `ConnectionEditor`, `PathPicker`, `ConnectionIndicator`.

## Architecture

See [feature-architecture.md](./desktop-connection-management/feature-architecture.md) for the engine seam, password-at-rest lifecycle, ConnectionEditor state machine, dialog flows (new vs edit), and the save-back path.

## Steps

- [x] [01 — ADR-010 + engine connection-test seam](./desktop-connection-management/01-desktop-connection-management-engine-seam-and-adr.md)
- [x] [02 — DPAPI password protector](./desktop-connection-management/02-desktop-connection-management-dpapi.md)
- [x] [03 — ConnectionEditor reusable control + VM](./desktop-connection-management/03-desktop-connection-management-connection-editor.md)
- [x] [04 — NewWorkspaceDialog + Edit-connection flow + wire-up](./desktop-connection-management/04-desktop-connection-management-new-and-edit-flows.md)
- [x] [05 — Cross-doc updates](./desktop-connection-management/05-desktop-connection-management-cross-docs.md)

## Notes

### 2026-05-03 — Step 01 (ADR-010 + engine connection-test seam) complete

**New files (Engine):**

- `ParameterizationExtractor.Logic/Connectivity/IConnectionTester.cs` — `public interface` + `public sealed record ConnectionTestResult`. `#nullable enable` at file level (Logic project doesn't have nullable enabled globally — same workaround as `Tests/Desktop/Fakes`).
- `ParameterizationExtractor.Logic/Connectivity/MSSQLConnectionTester.cs` — `public sealed`. `ConnectTimeout = 10s`. Counts user tables/FKs via `SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0` + same for `sys.foreign_keys` (filter on `is_ms_shipped = 0` to exclude system tables — minor refinement over the spec's bare `COUNT`). Catches `SqlException` / `InvalidOperationException` / `ArgumentException` (from `SqlConnectionStringBuilder`); lets `OperationCanceledException` propagate.

**New files (ADR + Tests):**

- `adr/010-desktop-password-at-rest.md` — formalises the L6 design lock. Threat model + alternatives + open follow-ups documented.
- `Tests/EngineConnectivityTests/MSSQLConnectionTesterTests.cs` — 5 integration tests against the test DB (`TestDbConfig.ResolveSourceConnectionString()`). Folder name `EngineConnectivityTests` matches the existing `EngineJsonTests/` precedent (step file said `Engine/Connectivity/`; chose existing convention).

**Modified files:**

- `adr/readme.md` — added ADR-010 to the index.
- `ParameterizationExtractor.Desktop/DesktopHost.cs` — registered `IConnectionTester` Singleton.
- `ParameterizationExtractor/AppBootstrap.cs` (CLI) — registered `IConnectionTester` Singleton in the `AddMSSQL` extension. Symmetric with Desktop.

**Verification:**

- `dotnet build` — 0 errors. (One pre-existing pass: cleared docfx incremental cache as usual.)
- `dotnet test` — **99/99 pass** (was 94; +5 exactly per projection).
- Tripwire grep across new files: 0 hits for `MessageBox.Show`, `IConfiguration[`, `Console.WriteLine`, `Thread.Sleep`, `.Result`, `.Wait()`, `Dispatcher.Invoke`, `TODO`, `FIXME`. `Microsoft.Data.SqlClient` types confined to `Logic/Connectivity/MSSQLConnectionTester.cs` (and the tests' `SqlConnectionStringBuilder` use for fixture construction in the bad-credentials test).
- `is_ms_shipped = 0` filter on the count queries: matches what an operator would consider "tables in their own model" — system tables would skew the headline number shown in the connection editor's status label.

### 2026-05-03 — Step 02 (DPAPI password protector) complete

**New files (Desktop):**

- `Services/Security/IPasswordProtector.cs` — `internal` interface; throws documented (`CryptographicException` from tampering / wrong-user; `FormatException` from non-base64).
- `Services/Security/DpapiPasswordProtector.cs` — `internal sealed`. `ProtectedData.Protect/Unprotect` with `DataProtectionScope.CurrentUser` and `optionalEntropy = null`. `Encoding.UTF8` for the byte/string bridge. No catches; exceptions propagate per the interface contract.

**New files (Tests):**

- `Tests/Desktop/Fakes/InMemoryPasswordProtector.cs` — base64 round-trip; not bound to user account.
- `Tests/Desktop/Security/DpapiPasswordProtectorTests.cs` — 6 tests: round-trip, salt non-determinism, tampering → `CryptographicException`, non-base64 → `FormatException`, empty string, Unicode (`пароль 🔒 P@ss`).

**Modified files:**

- `ParameterizationExtractor.Desktop/DesktopHost.cs` — registered `IPasswordProtector` Singleton.
- `Tests/Desktop/HostCompositionTests.cs` — added `DesktopHost_ResolvesIPasswordProtector`.

**Verification:**

- `dotnet build` — 0 errors.
- `dotnet test` — **106/106 pass** (was 99; +7 = 6 DPAPI + 1 DI smoke; exactly per projection).
- Tripwire grep: 0 hits across new files.

### 2026-05-03 — Step 03 (ConnectionEditor reusable control + VM) complete

**New files (Desktop):**

- `Controls/ConnectionEditor/ConnectionTestStatus.cs` — enum (Idle / Testing / Success / Failure / Cancelled).
- `Controls/ConnectionEditor/ConnectionEditorViewModel.cs` — `internal sealed partial`. `[RelayCommand(IncludeCancelCommand = true)]` on `TestAsync` generates `TestCommand` + `TestCancelCommand`. `IsWindowsAuth` ↔ `IsSqlAuth` mutually-exclusive radio binding (custom `IsSqlAuth` getter/setter delegates to `IsWindowsAuth`). `BuildConnectionString` uses `SqlConnectionStringBuilder` with the canonical exception comment + `TrustServerCertificate = true` to match the test DB's typical local-dev setup.
- `Controls/ConnectionEditor/ConnectionEditorView.xaml(.cs)` — UserControl, `x:ClassModifier="internal"`. SQL-auth fields toggle visibility on `IsSqlAuth` via `BoolToVis`. Test/Cancel button morph via complementary `Visibility` bindings on `IsTesting`.
- `Controls/ConnectionEditor/PasswordBoxBindingBehavior.cs` — attached property for two-way `PasswordBox.Password` binding (WPF doesn't expose it as a DP). Uses an `IsUpdating` flag to break feedback loops.

**New files (Tests):**

- `Tests/Desktop/Fakes/FakeConnectionTester.cs` — queue-driven (results + exceptions); records each `ConnectionStringsCalled`.
- `Tests/Desktop/ConnectionEditorViewModelTests.cs` — 12 tests covering default state, auth-radio coupling, Populate/ToWorkspaceSource round-trips (encrypts when `StoreCredentials = true`, leaves null otherwise; Windows-auth never stores credentials), Test command success/failure/cancellation, connection-string assembly.

**Modified files:**

- `ParameterizationExtractor.Desktop/DesktopHost.cs` — registered `ConnectionEditorViewModel` Transient (per recipe lifetime table for "dialog VMs").
- `Tests/Desktop/HostCompositionTests.cs` — added `DesktopHost_ResolvesConnectionEditorViewModel_AsTransient` (resolves twice, asserts different instances).

**Verification:**

- `dotnet build` — 0 errors.
- `dotnet test` — **119/119 pass** (was 106; +13 = 12 ConnectionEditor + 1 DI smoke; projection was +12, extra one because the auth-radio coupling spec was split into two tests for clarity — `IsSqlAuth_True_FlipsIsWindowsAuth` + `SwitchingToWindowsAuth_ClearsUserAndPassword`).
- `BuildConnectionString` exception to the "no `Microsoft.Data.SqlClient` outside Logic" tripwire is documented inline in the VM's `BuildConnectionString` method.
- Tripwire grep across new files: 0 hits for `MessageBox.Show`, `IConfiguration[`, `Console.WriteLine`, `Thread.Sleep`, `.Result`, `.Wait()`, `Dispatcher.Invoke`, `TODO`, `FIXME`, `Application.Current`. VM has no `using System.Windows*`.

### 2026-05-03 — Step 04 (NewWorkspaceDialog + Edit-connection flow + wire-up) complete

**New files (Desktop):**

- `Dialogs/NewWorkspace/{NewWorkspaceDialog.xaml(.cs), NewWorkspaceDialogViewModel.cs, NewWorkspaceResult.cs}` — modal `MetroWindow` hosting the reusable `ConnectionEditor`. The VM handles both `New` and `Edit` modes via a `Mode` enum + computed Title/ConfirmButtonText/IsNewMode/IsEditMode. Cancel + Confirm bubble through `CloseDialogWithResult` action set by the View — clean separation from WPF window types.

**Modified files (Desktop):**

- `Services/Dialogs/IDialogService.cs` — extended with `ShowNewWorkspaceDialogAsync` + `ShowEditConnectionDialogAsync`.
- `Services/Dialogs/DialogService.cs` — uses constructor-injected `Func<NewWorkspaceDialog>` factory (registered in DesktopHost) to construct dialogs without service-locator pattern. Sets `Owner` to `Application.Current.MainWindow`. Calls VM's `InitForNew` / `InitForEdit` then `ShowDialog()`; reads `CreatedResult` / `EditedSource` after.
- `Services/Workspace/WorkspaceModel.cs` — added `WithSource(WorkspaceSource)` helper that returns a fresh `WorkspaceModel`. Required so `Workspace = Workspace.WithSource(updated)` triggers the existing `[NotifyPropertyChangedFor]` chain.
- `MainWindowViewModel.cs` — full rewrite. Ctor now takes 7 params (added `IPasswordProtector`). New: `_currentPath` field; `NewWorkspaceCommand`, `EditConnectionCommand` (with `CanEditConnection = IsWorkspaceLoaded` gate). `OpenWorkspaceAsync` sets `_currentPath` on success. `Close` clears both `Workspace` and `_currentPath`. `EditConnectionAsync` decrypts the stored password (catching `CryptographicException` + `FormatException` → null), opens dialog, on confirm replaces Workspace + `_store.SaveAsync(Workspace, _currentPath)`.
- `Views/Welcome/WelcomeViewModel.cs` — `IsNewWorkspaceEnabled` flipped from `false` to `true`; `NewWorkspaceTooltip` rephrased; new `NewWorkspaceCommand` (`[RelayCommand]`) calls `_dialog.ShowNewWorkspaceDialogAsync()` then bubbles `result.Path` through the existing `_openHandler`.
- `Views/Welcome/WelcomeView.xaml` — `[ New workspace... ]` button gets `Command="{Binding NewWorkspaceCommand}"`.
- `MainWindow.xaml` — File menu's `New workspace...` MenuItem switched from `IsEnabled="False"` to `Command="{Binding NewWorkspaceCommand}"`. Toolbar `ConnectionIndicator` `<TextBlock>` replaced with a `<Button>` (transparent / no chrome / Cursor=Hand) bound to `EditConnectionCommand` with tooltip "Edit connection".
- `DesktopHost.cs` — registered `NewWorkspaceDialogViewModel` (Transient), `NewWorkspaceDialog` (Transient), and `Func<NewWorkspaceDialog>` factory (Singleton) for the DialogService factory.

**Modified files (Tests):**

- `Tests/Desktop/Fakes/FakeDialogService.cs` — extended with two new queues + recorders for the typed dialog methods. Records `EditConnectionCalls` separately from `Calls` so tests can assert the plaintext-password parameter passed to the dialog.
- `Tests/Desktop/Fakes/FakeWorkspaceStore.cs` — new `IWorkspaceStore` test double; in-memory dictionary + records every `SaveAsync` call.
- `Tests/Desktop/HostCompositionTests.cs` — added two DI smokes (`DesktopHost_ResolvesNewWorkspaceDialogViewModel_AsTransient`, `DesktopHost_ResolvesNewWorkspaceDialogFactory`); updated `DesktopHost_ResolvesWelcomeViewModel` (now expects `IsNewWorkspaceEnabled = true`).
- `Tests/Desktop/MainWindowViewModelOpenTests.cs` — updated `NewVm()` for the new 7-param ctor (passes `InMemoryPasswordProtector`).
- `Tests/Desktop/WelcomeViewModelTests.cs` — renamed regression-guard test to assert `IsNewWorkspaceEnabled = true` (was the `Locked_False` test).
- `Tests/Desktop/NewWorkspaceCommandTests.cs` (new) — 4 tests covering both VMs' command behaviour.
- `Tests/Desktop/EditConnectionFlowTests.cs` (new) — 5 tests: `CanExecute` gated on workspace, save-on-confirm, no-save-on-cancel, decrypt-before-dialog (passes plaintext), decrypt-failure-graceful (passes null).
- `Tests/Desktop/NewWorkspaceDialogViewModelTests.cs` (new) — 5 tests covering the VM's mode switching, gate-on-test, save in New mode, return-source in Edit mode, Cancel.

**Verification:**

- `dotnet build` — 0 errors.
- `dotnet test` — **135/135 pass** (was 119; +16 net = 4 NewWorkspaceCommand + 5 EditConnectionFlow + 5 NewWorkspaceDialogVM + 2 DI smokes; one existing test renamed in place). Step file projected ~132; actual is 135.
- Process smoke (programmatic):
  - `ParameterizationExtractor.Desktop.exe` (no args) → process alive after 2s → killed cleanly. Welcome view should show `[ New workspace... ]` enabled.
  - `ParameterizationExtractor.Desktop.exe Examples\sample.bws` → process alive after 2s → killed cleanly. Toolbar's connection pill should be clickable.
- **Pending**: full visual verification of New + Edit flows (open dialog, fill form, Test against the real test DB, Create / Save) needs the user.
- One spec deviation noted: confirm-without-test in New mode shows a generic "Run a successful Test connection before saving" dialog rather than auto-running the Test (step file said either is acceptable; explicit is clearer UX).
- Tripwire grep across new/modified files: 0 hits for `MessageBox.Show`, `IConfiguration[`, `Console.WriteLine`, `Thread.Sleep`, `.Result`, `.Wait()`, `Dispatcher.Invoke`, `TODO`, `FIXME`. `Application.Current` use confined to `MainWindowViewModel.Exit` (canonical comment), `DialogService` (canonical comment + new dialog factory call sites). `Microsoft.Data.SqlClient` (`SqlConnectionStringBuilder`) confined to `Controls/ConnectionEditor/ConnectionEditorViewModel.cs` (one call site, documented as exception).

### 2026-05-03 — Step 05 (cross-doc updates) complete

- `docs/architecture/overview.md` § 2 Desktop row + § 4 Workspace-files row + § 5 DI line + § 5 secret-management line + § 7 ADR list updated.
- `docs/methodology/wpf-desktop.md` `IDialogService` subsection now shows 6 methods (added `ShowNewWorkspaceDialogAsync` / `ShowEditConnectionDialogAsync`); implementation-timing table has the two new rows for `IConnectionTester` + `IPasswordProtector`; new § "Password protection" + § "Connection management" present; § How to add a screen lists three reference impls (`Views/Overview/`, `Views/Welcome/`, `Controls/ConnectionEditor/`).
- `docs/roadmap.md` — `desktop-connection-management` moved to ✅ Completed; `desktop-seed-tab` promoted to Proposed next + Desktop UI track top.
- `CLAUDE.md` gained one new tripwire line under `### WPF (Desktop)`: *"No `WorkspaceSource.PasswordEncrypted` reads from VMs or views — go through `IPasswordProtector` (ADR-010)."* Mirrored to `.github/copilot-instructions.md`. SHA256 of both files: `f0fe5df9b8fa4d99c25de32a13849e6814a07887098ac365a11b1513e8c52649` (equal).
- `dotnet build` 0 errors; `dotnet test` 135/135 (sanity).

## Final summary

The `desktop-connection-management` feature is complete: 5/5 steps, 135/135 tests green.

**Cumulative changes:**

- **2 new files** (Logic): `Connectivity/{IConnectionTester.cs, MSSQLConnectionTester.cs}` — engine-side seam + impl. Both `public`. `MSSQLConnectionTester` filters system tables (`is_ms_shipped = 0`).
- **9 new files** (Desktop): `Services/Security/{IPasswordProtector.cs, DpapiPasswordProtector.cs}`; `Controls/ConnectionEditor/{ConnectionEditorView.xaml(.cs), ConnectionEditorViewModel.cs, PasswordBoxBindingBehavior.cs, ConnectionTestStatus.cs}`; `Dialogs/NewWorkspace/{NewWorkspaceDialog.xaml(.cs), NewWorkspaceDialogViewModel.cs, NewWorkspaceResult.cs}`.
- **6 modified files** (Desktop): `Services/Dialogs/{IDialogService.cs, DialogService.cs}` (extended with typed dialogs + `Func<NewWorkspaceDialog>` factory); `Services/Workspace/WorkspaceModel.cs` (`WithSource` helper); `MainWindowViewModel.cs` (full rewrite — 7-param ctor, `EditConnectionCommand`, `NewWorkspaceCommand`); `MainWindow.xaml` (File menu wired, ConnectionIndicator → button); `Views/Welcome/{WelcomeViewModel.cs, WelcomeView.xaml}` (IsNewWorkspaceEnabled flipped, NewWorkspaceCommand bound); `DesktopHost.cs` (4 new registrations).
- **1 modified file** (CLI): `AppBootstrap.cs` (registered `IConnectionTester` symmetric with Desktop).
- **6 new test files**: `Tests/EngineConnectivityTests/MSSQLConnectionTesterTests.cs`, `Tests/Desktop/Security/DpapiPasswordProtectorTests.cs`, `Tests/Desktop/ConnectionEditorViewModelTests.cs`, `Tests/Desktop/NewWorkspaceCommandTests.cs`, `Tests/Desktop/EditConnectionFlowTests.cs`, `Tests/Desktop/NewWorkspaceDialogViewModelTests.cs`.
- **4 new test fakes** (`Tests/Desktop/Fakes/`): `InMemoryPasswordProtector`, `FakeConnectionTester`, `FakeWorkspaceStore`, plus extension to `FakeDialogService` (added queues + `EditConnectionCalls` recorder).
- **3 modified test files**: `HostCompositionTests.cs` (+5 DI smokes), `WelcomeViewModelTests.cs` (renamed regression guard), `MainWindowViewModelOpenTests.cs` (updated for 7-param ctor).
- **1 new ADR**: `adr/010-desktop-password-at-rest.md`; `adr/readme.md` index updated.
- **3 modified doc files**: `docs/architecture/overview.md`, `docs/methodology/wpf-desktop.md`, `docs/roadmap.md`.
- **1 new tripwire** in `CLAUDE.md` (mirrored to `.github/copilot-instructions.md`).

**Net test delta:** 94 → 135 (+41: 5 engine + 7 DPAPI + 13 ConnectionEditor + 4 NewWorkspaceCommand + 5 EditConnectionFlow + 5 NewWorkspaceDialogVM + 5 DI smokes − some renames absorbed).

**Pattern continued:** `Controls/ConnectionEditor/` is the third canonical View ↔ ViewModel pair (and the first reusable control per `components.md`); `Dialogs/NewWorkspace/` is the first modal dialog landed on the desktop. Both follow the recipe.

**Service abstractions:** `IDialogService` extended with typed dialogs; `IConnectionTester` + `IPasswordProtector` are now landed; `IUiDispatcher` remains pre-spec, ships with `desktop-seed-tab`.

**First save-back path** on the desktop: `EditConnectionCommand` → `_store.SaveAsync(Workspace, _currentPath)`. Save / Save-As as a general feature (with dirty-marker, prompts) is still future work.

**Open follow-ups (deliberately deferred):**

- `desktop-seed-tab` — first feature to use `IUiDispatcher`; first feature to consume the `IConnectionTester` for table discovery.
- Database dropdown discovery (`sys.databases`) deferred — free-form textbox is acceptable for v1.
- Re-encrypt-on-machine-change UX deferred — operator's first signal is failed Test, recovery is Edit-connection.
- General Save / Save-As feature deferred.

**Pending action:** archive needs your nod (per `prompts/execution.md` § Completion). Pair to move: `ongoing-tasks/desktop-connection-management-checklist.md` + `ongoing-tasks/desktop-connection-management/` → `ongoing-tasks/archive/`.

**Visual smoke pending:** the process-smokes proved the app launches and stays alive in both modes. Full UI interaction (open New-workspace dialog → fill → Test → Create → Overview loads + recents pushed; click ConnectionIndicator → Edit dialog opens with pre-populated fields → Save → workspace `.bws` modification time updates; close + reopen → password still works through DPAPI round-trip) needs your eyeball verification.

Free-form log of decisions, blockers, links to PRs. Dated entries preferred (YYYY-MM-DD).
