# 04 — desktop-startup-and-open-workspace — Cross-doc updates

## Goal

Synchronise the docs that describe the desktop's surface now that the Welcome view, `IDialogService`, and `IRecentFilesStore` have landed. Move the feature to `Completed` on the roadmap and promote `desktop-connection-management` to `Proposed next`.

## Track

`docs`.

## What Exists

- [`docs/architecture/overview.md`](../../docs/architecture/overview.md) — § 2 Components currently describes the Desktop as "renders the M3 tabbed shell (Overview / Seed / Graph / Extras / Run); Overview tab is implemented, the rest are placeholders". No mention of the Welcome view, dialogs, or recent files.
- [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) — § Service abstractions marks `IDialogService` as **pre-spec**. § Dialogs / file pickers points at the recipe spec, not at a real impl. § Implementation timing has the table that says `IDialogService` will land with M1 or M2.
- [`docs/methodology/wpf-desktop.md` § How to add a screen](../../docs/methodology/wpf-desktop.md) — points at `Views/Overview/` as the canonical reference; can also mention `Views/Welcome/` after this feature lands.
- [`docs/roadmap.md`](../../docs/roadmap.md) — `desktop-startup-and-open-workspace` listed in `## Proposed next` and `## Desktop UI track`. `desktop-connection-management` is the next item.
- [`CLAUDE.md`](../../CLAUDE.md) — root tripwire file. Re-check whether any new tripwire surfaced (e.g. dialogs / recent-file paths). Likely no change; verify hash unchanged with `.github/copilot-instructions.md`.

## What to Build

### `docs/architecture/overview.md`

- § 2 Components — Desktop row Purpose extended to mention: "Welcome view (mockup M1) on empty state; File menu (Open / Recent / Close / Exit); per-user recent files at `%APPDATA%\SqlBuldozer\recent-files.json`".
- § 4 Data Stores — add a row for the recent-files JSON. Same shape as the Workspace row:
  ```
  | Recent files (`recent-files.json`) | Local filesystem (JSON) | Per-user list of recently-opened workspace paths (max 5) | `Desktop` (`IRecentFilesStore` reads + writes) |
  ```
- § 5 Cross-cutting concerns — DI container row: append a sentence that the Desktop Singleton lifetime now covers `IDialogService` (MahApps + Win32) and `IRecentFilesStore` (JSON-backed) as well.
- § 7 Active ADRs — no new ADR for this feature; no edit needed.

### `docs/methodology/wpf-desktop.md`

- § Service abstractions header note — change `**Status: pre-spec.**` to `**Status: partially landed.** \`IDialogService\` is implemented (since \`desktop-startup-and-open-workspace\`); \`IUiDispatcher\` remains pre-spec.`
- § Service abstractions — `IDialogService` subsection: mark **Implemented** with a one-line pointer:
  > Implemented as `Services/Dialogs/{IDialogService.cs, DialogService.cs}` (since `desktop-startup-and-open-workspace`). Test fake at `Tests/Desktop/Fakes/FakeDialogService.cs`.
- § Service abstractions — Implementation timing table: change `IDialogService` row's "First feature" to read the actual feature: `desktop-startup-and-open-workspace (landed)`.
- § Dialogs / file pickers — change the verb from "go through `IDialogService`" (still correct) to also mention the WPF impl is `MahApps DialogCoordinator` + `Microsoft.Win32` pickers; one-line note about the `Application.Current` exception (services-only, with the canonical comment).
- § How to add a screen — add a second reference implementation pointer:
  > **Reference implementations:** `Views/Overview/` (read-only summary; landed by `desktop-shell`) and `Views/Welcome/` (services-driven empty-state view; landed by `desktop-startup-and-open-workspace`).
- New § "Recent files" between "Workspace store" and "Dialogs":
  > The desktop persists a per-user list of recently-opened workspaces through `IRecentFilesStore` in `Services/RecentFiles/`. The default impl (`JsonRecentFilesStore`) writes to `%APPDATA%\SqlBuldozer\recent-files.json`, capped at 5 entries (`IRecentFilesStore.MaxItems`), de-duplicated by canonical path (case-insensitive). The store is intentionally tolerant of disk errors — recents is a UX cache, not authoritative state. `MainWindowViewModel.OpenWorkspaceAsync` is the single entry that pushes to recents; consumers don't push directly. Tests use `Tests/Desktop/Fakes/InMemoryRecentFilesStore.cs`.

### `docs/roadmap.md`

- Move `desktop-startup-and-open-workspace` from `## Proposed next` and `## Desktop UI track` to `## Completed`. Completed entry shape (mirror existing entries):
  > ✅ desktop-startup-and-open-workspace — Welcome view (M1) on empty state; first impl of `IDialogService` (MahApps + Win32) and `IRecentFilesStore` (JSON @ `%APPDATA%\SqlBuldozer\`); File menu (Open / Recent / Close / Exit); recents capped at 5.
- Promote `desktop-connection-management` to `## Proposed next`. Update its description (currently: "DPAPI-encrypted password storage + opt-out checkbox; lands the ADR for password-at-rest and populates the placeholder field on `WorkspaceSource`") to reflect that it now also owns the `[ New workspace... ]` dialog from M2 (since this feature deferred it).

### `CLAUDE.md` audit

- No new tripwire is expected. Re-confirm by re-reading the tripwires section. If everything still holds, **don't edit**.
- Recompute `sha256sum CLAUDE.md` and `sha256sum .github/copilot-instructions.md` (or the equivalent on Windows: `Get-FileHash`). If these two files are kept in sync (they were as of `desktop-shell`), confirm both still match. Note both hashes in the checklist's `## Notes`.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors (sanity).
- `dotnet test "SQL Buldozer.sln"` — 92/92 pass (sanity; this step has no code change).

## Acceptance Criteria

- [ ] `docs/architecture/overview.md` § 2 Desktop row mentions Welcome view + File menu + recent files.
- [ ] `docs/architecture/overview.md` § 4 has a new row for `recent-files.json`.
- [ ] `docs/architecture/overview.md` § 5 DI-container line mentions the two new singletons.
- [ ] `docs/methodology/wpf-desktop.md` § Service abstractions header marks status `partially landed`; `IDialogService` subsection points at the impl files.
- [ ] `docs/methodology/wpf-desktop.md` § How to add a screen lists both Overview and Welcome as reference implementations.
- [ ] `docs/methodology/wpf-desktop.md` has a new § "Recent files".
- [ ] `docs/roadmap.md` — feature is in `## Completed`; `desktop-connection-management` is the proposed next item with updated description.
- [ ] `CLAUDE.md` SHA256 checked + recorded; equal to `.github/copilot-instructions.md` SHA256.
- [ ] `dotnet build` 0 errors; `dotnet test` 92/92 (sanity).

## References

- Sibling ADRs (no new): [`adr/007`](../../adr/007-desktop-wpf-stack.md), [`adr/008`](../../adr/008-desktop-ui-controls.md), [`adr/009`](../../adr/009-workspace-format.md)
- Touched docs: [`docs/architecture/overview.md`](../../docs/architecture/overview.md), [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md), [`docs/roadmap.md`](../../docs/roadmap.md)
- Pattern exemplar: `desktop-shell` step 03 cross-docs commit (in `ongoing-tasks/archive/desktop-shell/...` once archived; or in the live folder while still in flight).
- Depends on: [`03 — Welcome view + Open workflow`](./03-desktop-startup-and-open-workspace-welcome-and-open.md).
