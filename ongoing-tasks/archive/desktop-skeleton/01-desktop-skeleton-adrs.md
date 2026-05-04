# 01 — desktop-skeleton — ADRs (WPF stack + UI controls)

## Goal

Lock the desktop stack with two new ADRs so subsequent steps reference binding policy rather than fresh decisions. **No code in this step.**

## Track

`cross-cutting` (architecture / docs).

## What Exists

- `adr/` directory with `001` … `006` plus `readme.md` index. ADR conventions: `Status` / `Context` / `Decision` / `Consequences`.
- `adr/006-msdi-container.md` — pattern exemplar for an ADR that locks a stack-level choice and lists open follow-ups.
- ADR-002 (module layering) — names the `<ProjectReference>` graph; this step extends that graph in prose only (the actual `<ProjectReference>` lands in step 03).
- ADR-005 (F# DSL frozen) — desktop must not reference DSL / DSL.Connector. ADR-007 must reaffirm.
- Decisions to lock (already agreed in design session 2026-05-02):
  - **Stack:** WPF, target `net10.0-windows`, `<UseWPF>true</UseWPF>`, root namespace `Quipu.ParameterizationExtractor.Desktop`.
  - **MVVM:** CommunityToolkit.Mvvm 8.x.
  - **Composition:** Microsoft.Extensions.Hosting (Generic Host), MS.DI, symmetric with CLI per ADR-006.
  - **Logging:** Serilog (already in repo), reused.
  - **Testing:** NUnit (already in repo) + FluentAssertions (new dependency, ViewModel-level tests only).
  - **UI chrome:** MahApps.Metro 2.4.x.
  - **Code editor control:** AvalonEdit (ICSharpCode).
  - **Docking lib:** none upfront — `Grid` + `GridSplitter`.
  - **Graph viz library:** deferred to `desktop-graph-viz` feature; ADR-008 lists it as an open follow-up.

## What to Build

- `adr/007-desktop-wpf-stack.md` — Status: `Accepted`. Decision locks: target framework, MVVM library, composition / DI, logging, testing. Cites ADR-006 for DI symmetry, ADR-002 for layering, ADR-005 for F# constraint.
- `adr/008-desktop-ui-controls.md` — Status: `Accepted`. Decision locks: MahApps.Metro for chrome, AvalonEdit for the code editor surface, no docking library, FluentAssertions added as test-time dependency. Open follow-ups: graph viz library, password-at-rest strategy.
- `adr/readme.md` — index updated with rows for ADR-007 and ADR-008.

## Acceptance Criteria

- [ ] `adr/007-desktop-wpf-stack.md` exists with all four canonical sections (Status, Context, Decision, Consequences) and Status = `Accepted`.
- [ ] `adr/008-desktop-ui-controls.md` exists with all four canonical sections and Status = `Accepted`.
- [ ] ADR-007 explicitly states the desktop does **not** reference `DSL` or `DSL.Connector` (cites ADR-005).
- [ ] ADR-007 explicitly states Generic Host is the composition root (cites ADR-006).
- [ ] ADR-008 lists graph-viz library choice as an open follow-up (deferred to `desktop-graph-viz` feature).
- [ ] ADR-008 lists password-at-rest as an open follow-up (deferred to `desktop-connection-management` feature).
- [ ] `adr/readme.md` index has rows for 007 and 008 in the same shape as existing rows.
- [ ] Manual review by user: read both ADRs, confirm wording matches the agreed picks.
- [ ] `dotnet build` and `dotnet test` still 0 errors / 33 tests pass (no code changed; sanity check).

## References

- ADRs: `adr/002-module-layering.md`, `adr/005-freeze-fsharp-dsl.md`, `adr/006-msdi-container.md`
- Methodology: `docs/methodology/dotnet-cli.md` (sibling stack recipe — the WPF recipe in step 02 will mirror its shape)
- Mockups: `docs/design/desktop-ui/readme.md` (the "Decisions already locked" block summarises what ADR-007 / ADR-008 must capture)
- Depends on: nothing (first step of the feature)
