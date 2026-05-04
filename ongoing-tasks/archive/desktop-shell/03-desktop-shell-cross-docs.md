# 03 — desktop-shell — Cross-doc updates

## Goal

Bring `docs/architecture/overview.md` and `docs/methodology/wpf-desktop.md` in line with what the desktop now renders. Update the roadmap to mark `desktop-shell` complete and tee up the next feature.

## Track

`cross-cutting` (docs only).

## What Exists

- After steps 01–02: shell renders, `examples/sample.bws` loads, all tests green.
- [`docs/architecture/overview.md`](../../docs/architecture/overview.md) — § 2 Desktop row currently reads "Operator-facing UI shell — hosts the workspace authoring views; owns `IWorkspaceStore` for `.bws` JSON workspace files (ADR-009)". The `IWorkspaceStore` claim is now backed by a visible UI; the Desktop row should reflect that.
- [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) — § How to add a screen describes the `Views/Xxx/` pattern in the abstract. After step 01, there's a concrete reference (`Views/Overview/`) to cite.
- [`docs/roadmap.md`](../../docs/roadmap.md) — `desktop-shell` is in "Proposed next" and "Desktop UI track"; needs to move to "Completed".

## What to Build

### `docs/architecture/overview.md`

- § 2 (Components) — Desktop row Purpose: append "Renders the M3 tabbed shell (Overview / Seed / Graph / Extras / Run). Overview tab is implemented; remaining tabs are placeholders until their owning features land."
- § 7 (Active ADRs) — no change (no new ADR introduced by this feature).
- § 8 (Where to add detail) — verify the "Per-stack recipes" pointer still applies; no edit expected.

### `docs/methodology/wpf-desktop.md`

- § "How to add a screen" — add a concrete example sentence pointing at `Views/Overview/`:
  > **Reference implementation:** `Views/Overview/{OverviewView.xaml(.cs), OverviewViewModel.cs}` is the canonical pairing. New screens copy this layout.
- § "DI lifetimes" — verify the table still matches reality. Tab/screen ViewModels are Singleton today (`OverviewViewModel`); recipe says Singleton for tab VMs. Consistent. No edit.

### `docs/roadmap.md`

- Move `desktop-shell` from `## Proposed next` and `## Desktop UI track` to `## Completed` with one-sentence summary: `desktop-shell — M3 tabbed shell with Overview tab bound to IWorkspaceStore; placeholder tabs for Seed/Graph/Extras/Run.`
- Update `## Proposed next` to point at `desktop-startup-and-open-workspace` (the natural follow-up; first feature to ship `IDialogService`).

### `CLAUDE.md` and mirror

- No tripwire change expected. Only edit if step 02 surfaces a new pattern worth ratifying (e.g. "always `await InitializeAsync` before `Show()` in `App.OnStartup`"). If touched, mirror to `.github/copilot-instructions.md` byte-identical and verify SHA256.

## Acceptance Criteria

- [ ] `docs/architecture/overview.md` § 2 Desktop row mentions the M3 shell and the Overview tab implementation status.
- [ ] `docs/methodology/wpf-desktop.md` § How to add a screen cites `Views/Overview/` as the reference pairing.
- [ ] `docs/roadmap.md` moves `desktop-shell` to `## Completed`; `## Proposed next` recommends `desktop-startup-and-open-workspace`.
- [ ] If `CLAUDE.md` is touched, the byte-identical mirror to `.github/copilot-instructions.md` is verified by SHA256.
- [ ] `dotnet build` 0 errors; `dotnet test` 63/63 (sanity).
- [ ] All three checklist items in `desktop-shell-checklist.md` are `[x]`.
- [ ] Closing summary entry added to `desktop-shell-checklist.md` `## Notes`.

## References

- Pattern exemplar for closing-summary notes: [`ongoing-tasks/archive/desktop-workspace-format-checklist.md`](../archive/desktop-workspace-format-checklist.md) `## Final summary`.
- Architecture: [`docs/architecture/overview.md`](../../docs/architecture/overview.md)
- Roadmap: [`docs/roadmap.md`](../../docs/roadmap.md)
- Depends on: steps 01 and 02 must both be `[x]`.
