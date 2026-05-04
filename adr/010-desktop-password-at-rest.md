# 010 — Desktop password-at-rest — DPAPI under `DataProtectionScope.CurrentUser`

## Status

Accepted

## Context

[ADR-009](./009-workspace-format.md) reserved a `passwordEncrypted` slot on `WorkspaceSource` but deferred the encryption choice to the `desktop-connection-management` feature. Operators authenticate to SQL Server with either Windows-integrated auth (no stored secret) or SQL auth (username + password). For SQL auth, the workflow is "open the same `.bws` daily without retyping the password". Storing plaintext is unacceptable; storing nothing forces re-entry on every open and breaks the recents-driven daily flow established by `desktop-startup-and-open-workspace`.

Mockup [M2](../docs/design/desktop-ui/02-new-workspace.md) and the L6 design pass already locked the user-facing choice: a "Store credentials (DPAPI-encrypted in the workspace file)" checkbox, default-checked, whose interaction model is "credentials at rest are bound to this Windows user account on this machine". This ADR formalises the encryption scheme, scope, and recovery posture.

The threat model is narrow: the `.bws` file is a developer artefact that may be checked into a private repo, copied to a USB drive, or sit on a shared file server. The attacker we defend against is *anyone with read access to the file but without the encrypting Windows user's logon session*. We are not defending against an attacker who has compromised the operator's user account; that's an OS-level threat outside the tool's scope.

## Decision

We will:

- **Encrypt the SQL password using DPAPI** (`System.Security.Cryptography.ProtectedData`) under `DataProtectionScope.CurrentUser`, with `optionalEntropy = null`.
- **Persist the ciphertext** as base64 in `WorkspaceSource.PasswordEncrypted` inside the `.bws` JSON. The wire schema does not change — the field already exists from ADR-009 as a placeholder.
- **Default the "Store credentials" checkbox to checked**. When unchecked, the workspace is saved with `passwordEncrypted: null`; the operator must re-enter on every open.
- **Decrypt on workspace load**, hold the plaintext as a runtime shadow string on `ConnectionEditorViewModel` for the lifetime of the loaded workspace, and clear it on Close.
- **Treat decrypt failures as "no stored credential"** — `CryptographicException` from `Unprotect` is logged at warning level and swallowed; the operator's first signal is a failed connection test, and the recovery path is the Edit-connection flow (already shipped by this same feature).
- **Use a Desktop-side seam (`IPasswordProtector`)** so VMs and views never call `ProtectedData` directly. The interface lets a future feature swap to a different scheme (e.g. shared-team scheme) without touching VMs.

Alternatives considered and rejected:

- **`DataProtectionScope.LocalMachine`** — any user on the box could decrypt. Rejected; defeats the threat model when developer machines are shared (uncommon but real, e.g. CI runners with multiple service accounts).
- **AES + per-workspace key derived from a passphrase** — adds a passphrase-prompt UX surface for every open + a key-management story. Heavyweight for a single-operator dev tool; revisit if "share workspace" becomes a thing.
- **OS Credential Manager (`Windows.Security.Credentials`)** — keys stored outside the `.bws` file. Surface area trades portability for a Windows-only manager with no in-product visibility. DPAPI inside the file keeps the workspace self-contained.
- **Plaintext + obfuscation** — explicitly rejected; this is the threat we're defending against.
- **Don't store passwords at all** — the user-facing decision was already locked at L6.

## Consequences

- **Easier:**
  - Daily workflow: open recent → connection test passes immediately. No retyping for the common case.
  - `.bws` files remain self-contained: ciphertext travels with the file. No external key-vault dependency.
  - The `IPasswordProtector` seam keeps VMs ignorant of crypto specifics; tests use an `InMemoryPasswordProtector` that base64-round-trips without bound user context.
  - DPAPI failures (different user, machine moved) self-heal through the existing Edit-connection flow — no migration tool needed.

- **Harder:**
  - `.bws` files are NOT portable across user accounts or machines. A workspace zipped from a teammate or copied to a different login fails decrypt; the operator must re-enter.
  - Future "share workspace" / "team library" features will need either a separate plaintext-export path or a different encryption scheme (and a fresh ADR). The format is already future-proof — ciphertext is opaque to the wire schema — but the encryption boundary is per-user.
  - DPAPI is Windows-only. Cross-platform Avalonia ports (long-term roadmap item) will need an alternative protector impl. The interface seam keeps the impact isolated.

- **Open follow-ups:**
  - **Re-encrypt-on-machine-change UX** — today the operator's first signal is "connection test failed". A future enhancement could detect `CryptographicException` on load and show a banner "stored credentials unavailable on this machine; re-enter to continue". Nice-to-have, deferred.
  - **Tampered-ciphertext audit log** — DPAPI throws on tampering. Today we log warning + clear; we could surface a dialog. Deferred unless there's evidence operators want it.
  - **Tests / CI parity** — DPAPI is bound to the running process's user account. The unit tests in step 02 round-trip happily because they run as the same user that encrypts; a CI runner on a different account will also work as long as the test creates and decrypts ciphertext within the same run. Cross-run decrypt would fail — and we don't try that.
