# 04 — desktop-workspace-format — Workspace wrapper + `IWorkspaceStore` + sample + docs

## Goal

Land the desktop-side `IWorkspaceStore` that reads/writes `.bws` workspace files, ship a sample workspace ported from `ClearingPackage.xml`, and update cross-cutting docs (`overview.md`, `dotnet-cli.md`, `wpf-desktop.md`) to reflect the new reality (workspace store is real, not pre-spec).

After this step the desktop has a workspace data layer behind a stable interface. UI features pick up `IWorkspaceStore` from DI.

## Track

`desktop` (WPF / C# — desktop-side services, no UI views).

## What Exists

- After steps 01–03: ADR-009, design doc, engine JSON readers, characterisation parity. Engine input contract is settled.
- [`ParameterizationExtractor.Desktop/`](../../ParameterizationExtractor.Desktop/) project — Generic Host wired up via `DesktopHost.CreateApplicationBuilder()`. Adding services here is the standard registration pattern.
- [`docs/design/desktop-ui/02-new-workspace.md`](../../docs/design/desktop-ui/02-new-workspace.md) — the "New workspace" mockup defines what fields the wrapper holds (workspace name, source connection, password placeholder).
- [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) — recipe; `IWorkspaceStore` is named in `feature-architecture.md § 5` of the archived `desktop-skeleton` feature as a pre-spec seam.
- `examples/` folder — does **not** exist yet at the repo root; create it.
- [`ParameterizationExtractor/ClearingPackage.xml`](../../ParameterizationExtractor/ClearingPackage.xml) — source of truth for the sample's content.

## What to Build

### Desktop service additions (`ParameterizationExtractor.Desktop/Services/Workspace/`)

- **`WorkspaceModel`** POCO — desktop-side wrapper. Holds:
  - `int Version`         (default 1; corresponds to `$version` in the JSON wrapper)
  - `string Name`
  - `WorkspaceSource Source`     (server / database / auth / user / passwordEncrypted-as-null)
  - `Package Package`            (engine POCO from `Logic`)
  - `GlobalExtractConfiguration Global`     (engine POCO from `Logic`)
- **`WorkspaceSource`** POCO — connection metadata, no password handling logic. `passwordEncrypted` is a `string?` placeholder; semantics owned by the future `desktop-connection-management` feature. Document explicitly in an XML doc-comment.
- **`IWorkspaceStore`** interface — per the recipe pre-spec seam:
  ```csharp
  internal interface IWorkspaceStore
  {
      Task<WorkspaceModel> LoadAsync(string path, CancellationToken ct = default);
      Task SaveAsync(WorkspaceModel workspace, string path, CancellationToken ct = default);
  }
  ```
- **`JsonWorkspaceStore`** — concrete implementation. Uses `System.Text.Json` with `JsonNamingPolicy.CamelCase`. Embeds the engine's `Package` and `GlobalExtractConfiguration` directly — same `JsonSerializerOptions` as the engine readers from step 02 so there's no shape drift.
- **DI registration** in [`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs): `services.AddSingleton<IWorkspaceStore, JsonWorkspaceStore>();`. Singleton is correct — it's stateless.

### Sample workspace

- New folder `examples/` at the repo root.
- New file `examples/sample.bws` — JSON workspace whose `package` and `global` subtrees are a translation of [`ParameterizationExtractor/ClearingPackage.xml`](../../ParameterizationExtractor/ClearingPackage.xml). `source` block synthesised with the dev-DB connection metadata used by the characterisation harness (no password — the placeholder is `null`).
- The sample is **not** a runtime fixture; it's developer documentation. Tests load it for shape verification only.

### Tests (TDD)

- New test file `Tests/Desktop/WorkspaceStoreTests.cs`:
  - `JsonWorkspaceStore_RoundTrips_PreservesFields` — `Save` then `Load` an in-memory `WorkspaceModel`; assert all fields equal (FluentAssertions `BeEquivalentTo`).
  - `JsonWorkspaceStore_LoadsSampleBws` — load `examples/sample.bws`, assert non-null, assert `Package.Scripts` count and `Global.DefaultExtractStrategy` discriminator match the sample.
  - `JsonWorkspaceStore_RejectsUnknownVersion` — set `$version: 2`, assert load throws (closed-set version check).
  - `JsonWorkspaceStore_PasswordEncryptedField_RoundTripsAsNull` — placeholder semantics: writing `null`, reading back `null`. The field exists structurally but isn't populated. (Validates the schema slot reservation for the connection-management feature.)
- The smoke test from step 03 of `desktop-skeleton` (host composition) still runs and passes — `IWorkspaceStore` is now in the service collection.

### Cross-doc updates

- **`docs/architecture/overview.md` § 2 (Components):** mention `IWorkspaceStore` in the Desktop row's Purpose column. Reference graph block already has `Desktop → Common, Logic` — no change. Add a sentence under § 4 (Data Stores) about the new `*.bws` filesystem store.
- **`docs/architecture/overview.md` § 7 (Active ADRs):** add row for ADR-009.
- **`docs/methodology/dotnet-cli.md`:** in the SQL generation section (or a new "Configuration formats" section), document that `Package` and `GlobalExtractConfiguration` accept JSON via `JsonPackageReader` / `JsonGlobalConfigReader` in addition to XML. Cite ADR-009.
- **`docs/methodology/wpf-desktop.md`:** the `## Service abstractions — pre-spec` section's `Implementation timing` table — change `IWorkspaceStore` row from "deferred" to "implemented in `desktop-workspace-format`". The interface is no longer pre-spec; mention that the recipe's pre-spec section now covers only `IUiDispatcher` and `IDialogService`.
- **`CLAUDE.md`** — no tripwire changes expected. If the feature surfaces any new tripwire (e.g. "no `JsonSerializer.Deserialize<object>` — type-bind or fail"), add it; mirror to `.github/copilot-instructions.md` byte-identical.
- **`adr/readme.md`** — verify ADR-009 row from step 01 still consistent.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test "SQL Buldozer.sln"` — pre-existing tests + new workspace-store tests, all green.
- Manual verification: launch the desktop exe — should still open the empty MetroWindow, exit cleanly. The new service is registered but not used by any UI yet.

## Acceptance Criteria

- [ ] `WorkspaceModel`, `WorkspaceSource`, `IWorkspaceStore`, `JsonWorkspaceStore` exist under `ParameterizationExtractor.Desktop/Services/Workspace/`.
- [ ] DI registration added in `DesktopHost.cs`. The host-composition smoke test from `desktop-skeleton` extended (or a new test added) to assert `IWorkspaceStore` resolves.
- [ ] `examples/sample.bws` exists, is valid JSON, loads cleanly via `JsonWorkspaceStore.LoadAsync`, and its embedded `package` reflects `ClearingPackage.xml`.
- [ ] Round-trip test asserts every field of `WorkspaceModel` survives save→load.
- [ ] `passwordEncrypted` field round-trips as `null` (placeholder semantics).
- [ ] Unknown `$version` rejected with a clear exception.
- [ ] `docs/architecture/overview.md` § 2 / § 4 / § 7 updated.
- [ ] `docs/methodology/dotnet-cli.md` documents the JSON input path, citing ADR-009.
- [ ] `docs/methodology/wpf-desktop.md` `IWorkspaceStore` is no longer marked pre-spec.
- [ ] If `CLAUDE.md` is touched, `.github/copilot-instructions.md` is byte-identical (SHA256 verified).
- [ ] `dotnet build` 0 errors; `dotnet test` all green.
- [ ] Manual: desktop exe launches, opens window, closes cleanly. New service registered but not used by UI.
- [ ] Tripwire compliance: VMs / services don't reference WPF types (workspace store is plain `Task`-based; no `IUiDispatcher` needed).

## References

- ADR: [`adr/009-workspace-format.md`](../../adr/009-workspace-format.md)
- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md), [`docs/methodology/workspace-format.md`](../../docs/methodology/workspace-format.md)
- Architecture: [`docs/architecture/overview.md`](../../docs/architecture/overview.md)
- Pattern exemplar: existing `JsonPackageReader` from step 02 (same `JsonSerializerOptions`)
- Mockups for wrapper field shape: [`docs/design/desktop-ui/02-new-workspace.md`](../../docs/design/desktop-ui/02-new-workspace.md)
- Depends on: steps 01, 02, 03 must all be `[x]` first.
