# Desktop Connection Management — Architecture Overview

> First feature to drive a real `SqlConnection` from the Desktop (through `Logic`, never directly). Introduces password-at-rest (DPAPI ADR-010) and the first save-back path on the desktop (edit-connection → `IWorkspaceStore.SaveAsync`).

---

## 1. Pipeline / Integration

```
Two user-facing entry points share one ConnectionEditor:

(a) New workspace
    Welcome [ New workspace... ]  /  File menu New workspace...
       └→ MainWindowViewModel.NewWorkspaceCommand
            └→ IDialogService.ShowNewWorkspaceDialogAsync()
                 └→ NewWorkspaceDialog (modal MetroWindow)
                      ├── workspace-name textbox
                      ├── save-path picker
                      └── ConnectionEditor (reused)
                           └→ Test → IConnectionTester.TestAsync(connStr, ct)
                                       └→ MSSQLConnectionTester (Logic) opens SqlConnection
                                          counts tables + FKs
                                          returns ConnectionTestResult
                 ↓ Create workspace clicked
            ← NewWorkspaceResult { Path }     (or null on cancel)
       └→ MainWindowViewModel.OpenWorkspaceAsync(Path)
            └→ existing flow: IWorkspaceStore.SaveAsync(skeleton) → LoadAsync → Workspace = ...

(b) Edit connection
    MainWindow toolbar ConnectionIndicator (clickable button)
       └→ MainWindowViewModel.EditConnectionCommand
            └→ IDialogService.ShowEditConnectionDialogAsync(currentSource)
                 └→ NewWorkspaceDialog in edit mode (name+path read-only/hidden)
                      └── ConnectionEditor (same as above)
                 ↓ Save clicked
            ← updated WorkspaceSource (or null on cancel)
       └→ Workspace = Workspace with { Source = updated }   // new instance for INPC
       └→ IWorkspaceStore.SaveAsync(Workspace, currentPath)
```

| Stage | What changes | Where |
|-------|--------------|-------|
| Engine | New `IConnectionTester` + `MSSQLConnectionTester` | `ParameterizationExtractor.Logic/Connectivity/` |
| Security | New `IPasswordProtector` + `DpapiPasswordProtector` | `ParameterizationExtractor.Desktop/Services/Security/` |
| UI control | New reusable `ConnectionEditor` | `ParameterizationExtractor.Desktop/Controls/ConnectionEditor/` |
| Dialogs | New `NewWorkspaceDialog` (MetroWindow) | `ParameterizationExtractor.Desktop/Dialogs/NewWorkspace/` |
| Service abstractions | `IDialogService` extended (typed methods for new + edit) | `Services/Dialogs/IDialogService.cs` (recipe spec updated in step 05) |
| Toolbar | `ConnectionIndicator` text → clickable button | `MainWindow.xaml` |
| MainWindowViewModel | New `NewWorkspaceCommand`, `EditConnectionCommand` | `MainWindowViewModel.cs` |
| Welcome view | `IsNewWorkspaceEnabled` `false → true`, command bound | `WelcomeViewModel.cs`, `WelcomeView.xaml` |
| Workspace format | `WorkspaceSource.PasswordEncrypted` becomes the canonical storage | `Logic/Configs/Json` (no schema change; semantics change) |

---

## 2. Component Diagram

```
+--------------------------------------------------------+
|  ParameterizationExtractor.Desktop                     |
|                                                        |
|  MainWindow                                            |
|   ├── Toolbar.ConnectionIndicator (Button — NEW click) |
|   └── (existing chrome)                                |
|              ↓ EditConnectionCommand                   |
|   MainWindowViewModel                                  |
|   ├── NewWorkspaceCommand   (NEW)                      |
|   ├── EditConnectionCommand (NEW)                      |
|   └── existing OpenCommand / CloseCommand / Exit       |
|         ↓ uses                                         |
|   IDialogService (extended)                            |
|   ├── ShowNewWorkspaceDialogAsync()  → NewWsResult?    |
|   └── ShowEditConnectionDialogAsync(WorkspaceSource)   |
|         ↓ opens                                        |
|   NewWorkspaceDialog (MetroWindow, modal)              |
|   ├── name textbox / path picker (collapsed in edit)   |
|   ├── ConnectionEditor (reused control)                |
|   │     └── ConnectionEditorViewModel                  |
|   │          ├── TestCommand (cancellable)             |
|   │          ├── status enum (idle/testing/ok/fail)    |
|   │          └── uses IConnectionTester +              |
|   │                   IPasswordProtector               |
|   ├── Create / Save button                             |
|   └── Cancel button                                    |
|                                                        |
|  IPasswordProtector (NEW Singleton)                    |
|   └── DpapiPasswordProtector                           |
|        └── ProtectedData.Protect/Unprotect             |
|             (DataProtectionScope.CurrentUser)          |
+--------------------------------------------------------+
                              │
                              │ IConnectionTester
                              ↓
+--------------------------------------------------------+
|  ParameterizationExtractor.Logic                       |
|  Connectivity/                                         |
|   ├── IConnectionTester                                |
|   ├── ConnectionTestResult (record)                    |
|   └── MSSQLConnectionTester                            |
|        └── opens SqlConnection (Microsoft.Data.SqlClient)
|             counts: sys.tables, sys.foreign_keys       |
|             10s connection timeout (inner)             |
|             observes CancellationToken (outer)         |
+--------------------------------------------------------+
```

