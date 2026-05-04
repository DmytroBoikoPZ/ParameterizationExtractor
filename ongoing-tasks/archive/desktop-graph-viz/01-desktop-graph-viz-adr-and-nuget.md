# 01 — desktop-graph-viz — ADR-012 + AutomaticGraphLayout NuGet

## Goal

Close ADR-008's deferred follow-up: pick the graph visualisation library. Write ADR-012 documenting `AutomaticGraphLayout` 1.1.12 (community Msagl fork), the constraint trail (edge-pick events for v2), and the alternatives considered. Add the NuGet package to the Desktop project. **No code yet uses the library** — step 05 builds the host control. Step 01 produces a decision document + a buildable solution that has the dependency available.

## Track

`adr` + `infra`.

## What Exists

- [`adr/008-desktop-ui-controls.md`](../../adr/008-desktop-ui-controls.md) — open follow-up `Graph visualisation library` named the constraint: chosen library must support edge-level pick / click events.
- [`adr/readme.md`](../../adr/readme.md) — index of accepted ADRs; line for ADR-012 to be added.
- [`docs/design/desktop-ui/05-graph.md`](../../docs/design/desktop-ui/05-graph.md) — M5 mockup; explicitly defers the library pick to this feature.
- `ParameterizationExtractor.Desktop.csproj` — already targets `net10.0-windows`; receives the new `<PackageReference>`.

## What to Build

### `adr/012-graph-visualisation-library.md`

- **Status:** Accepted.
- **Context:** ADR-008 deferred this; M5 calls for an automatic layout; v1 needs nodes + edges + node-state badges; v2 needs edge-pick events.
- **Decision:** `AutomaticGraphLayout` 1.1.12 (NuGet `AutomaticGraphLayout` + `AutomaticGraphLayout.Drawing` + `AutomaticGraphLayout.WpfGraphControl` — verify the WPF-specific package id at install time and document the actual id used).
- **Alternatives considered** (mirror ADR-008's table style):
  - `Microsoft.Msagl` 1.1.6 — official Microsoft Research; same API; last release 2017. Skipped because the community fork is more recently maintained while staying API-compatible (drop-in fallback).
  - `GraphX` (`Stride.GraphX.PCL.*`) — generic graph viz with multiple layout backends. Skipped because the abandoned-looking maintenance and the Stride-namespaced fork suggests instability.
  - Custom Canvas + hand-rolled hierarchical layout — full control; layout engine is multi-week work; skipped on YAGNI grounds.
  - Hosting `vis.js` / `cytoscape.js` via WebView2 — pulls JS runtime + interop seam; rejected (too heavy for a single tab).
- **Consequences:**
  - **Easier:** edge-pick events satisfied (AGL exposes `Edge`-level mouse events); hierarchical / layered / force-directed layouts available out-of-the-box; NuGet-first install (no native binaries beyond what Msagl ships).
  - **Harder:** AGL's API is C#-idiomatic but documented sparingly; future contributors may need to read source. We accept the risk because the API surface we touch is small (build a `DrawingGraph`, attach to `GraphViewer`).
  - **Tripwire:** AGL types live only inside `Controls/GraphHost/`. ViewModels never see `AutomaticGraphLayout.*` or `Microsoft.Msagl.*`. Mirrored from ADR-008's AvalonEdit isolation. Recipe section "Graph host" added in the cross-doc step.
  - **Reversibility:** dropping back to `Microsoft.Msagl` 1.1.6 is a `<PackageReference>` swap; the API is the same.

### Update `adr/readme.md`

- Add row: `| [012](012-graph-visualisation-library.md) | Graph visualisation library — AutomaticGraphLayout 1.1.12 (Msagl fork); edge-pick events satisfied | Accepted |`

### `ParameterizationExtractor.Desktop.csproj` — add NuGet

- `<PackageReference Include="AutomaticGraphLayout" Version="1.1.12" />`
- `<PackageReference Include="AutomaticGraphLayout.Drawing" Version="1.1.12" />`
- WPF-specific assembly: verify whether the WPF host is in `AutomaticGraphLayout.WpfGraphControl` or `AutomaticGraphLayout.Wpf`. Add the correct one. Document the resolved id in the ADR.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors. Package resolves on `net10.0-windows`.
- `dotnet test` — pre-existing N stays green (no consumer code yet).
- `adr/readme.md` row appears; ADR-012 file exists.

## Acceptance Criteria

- [ ] `adr/012-graph-visualisation-library.md` exists with Status / Context / Decision / Alternatives / Consequences sections.
- [ ] `adr/readme.md` index has the ADR-012 row.
- [ ] `ParameterizationExtractor.Desktop.csproj` has the AGL `<PackageReference>` entries.
- [ ] `dotnet build "SQL Buldozer.sln"` 0 errors.
- [ ] `dotnet test` green (no behavioural change from this step).
- [ ] No code consumes AGL yet — grep `AutomaticGraphLayout` / `Msagl` outside the csproj returns nothing.

## References

- ADR: [`adr/008-desktop-ui-controls.md`](../../adr/008-desktop-ui-controls.md) (follow-up trail), [`adr/readme.md`](../../adr/readme.md).
- Mockup: [`docs/design/desktop-ui/05-graph.md`](../../docs/design/desktop-ui/05-graph.md).
- Pattern exemplar: ADR-009 / ADR-010 / ADR-011 — short single-screen ADRs.
