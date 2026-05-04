# 08 — Run tab

> Captures: M8 from the mockup pass. Status: draft.
>
> One tab, three modes: dry-run (analyse only), generate `.sql` to disk, execute against a target connection. Covers point #5 of the user concept ("workspace to design the package") and the deferred "execute from the tool" follow-up.

## Mockup

```
┌─ Run ───────────────────────────────────────────────────────────────────────┐
│ Mode: ( ) Generate .sql to disk   (•) Dry-run (analyse only)                │
│        ( ) Execute against target connection [ none configured… ]           │
│                                                                             │
│ [ Start ]   Last run: 14:23 · 3.2s · 0 errors                              │
│                                                                             │
│ ┌──────────────────┬─────────┬───────────┬─────────────────┐                │
│ │ Table            │ Rows    │ INSERT KB │ Strategy        │                │
│ ├──────────────────┼─────────┼───────────┼─────────────────┤                │
│ │ Patient          │     1   │     0.4   │ FKDependency    │                │
│ │ Country          │     1   │     0.1   │ OnlyOneTable    │                │
│ │ Insurance        │     1   │     0.3   │ FKDependency    │                │
│ │ Visit            │    47   │    18.2   │ OnlyChildren    │                │
│ │ Payment          │    89   │    41.5   │ FKDependency    │                │
│ │ ...              │   ...   │   ...     │   ...           │                │
│ ├──────────────────┼─────────┼───────────┤                                  │
│ │ TOTAL            │ 1,847   │   312.4   │                                  │
│ └──────────────────┴─────────┴───────────┴─────────────────┘                │
│                                                                             │
│ Output:   [ View .sql v ]   [ Save to disk... ]                             │
│ Log:                                                                        │
│  14:23:01  INFO  Loaded 372 tables, 489 FKs                                 │
│  14:23:01  INFO  Seed: SELECT * FROM Patient WHERE LastName='Demo' →1 row   │
│  14:23:02  INFO  Walked Patient → Visit (47) → Payment (89)                 │
│  14:23:04  INFO  Generated 312.4 KB of T-SQL                                │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Implications

- **Three radio modes**, one results pane (macro decision #5):
  - **Dry-run** — invoke the engine, but never write to disk and never execute. Just produce the per-table breakdown + the would-be `.sql` text in memory for preview.
  - **Generate .sql to disk** — same as dry-run plus write the output to a chosen path.
  - **Execute against target** — run the produced SQL against a *second* (target) connection. Requires a configured target connection (separate from the workspace's source connection).
- The desktop calls into `Logic.PackageProcessor` for all three modes — the same engine path the CLI uses. This is why decision #4 in the [readme](./readme.md) lists "must be able to call `PackageProcessor` directly" as a hard requirement on the project shape.
- The breakdown table is populated from the engine's per-table extraction stats — already collected today; just needs to be exposed.
- The log is the in-app Serilog sink: same writes that go to the file sink, mirrored into a UI control.

## Components used

- [RunModePicker](./components.md#runmodepicker) — three-radio mode chooser.
- [TargetConnectionPicker](./components.md#targetconnectionpicker) — link/popover that references [ConnectionEditor](./components.md#connectioneditor) for the "execute against target" mode.
- [PerTableBreakdownGrid](./components.md#pertablebreakdowngrid) — the row/byte breakdown DataGrid.
- [SqlOutputPreview](./components.md#sqloutputpreview) — read-only `.sql` preview pane behind the "View .sql" toggle. Wraps [SqlEditor](./components.md#sqleditor) in read-only mode.
- [RunLogView](./components.md#runlogview) — append-only log pane bound to a Serilog UI sink.

## Open questions specific to this screen

- **Confirmation before execute** — required for execute-target mode? Multi-stage confirm (estimate, preview SQL, then run)? *(Pass 3.)*
- **Cancel a running operation** — engine doesn't take a `CancellationToken` today on `PackageProcessor`. Either a feature on its own or a UI no-op until the engine supports it. *(Pass 3.)*
- **Output path memory** — "Save to disk" should remember the last directory per workspace. *(Pass 3.)*
- **Schema diff before execute** — run a structural compare between source and target before allowing execute? *(Pass 3.)*
- **Streaming progress** — for large extractions, show progress as tables complete instead of a single end-of-run breakdown. *(Pass 3.)*

> Resolved: one tab with three radio modes (L4).