No new project. No new project references. Desktop still references `Common` + `Logic` only.

---

## 3. Data Flow

### 3.1 New workspace (happy path)

```
User → [ New workspace... ]
     → NewWorkspaceCommand
     → IDialogService.ShowNewWorkspaceDialogAsync()
     → NewWorkspaceDialog.ShowDialog()
         user fills: name="patient-clearing", path="C:\Workspaces\patient-clearing.bws",
                     server, database, auth=SQL, user, password, store=true
         clicks Test → ConnectionEditorViewModel.TestAsync
              → builds connection string
              → IConnectionTester.TestAsync(connStr, ct)
                   → MSSQLConnectionTester opens SqlConnection
                   → SELECT COUNT(*) FROM sys.tables / sys.foreign_keys
                   → returns ConnectionTestResult { Success=true, TableCount=372, FkCount=489 }
              → status="OK Connected — 372 tables, 489 FKs"
         clicks Create workspace
              → IPasswordProtector.Protect(password) → base64
              → builds WorkspaceModel { Name, Source, default Global, empty Package }
              → IWorkspaceStore.SaveAsync(model, path)
              → returns NewWorkspaceResult { Path }
         dialog closes
     ← MainWindowViewModel receives Path
     → OpenWorkspaceAsync(Path) (existing entry — pushes recents, sets Workspace, refreshes Welcome)
```

### 3.2 Edit connection (happy path)

```
User clicks ConnectionIndicator
     → EditConnectionCommand
     → IDialogService.ShowEditConnectionDialogAsync(_workspace.Source)
     → NewWorkspaceDialog opens in edit mode
         name + path collapsed; ConnectionEditor pre-populated
         user changes password
         clicks Test → ... → OK
         clicks Save
              → IPasswordProtector.Protect(newPassword) → base64
              → returns updated WorkspaceSource
         dialog closes
     ← MainWindowViewModel receives updated source
     → Workspace = Workspace with { Source = updated }   (record-with or new-up; see § 5)
     → _store.SaveAsync(Workspace, _currentPath)
     → ConnectionDisplay re-fires automatically via existing [NotifyPropertyChangedFor]
```

### 3.3 Cancellation path (Test in flight)

```
User clicks Test → TestCommand starts → status="testing", button shows "Cancel"
     → IConnectionTester.TestAsync(..., ct) is in progress
User clicks Cancel button → TestCancelCommand
     → ct.Cancel()
     → MSSQLConnectionTester observes (await SqlConnection.OpenAsync(ct) throws OperationCanceledException)
     → catch OperationCanceledException in VM → status="cancelled"
     → button reverts to "Test"
```

The `[RelayCommand(IncludeCancelCommand = true)]` source generator gives both `TestCommand` and `TestCancelCommand` from a single async method that takes a `CancellationToken`.

### 3.4 Test failure path

`MSSQLConnectionTester` does **not** rethrow `SqlException` / `InvalidOperationException` — it catches and packages them into `ConnectionTestResult { Success = false, ErrorMessage = ex.Message }`. This keeps the engine's contract symmetric and avoids leaking ADO.NET exception types across the module boundary. The VM displays `ErrorMessage` in the status label.

`OperationCanceledException` does propagate (cancellation is structurally distinct from a logical failure).

---

## 4. Data Stores

| Store | Technology | Purpose | Owner |
|-------|------------|---------|-------|
| `WorkspaceSource.PasswordEncrypted` | Inside `.bws` JSON, base64-encoded DPAPI ciphertext | Persisted SQL password (when "Store credentials" is checked) | `IPasswordProtector` (Desktop) |

The `.bws` schema doesn't change — the field exists today as a placeholder. What changes is **semantics**:

