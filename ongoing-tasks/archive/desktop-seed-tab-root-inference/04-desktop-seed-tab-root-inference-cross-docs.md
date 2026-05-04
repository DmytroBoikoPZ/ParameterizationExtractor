# 04 — desktop-seed-tab-root-inference — Cross-doc updates

## Goal

Sync docs for the new auto-detect behaviour: M4 mockup, `wpf-desktop.md` recipe note (so future operators / contributors know the picker is auto-driven), and the roadmap entry.

## Track

`docs`.

## What to Build

### `docs/design/desktop-ui/04-seed.md`

Update the "Implications" section:

- Add a new bullet: "**Root is auto-detected** from the seed query's first `FROM <table>`. The picker shows the inferred value as a passive confirmation; manual override is allowed and surfaces a mismatch hint when the operator's pick disagrees with the SQL."
- Update the mockup ASCII to show the relabelled `Root (auto-detected from query):` row.

### `docs/methodology/wpf-desktop.md`

Small note in the "Code editor (AvalonEdit)" section: "VMs hosting an `SqlEditor` may parse the typed SQL for inferred state — see `Logic/Helpers/SeedQueryParser` for the pattern (used by `ScriptEditorViewModel` to auto-fill the Root picker from the first `FROM` clause)."

### `docs/roadmap.md`

- Move `desktop-seed-tab-root-inference` to ✅ Completed:
  > ✅ desktop-seed-tab-root-inference — Seed tab auto-detects the root table from the SQL editor's first `FROM` clause; manual override surfaces a non-blocking mismatch hint. UX patch closing the operator-feedback gap from `desktop-graph-viz` smoke testing.
- Promote next item to Proposed next.

### `CLAUDE.md` audit

No new tripwire needed — `SeedQueryParser` lives in `Logic/Helpers/` (existing pattern); the VM uses an existing partial method hook. Verify CLAUDE.md ↔ `.github/copilot-instructions.md` SHA256 unchanged; record in Notes.

### Verification

- `dotnet build` 0 errors (sanity); `dotnet test` green (sanity).
- Spot-check rendered docs.

## Acceptance Criteria

- [ ] `docs/design/desktop-ui/04-seed.md` Implications updated with auto-detect bullet; mockup label updated.
- [ ] `docs/methodology/wpf-desktop.md` mentions `SeedQueryParser` as the inferred-state pattern.
- [ ] `docs/roadmap.md` — feature in ✅ Completed; next promoted.
- [ ] CLAUDE.md SHA unchanged.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Touched docs: as listed above.
- Depends on: [03 — view + smoke](./03-desktop-seed-tab-root-inference-view.md).
