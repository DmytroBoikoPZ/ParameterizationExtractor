# 008 — Desktop UI controls — MahApps.Metro chrome, AvalonEdit code editor, no docking lib initially

## Status

Accepted

## Context

[ADR-007](./007-desktop-wpf-stack.md) locks the desktop framework, MVVM library, app composition, and testing approach. This ADR locks the user-visible control set: the chrome library that themes the window and provides ready-made UI primitives (dialogs, flyouts, buttons), the code-editor control that hosts SQL text in several screens, and the docking model for splitting the shell into resizable regions.

WPF's built-in controls render dated by default — fine for a quick utility, jarring for a tool the operator spends hours in. The desktop UI mockups ([`docs/design/desktop-ui/`](../docs/design/desktop-ui/readme.md)) show modern flat chrome, themed dialogs, a slide-in inspector pane, and multi-line SQL editing in several places. We need a chrome library and a code editor; we do **not** need a docking framework for the layout drawn in those mockups.

As with [ADR-007](./007-desktop-wpf-stack.md), AI-driven implementation is the constraint. The picks favour mature libraries with large training corpora, stable APIs, and ample first-party documentation.

## Decision

We will use:

- **Chrome / theming library:** **MahApps.Metro 2.4.x.** MahApps provides themed `MetroWindow` chrome (replaces the default `Window`), themed dialogs, the `Flyout` primitive used for the slide-in inspector pane on the Graph tab, and a flat-modern Metro/Win10-style theme system. It is MIT-licensed, has a 10+ year track record, and a deep example base on GitHub and Stack Overflow. Theme dictionaries are loaded once in `App.xaml`'s merged-dictionary block; per-window theme loading is forbidden by the recipe.
- **Code editor control:** **AvalonEdit (`ICSharpCode.AvalonEdit`).** The de facto standard WPF code-editor control. Used in SharpDevelop and dozens of OSS apps. Supports SQL syntax highlighting out of the box, line numbering, find-replace. We wrap it in a project-internal `SqlEditor` UserControl and bind through a dependency property — ViewModels never reference AvalonEdit's `TextDocument` or `TextEditor` types. This isolation is enforced by a `### WPF` tripwire in `CLAUDE.md`.
- **Docking / pane layout:** **none initially.** The mockups call for a tab strip, a single content area, and an occasional slide-in pane (handled by MahApps `Flyout`). A `Grid` with `RowDefinition` / `ColumnDefinition` and `GridSplitter` covers every layout in the design doc. We deliberately defer adding AvalonDock or Dragablz until a concrete need arises — those libraries are reversible additions, but adding them prematurely means coupling to one library's docking model before we know whether the layout even needs docking.
- **Test-time dependency on the desktop project:** **FluentAssertions** is added to the existing `Tests/` project. Pure ergonomic improvement on top of NUnit; same scope as ADR-007.
- **Required from the (future) graph viz library:** support for **edge-level pick / click events.** The library choice itself is deferred (see follow-up below), but this constraint is locked here so the future ADR can't pick a library that fails it.

Alternatives considered:

- **Material Design In XAML Toolkit** — material design look. Solid, well-documented, MIT. Skipped because Material aesthetic on a Windows-native developer tool reads less native than Metro/Fluent. Easy to switch later if the user prefers.
- **WPF UI (lepoco) / ModernWpf** — Fluent / Win11 look. Newer, smaller training corpus than MahApps. Possible if Fluent is wanted; less safe AI-wise.
- **DevExpress / Telerik / Syncfusion** — commercial control suites. Excellent quality but versioned APIs that drift, paid licensing (Syncfusion Community is free for indie), and AI training data lags. Skipped unless a specific control gap forces a re-think.
- **Plain WPF without a chrome library** — zero deps but the visual result is jarring; user-facing chrome would need to be hand-themed.
- **AvalonDock / Dragablz upfront** — premature for the current mockups. Reconsider when the layout grows beyond what `Grid` + `GridSplitter` handles.
- **CodeEditorControl-WPF, RoslynPad's editor, hosting Monaco via WebView2** — alternatives to AvalonEdit. AvalonEdit has the deepest integration history, smallest dependency footprint, and a maintained .NET-native code path; the others either narrow to C# (RoslynPad) or pull in WebView2 + JavaScript (Monaco).

## Consequences

- **Easier:** every screen in the mockups can be built with one chrome lib + one editor control + native WPF layout primitives. No layout-engine learning curve. AI training data on MahApps and AvalonEdit is dense, so generated code lands close to idiomatic on the first try.
- **Harder:** locking the chrome to MahApps means re-skinning the app later (e.g. to Material or Fluent) is a non-trivial migration — not a `Theme="Fluent"` swap. We accept this; the user is the operator, and the operator picked Metro.
- **Open follow-ups:**
  - **Graph visualisation library** — Microsoft.Msagl, GraphX for .NET, or hand-rolled Canvas with a custom hierarchical layout. **Deferred to the `desktop-graph-viz` feature.** Constraint locked here: chosen library must support edge-level pick/click events so the v2 inspector ↔ edge-click upgrade path stays cheap (see [`docs/design/desktop-ui/05-graph.md`](../docs/design/desktop-ui/05-graph.md)).
  - **Password-at-rest strategy for connection credentials** — DPAPI on save with an opt-out checkbox is the agreed direction (see [`docs/design/desktop-ui/readme.md`](../docs/design/desktop-ui/readme.md)). **Deferred to the `desktop-connection-management` feature**, which will land its own ADR.
  - **Docking library** — revisit only if a future feature genuinely requires user-rearrangeable panes (current mockups do not).