- Pre-feature: always `null`; tests pin "placeholder behaviour".
- Post-feature: ciphertext when stored; `null` when opt-out is selected (operator must re-enter on each open — but the in-feature open flow doesn't yet prompt; see "Out of scope").

DPAPI ciphertext is bound to the encrypting Windows user account. Decrypt fails after machine move / account change → `Unprotect` throws `CryptographicException` → on workspace load we catch, log a warning, and treat as if `PasswordEncrypted == null`. Operator has to re-enter through Edit-connection.

No SQL Server data writes from this feature — `IConnectionTester` is read-only (SELECTs against `sys` views).

---

## 5. State model

`MainWindowViewModel` gains:

| Member | Type | Behaviour |
|---|---|---|
| `NewWorkspaceCommand` | `IAsyncRelayCommand` | dialog → `OpenWorkspaceAsync(path)` |
| `EditConnectionCommand` | `IAsyncRelayCommand` (CanExecute = `IsWorkspaceLoaded`) | dialog (edit mode) → `Workspace = Workspace with { Source = updated }` → `SaveAsync` |

`Workspace` replacement strategy: `WorkspaceModel` is currently a class with init-only-ish properties. Decision: in step 04, add a `WorkspaceModel.WithSource(WorkspaceSource)` instance method (or convert to a `record` if mutation surface is small enough). The setter chain stays identical: assigning the new `WorkspaceModel` to `Workspace` triggers the existing `[NotifyPropertyChangedFor(nameof(WindowTitle), nameof(ConnectionDisplay), nameof(IsWorkspaceLoaded))]` chain.

`ConnectionEditorViewModel` (transient, one per dialog instance):

| Member | Type | Behaviour |
|---|---|---|
| `Server` | `string` | bound textbox |
| `Database` | `string` | bound textbox (free-form v1) |
| `IsWindowsAuth` / `IsSqlAuth` | `bool` (linked) | radio binding |
| `User`, `Password` | `string` | shown only when `IsSqlAuth` |
| `StoreCredentials` | `bool` | default `true`; hidden in Windows-auth |
| `TestStatus` | enum `Idle / Testing / Success / Failure / Cancelled` | drives status label + colour |
| `StatusMessage` | `string` | "OK Connected — 372 tables, 489 FKs" or error |
| `TestCommand` / `TestCancelCommand` | generated by CTK.MVVM | calls `IConnectionTester.TestAsync` |

Auth-radio toggles field visibility via value-converted bindings on `IsSqlAuth`.

`WelcomeViewModel`:

- `IsNewWorkspaceEnabled` flips from hardcoded `false` to a real `true`.
- `[RelayCommand] private async Task NewWorkspaceAsync()` calls `_dialog.ShowNewWorkspaceDialogAsync()` then `await _openHandler(result.Path)` on success. Wired via the existing `Bind(Func<string, Task>)` mechanism — no architectural change.
- `NewWorkspaceTooltip` removed (button is enabled now).

---

## 6. Threading & Cancellation

- `TestCommand` is `async Task` with a `CancellationToken` parameter — CTK.MVVM source-gen produces both `TestCommand` and `TestCancelCommand`. The latter calls `Cancel()` on the token source the framework manages.
- `IConnectionTester.TestAsync(connectionString, ct)`: must observe `ct` and propagate `OperationCanceledException`. Inner timeout (Connect Timeout=10) is a hard upper bound when the user doesn't cancel.
- `_store.SaveAsync(workspace, path)` after edit-confirm runs on the dispatcher; the small JSON write doesn't justify `IUiDispatcher`. (Recipe still has `IUiDispatcher` arriving with `desktop-seed-tab`.)
- The `NewWorkspaceDialog.ShowDialog()` blocks the dispatcher per WPF modal semantics. The `IDialogService.ShowNewWorkspaceDialogAsync` impl wraps `ShowDialog` in a `TaskCompletionSource` so VMs see a `Task<NewWorkspaceResult?>` (consistent with the existing async dialog methods).

---

## 7. Service abstractions — what lands

| Abstraction | Status before | Status after | Lifetime | Impl |
|---|---|---|---|---|
| `IConnectionTester` | did not exist | **Implemented** | Singleton | `Logic/Connectivity/MSSQLConnectionTester.cs` |
| `IPasswordProtector` | did not exist | **Implemented** | Singleton | `Desktop/Services/Security/DpapiPasswordProtector.cs` |
| `IDialogService` | landed in prior feature | **extended** with two typed dialog methods | Singleton | `Services/Dialogs/DialogService.cs` |
| `IUiDispatcher` | pre-spec | still pre-spec | — | first impl ships with `desktop-seed-tab` |

Test fakes:

- `FakeConnectionTester` (Tests/Desktop/Fakes/) — queue-driven, mirrors the existing `FakeDialogService` shape.
- `InMemoryPasswordProtector` (Tests/Desktop/Fakes/) — base64-encodes the plaintext (NOT a real protect; just round-trippable for tests).
- `FakeDialogService` extended with `EnqueueNewWorkspaceResponse(NewWorkspaceResult?)` + `EnqueueEditConnectionResponse(WorkspaceSource?)`.

---

## 8. Extension Points

| Seam | After this feature | Next consumer |
|---|---|---|
| `IConnectionTester` | first impl | `desktop-seed-tab` (root-table picker uses the same connection); `desktop-dry-run-and-execute` (target connection test) |
| `IPasswordProtector` | first impl | any future feature that handles secrets (none planned) |
| `ConnectionEditor` reusable control | exists | `desktop-dry-run-and-execute` Run-tab target connection (per components.md) |
| `IDialogService` typed dialog methods | precedent set | future custom dialogs follow the same `Show*Async` shape |
| `IWorkspaceStore.SaveAsync` from a VM | first call site | future Save / Save-As feature, dirty-marker prompts |

---

## 9. Risks & Open Questions

- **DPAPI failure on machine move.** `Unprotect` throws `CryptographicException` if the encrypting user / machine context is gone. **Decision:** on workspace load, catch + log warning, set the runtime `Password` to empty. The operator's first sign is a failed Test; they Edit-connection and re-enter. No "your credentials need refresh" UX in v1.
- **WorkspaceModel mutability.** Today's `WorkspaceModel` properties are settable. Step 04 must decide between (a) "in-place edit `Workspace.Source.User = ...`" (doesn't fire INPC on `Workspace`) — bad — or (b) "construct a new `WorkspaceModel` with the updated `Source`" — preferred. Consider a small `Workspace.WithSource(WorkspaceSource)` helper to keep the call site readable.
- **Edit-connection without a current path.** Defensive: `EditConnectionCommand` `CanExecute = IsWorkspaceLoaded` — if no workspace, the button is disabled. Won't fire.
- **Two simultaneous Test runs.** If the user spams Test, CTK.MVVM's `[RelayCommand]` default `AllowConcurrentExecutions = false` already serialises. We rely on that; no extra guard.
- **NewWorkspaceDialog as Window vs Flyout.** Picked: separate `MetroWindow`, owner = main window, `WindowStartupLocation = CenterOwner`. Easier focus + testing than a Flyout.
- **Engine `MSSQLConnectionTester` placement.** Must live where `Microsoft.Data.SqlClient` types are allowed — `Logic/MSSQL/` would be the existing precedent. We pick `Logic/Connectivity/` to keep the connection-test concept distinct from the bigger `MSSQL` schema/dependency code (which is engine-internal). Both are `Logic`-scoped; tripwire is preserved.

