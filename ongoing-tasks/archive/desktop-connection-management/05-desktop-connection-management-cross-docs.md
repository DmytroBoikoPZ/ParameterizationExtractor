# 05 — desktop-connection-management — Cross-doc updates

## Goal

Sync the docs that describe the desktop's surface now that the ConnectionEditor, dialogs, DPAPI, and engine connection-test seam have landed. Move the feature to `Completed` on the roadmap and promote the next item to `Proposed next`.

## Track

`docs`.

## What Exists

- [`adr/readme.md`](../../adr/readme.md) — already updated in step 01 to include ADR-010.
- [`docs/architecture/overview.md`](../../docs/architecture/overview.md) — Desktop row mentions Welcome view + recent files; no mention of ConnectionEditor / DPAPI / engine connection-test seam yet. § 7 Active ADRs needs ADR-010.
- [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) — § Service abstractions has `IDialogService` marked **implemented** with the original 4 methods; needs the 2 new typed methods. § Dialogs / file pickers describes the current (smaller) surface. No section on password-at-rest or connection management. § How to add a screen lists Overview + Welcome reference impls.
- [`docs/roadmap.md`](../../docs/roadmap.md) — `desktop-connection-management` listed in `## Proposed next`. The next candidate to promote is `desktop-seed-tab` per the Desktop UI track ordering.
- [`CLAUDE.md`](../../CLAUDE.md) — root tripwires. Re-check whether any new tripwire surfaced and recompute SHA256 sync with `.github/copilot-instructions.md`.

## What to Build

### `docs/architecture/overview.md`

- § 2 Components — Desktop row Purpose extended:
  > "…New-workspace + Edit-connection dialogs talk to SQL Server through `IConnectionTester` (Logic-side); SQL passwords stored DPAPI-encrypted (ADR-010)."
- § 4 Data stores — no new file/store entry (the `WorkspaceSource.PasswordEncrypted` field is part of the existing workspace-files row); add a one-line clarification under that row: *"SQL passwords are DPAPI-encrypted at rest when 'Store credentials' is checked (ADR-010)."*
- § 5 Cross-cutting concerns — DI line gets `IConnectionTester` (Singleton, Logic) + `IPasswordProtector` (Singleton, Desktop) appended.
- § 5 Secret management bullet — replace the existing line with: *"Connection strings live in `appsettings.json` or env vars (CLI). Workspace-side SQL passwords are DPAPI-encrypted under `DataProtectionScope.CurrentUser` (ADR-010); plaintext is held in memory only for the lifetime of the loaded workspace."*
- § 7 Active ADRs — add the new entry:
  > `010-desktop-password-at-rest.md` — Workspace SQL passwords are DPAPI-encrypted under `DataProtectionScope.CurrentUser`, with an opt-out checkbox.

### `docs/methodology/wpf-desktop.md`

- § Service abstractions — header status: still `partially landed`; `IUiDispatcher` remains pre-spec; `IDialogService` now has typed dialog methods. Update the body of the `IDialogService` subsection to show the **expanded** interface (all six methods).
- § Service abstractions — implementation-timing table: append rows for `IConnectionTester` (`desktop-connection-management`, **landed**) and `IPasswordProtector` (`desktop-connection-management`, **landed**).
- § Dialogs / file pickers — append: *"Typed dialog methods (`ShowNewWorkspaceDialogAsync`, `ShowEditConnectionDialogAsync`) follow the same pattern: `IDialogService` returns the typed result (`NewWorkspaceResult?`, `WorkspaceSource?`); the impl constructs the `MetroWindow` via a `Func<NewWorkspaceDialog>` DI factory and wraps `ShowDialog()` in `Task.FromResult`."*
- New § "Password protection" between "Recent files" and "Dialogs":
  > The desktop never holds SQL passwords in plaintext on disk. Persisted passwords live in `WorkspaceSource.PasswordEncrypted` as base64-encoded DPAPI ciphertext (`DataProtectionScope.CurrentUser`); `IPasswordProtector` (`Services/Security/`) is the one-way seam VMs use. Plaintext exists only as a runtime field on `ConnectionEditorViewModel` / a transient pass-through in `MainWindowViewModel.EditConnectionAsync`. **Tripwire: VMs and views never read `WorkspaceSource.PasswordEncrypted` directly — always go through `IPasswordProtector.Unprotect`.** Decrypt failures (different user / machine) are caught at the boundary, logged, and surface as "no stored credential"; the Edit-connection flow is the recovery path. See [ADR-010](../../adr/010-desktop-password-at-rest.md).
