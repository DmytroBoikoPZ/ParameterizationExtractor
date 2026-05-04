# 09 — desktop-graph-viz — MainWindow wire-up + sample.bws update + manual smoke

## Goal

Replace the Graph-tab placeholder with `<graph:GraphView/>`. Extend `MainWindowViewModel` to inject `GraphViewModel`, hand it the workspace + decrypted password from `OnWorkspaceChanged`, and let `Hydrate` fire `RefreshAsync` automatically. Update `examples/sample.bws` so the tab has something to render. Manual smoke walks the end-to-end UX. Mirrors the seed-tab wire-up step exactly.

## Track

`desktop` (WPF / C#) + `tests` + `examples`.

## What Exists

- `GraphView` (step 08), `GraphViewModel` (step 06).
- `MainWindowViewModel` — currently 8-param ctor (post-seed-tab); receives a 9th param for `GraphViewModel`.
- `MainWindow.xaml` — Graph tab is a placeholder `<TextBlock>`.
- `examples/sample.bws` — `PatientClearing` script with `Patient` root + `LookupCountry` standalone. The Graph tab will visualize the FK subgraph reachable from `dbo.Patient`.
- Pattern exemplar: `desktop-seed-tab` step 06 (8-param ctor + Hydrate + tab swap + sample update + manual smoke).

## What to Build

### `MainWindowViewModel.cs` — 9th param + Hydrate wiring

Constructor signature gains `GraphViewModel graph` (9th parameter). Property: `public GraphViewModel Graph { get; }`. Ctor body assigns. `OnWorkspaceChanged(WorkspaceModel? value)` extends to call `Graph.Hydrate(value, plaintextForGraph)` after `Seed.Hydrate(value, plaintextForSeed)` (same `plaintextForSeed` value can be reused; rename to `plaintextForVms` for clarity, OR pass independently — pick the cleaner shape during implementation).

The `Graph.Hydrate` call internally fires `RefreshCommand.ExecuteAsync(null)` (step 06's `Hydrate` does this). No save handler is needed for `GraphViewModel` v1 (the VM doesn't persist anything; layout choice is in-memory only).

### `MainWindow.xaml` — Graph-tab content swap

- Add `xmlns:graph="clr-namespace:Quipu.ParameterizationExtractor.Desktop.Views.Graph"`.
- Replace:
  ```xml
  <TabItem Header="Graph">
    <TextBlock Margin="20" Opacity="0.7" Text="Graph tab — implemented in desktop-graph-viz / desktop-graph-tab"/>
  </TabItem>
  ```
  with:
  ```xml
  <TabItem Header="Graph">
    <graph:GraphView DataContext="{Binding Graph}"/>
  </TabItem>
  ```

### `examples/sample.bws` — verify rendering

Sample already has `PatientClearing` script with `Patient` root. No edit required if `Patient` has FKs in the test DB (likely yes). Verify by manual smoke.

If desired (operator's call), add a second script demonstrating multi-seed reachability — but step 09 should stay focused.

### Tests

**Existing test fixtures must compile** under the 9-param ctor. Update three sites — same pattern as seed-tab step 06:

- `Tests/Desktop/MainWindowViewModelOpenTests.cs`: `NewVm()` factory adds `GraphViewModel` to the ctor call. Build a real `GraphViewModel` with `FakeGraphBuilder`, `FakeUiDispatcher`, `FakeDialogService`, `NullLoggerFactory.Instance`.
- `Tests/Desktop/EditConnectionFlowTests.cs`: same.
- `Tests/Desktop/NewWorkspaceCommandTests.cs`: same.

New tests:

`Tests/Desktop/MainWindowViewModelGraphHydrationTests.cs`:

1. `OnWorkspaceChanged_HydratesGraphWithDecryptedPassword` — load workspace whose `PasswordEncrypted` decrypts to `"hunter2"` via `InMemoryPasswordProtector`; assert `FakeGraphBuilder.ConnectionStringsCalled[0]` contains `Password=hunter2`.
2. `OnWorkspaceChanged_DecryptFailure_PassesNullPlaintextToGraph` — corrupt cipher; assert `Password=` is absent (or empty value) in the connection string passed to the builder.
3. `OnWorkspaceChanged_NullWorkspace_ClearsGraph` — set workspace then null; assert `Graph.Reachable == null` and `Graph.Nodes.Count == 0`.
4. `OnWorkspaceChanged_GraphBuilderThrows_StatusFailedNoCrash` — `FakeGraphBuilder.EnqueueException(new DatabaseExplorerException("login failed"))`; load workspace; assert `Graph.Status` starts with `"Failed:"`.

DI smoke: covered by step 06's `DesktopHost_ResolvesGraphViewModel`. No new DI smoke here.

### Manual smoke (mandatory — operator-driven)

- `dotnet build "ParameterizationExtractor.Desktop"`. `dotnet run --project ParameterizationExtractor.Desktop -- examples/sample.bws`.
- App opens; switch to Graph tab.
- Wait for "Loading…" → see nodes appear (Patient seed at top per hierarchical layout).
- Click a node → `SelectedNodeId` updates (verify via debugger or a temporary `<TextBlock Text="{Binding SelectedNodeId}"/>` somewhere in the view).
- Switch layout dropdown to "Layered" → graph re-lays out.
- Switch to "ForceDirected" → graph re-lays out.
- Switch back to Seed tab and back to Graph tab → state preserved (Singleton).
- Close workspace → graph clears.
- Reopen workspace → graph rebuilds.
- Logged in checklist Notes (date + outcome bullets).

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-08 N + 4 (new hydration tests). All existing tests pass under the 9-param ctor.
- Manual smoke per above.

## Acceptance Criteria

- [ ] `MainWindowViewModel` ctor takes 9 params (added `GraphViewModel`); `OnWorkspaceChanged` propagates to `Graph.Hydrate(value, plaintextForGraph)`.
- [ ] `MainWindow.xaml` Graph tab hosts `<graph:GraphView/>` (not the placeholder TextBlock).
- [ ] Three existing fixtures (`MainWindowViewModelOpenTests`, `EditConnectionFlowTests`, `NewWorkspaceCommandTests`) updated for 9-param ctor without test-logic changes.
- [ ] All 4 new `MainWindowViewModelGraphHydrationTests` pass.
- [ ] No tripwires introduced.
- [ ] Manual smoke walked end-to-end (load → render → click node → layout swap → close → reopen). Logged in checklist Notes with date.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Mockup: [`docs/design/desktop-ui/05-graph.md`](../../docs/design/desktop-ui/05-graph.md).
- Pattern exemplar: `desktop-seed-tab` step 06 (`MainWindowViewModelSeedHydrationTests` shape).
- Depends on: [06 — GraphViewModel](./06-desktop-graph-viz-graph-view-model.md), [08 — GraphView](./08-desktop-graph-viz-graph-view.md).