---

## 10. Security & Isolation

- **Password at rest:** DPAPI `DataProtectionScope.CurrentUser` (ADR-010). Single-user / single-machine binding is intentional — operator credentials shouldn't be portable. The `.bws` file alone is not enough to recover the password.
- **Password in memory:** decrypted to a runtime shadow string only when the workspace is loaded. Cleared on `CloseCommand`. We do **not** push the plaintext into the workspace JSON string — only the ciphertext field is serialised.
- **Connection string construction:** lives in `ConnectionEditorViewModel`. Uses `SqlConnectionStringBuilder` (`Microsoft.Data.SqlClient`) to avoid manual string concat — we make an exception to the "no `Microsoft.Data.SqlClient` outside Logic" tripwire **for the builder type only** (it's parameter-quoting, not SQL execution). Step 03's tripwire-grep call-out flags this with the canonical exception comment. Alternative: build connection string in `Logic` and accept the round-trip — probably cleaner; **decide in step 03**.
- **No new endpoints. No remote IO beyond the operator's own SQL Server.**

---

## 11. What This Feature Does *Not* Build

- No `sys.databases` discovery dropdown.
- No general Save / Save-As / dirty-marker / unsaved-changes prompt.
- No "credentials need refresh" UX after machine move.
- No connection presets, no credential vault integration.
- No connection sharing across workspaces (each `.bws` has its own).
- No `IUiDispatcher` (lands with `desktop-seed-tab`).
- No engine invocation beyond the test SELECTs.