- New § "Connection management" between "Password protection" and "Dialogs":
  > The Desktop never opens `SqlConnection` directly (mirrors the .NET tripwire). Connection tests go through `IConnectionTester` (engine-side, in `Logic/Connectivity/`); the result is `ConnectionTestResult { Success, TableCount, FkCount, ErrorMessage }`. The reusable `Controls/ConnectionEditor/` UserControl is the only place that builds connection strings (via `SqlConnectionStringBuilder` — exception to the "no `Microsoft.Data.SqlClient` outside Logic" tripwire because the builder doesn't execute SQL). The control hosts in `NewWorkspaceDialog` for both new-workspace and edit-connection flows.
- § How to add a screen — pointer expanded again:
  > **Reference implementations:** `Views/Overview/` (read-only summary; landed by `desktop-shell`), `Views/Welcome/` (services-driven empty-state view; landed by `desktop-startup-and-open-workspace`), `Controls/ConnectionEditor/` (reusable control hosted in dialogs; landed by `desktop-connection-management`).

### `docs/roadmap.md`

- Move `desktop-connection-management` to `## Completed`:
  > ✅ desktop-connection-management — DPAPI password-at-rest (ADR-010), `IConnectionTester` engine seam, reusable `ConnectionEditor`, M2 New-workspace dialog, Edit-connection flow with save-back, Welcome's `[ New ]` button live.
- Promote `desktop-seed-tab` to `## Proposed next`. (If team prefers different ordering, swap there.)

### `CLAUDE.md` audit

The new tripwires surfaced by this feature:

- *VMs and views never read `WorkspaceSource.PasswordEncrypted` directly — go through `IPasswordProtector`.*
- *Desktop never opens `SqlConnection` (extension of the existing tripwire). `SqlConnectionStringBuilder` in the ConnectionEditor is the documented exception.*

These are arguably already covered by existing tripwires ("ViewModels never reference WPF types" — well, `SqlConnection` is the engine boundary, not WPF; and "Raw ADO.NET … types stay in `ParameterizationExtractor.Logic`. Other modules never open a `SqlConnection`" — the new tripwire is just a re-statement and a documented `SqlConnectionStringBuilder` exception). **Decision:** add **one** line to the WPF-Desktop tripwires block:

> *No `WorkspaceSource.PasswordEncrypted` reads from VMs or views — go through `IPasswordProtector` (ADR-010).*

The `SqlConnectionStringBuilder` exception is documented in the methodology recipe (§ Connection management) — no need to surface in the tripwire bullet list.

After editing CLAUDE.md, mirror the change to `.github/copilot-instructions.md` (the two are kept hash-equal). Recompute both SHA256 hashes; record both in checklist `## Notes`.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors (sanity).
- `dotnet test "SQL Buldozer.sln"` — all tests pass (sanity; this step has no code change).

## Acceptance Criteria

- [ ] `docs/architecture/overview.md` § 2 Desktop row + § 5 secret-management line + § 7 ADR list updated.
- [ ] `docs/methodology/wpf-desktop.md` `IDialogService` subsection shows 6 methods; implementation-timing table has the two new rows; new § "Password protection" + § "Connection management" present; § How to add a screen lists three reference impls.
- [ ] `docs/roadmap.md` — feature in ✅ Completed; `desktop-seed-tab` (or chosen next item) in `## Proposed next`.
- [ ] `CLAUDE.md` gains the one new tripwire line; `.github/copilot-instructions.md` mirror; SHA256 hashes equal and recorded in `## Notes`.
- [ ] `dotnet build` 0 errors; `dotnet test` green (sanity).

## References

- ADR: [`adr/010-desktop-password-at-rest.md`](../../adr/010-desktop-password-at-rest.md) (authored in step 01).
- Touched docs: [`docs/architecture/overview.md`](../../docs/architecture/overview.md), [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md), [`docs/roadmap.md`](../../docs/roadmap.md), [`CLAUDE.md`](../../CLAUDE.md), [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md).
- Pattern exemplar: archived `desktop-startup-and-open-workspace` step 04 (the prior cross-docs commit).
- Depends on: [`04 — flows and wire-up`](./04-desktop-connection-management-new-and-edit-flows.md).
