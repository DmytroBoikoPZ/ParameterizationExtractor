# 06 — engine-schema-aware-resolution — Cross-doc updates

## Goal

Sync the docs that describe the engine's surface now that schema awareness is live. Move the feature to Completed on the roadmap. Keep CLAUDE.md sync intact.

## Track

`docs`.

## What to Build

### `docs/architecture/overview.md`

- § 3.2 (Processing) — adjust the bullet about graph traversal: "walks foreign-key relations from a seed query, **resolving tables by `(Schema, Name)` tuples (ADR-011)**, …"
- § 7 Active ADRs — add ADR-011 entry:
  > `011-engine-schema-awareness.md` — Engine resolves tables by `(Schema, Name)` tuples; empty Schema in operator config is treated as bare-name lookup with one-or-throw policy. T4 emits `[Schema].[Table]` when Schema is non-empty.
- No § 4 (Data stores) change — no new artefact.

### `docs/methodology/dotnet-cli.md`

- Note in the engine recipe (find the section that names `PTable`/`TableToExtract`): `Schema` (string, default `""`) is the canonical schema slot. Empty Schema means "use SQL default-schema fallback via `MSSQLSourceSchema.ResolveTable`'s bare-name policy". Cross-link to ADR-011.
- If the recipe has a "tripwires" section: optionally add: *"Don't compare table names without using `MSSQLSourceSchema.ResolveTable` (or equivalent `(Schema, Name)` tuple comparison) — bare `Tables.First(t => t.Name == ...)` is forbidden post-ADR-011 because it silently picks a single-schema match and misses the ambiguity case."* Decision: keep as a recipe note rather than a CLAUDE.md tripwire — it's a single-module rule, not cross-cutting.

### `docs/methodology/workspace-format.md`

- The `TableToExtract` shape now carries an optional `schema`. Update the example JSON to show one entry with `schema` and one without (back-compat illustration).

### `docs/roadmap.md`

- Move `engine-schema-aware-resolution` from `## Proposed next` (where step 05 of the previous feature put it — actually it was `desktop-seed-tab` that became Proposed next; `engine-schema-aware-resolution` was always under `## Engine / CLI`). Re-check the actual current location and move to `## Completed`.
- Completed entry shape:
  > ✅ engine-schema-aware-resolution — Engine model gains `Schema` (default empty for back-compat); `MSSQLSourceSchema.ResolveTable` implements bare-name + qualified resolution; T4 emits `[Schema].[Table]` when non-empty; cross-schema characterisation scenario landed (skips if operator lacks `CREATE SCHEMA`).
- Promote `desktop-seed-tab` to `## Proposed next` (it was queued there pending this feature).
- The cross-schema scenario also closes a follow-up note from `characterisation-tests` — if any line in roadmap or that archived checklist references it as deferred, mark it crossed off (light edit).

### `CLAUDE.md` audit

- No new tripwire warranted. Recipe-level note (above) is sufficient.
- Recompute SHA256 of `CLAUDE.md` and `.github/copilot-instructions.md`; assert equal; record in Notes.

### Verification

- `dotnet build` — 0 errors (sanity).
- `dotnet test` — full suite green (sanity; this step has no code change).

## Acceptance Criteria

- [ ] `docs/architecture/overview.md` § 3.2 mentions schema-aware traversal; § 7 has ADR-011 entry.
- [ ] `docs/methodology/dotnet-cli.md` mentions the `Schema` slot + back-compat policy.
- [ ] `docs/methodology/workspace-format.md` example shows `schema` field optional.
- [ ] `docs/roadmap.md` — feature in ✅ Completed; `desktop-seed-tab` is Proposed next.
- [ ] CLAUDE.md / copilot-instructions.md SHA256 unchanged from feature-start; equal to each other; recorded in Notes.
- [ ] `dotnet build` 0 errors; `dotnet test` green (sanity).

## References

- ADR: [`adr/011-engine-schema-awareness.md`](../../adr/011-engine-schema-awareness.md) (from step 01).
- Touched docs: as listed above.
- Pattern exemplar: prior features' step-NN cross-docs.
- Depends on: [05 — characterisation](./05-engine-schema-aware-resolution-characterisation.md).
