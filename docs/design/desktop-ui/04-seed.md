# 04 — Seed tab

> Captures: M4 from the mockup pass. Status: draft.
>
> Where the user picks the root table and writes the seed query that selects the entry rows for the FK walk.

## Mockup

```
┌─ Seed ──────────────────────────────────────────────────────────────────────┐
│  Root (auto-detected from query): [ dbo.Patient                       v  ]  │
│                                                                             │
│  Type a SELECT — the root table is detected automatically.                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ SELECT *                                                                ││
│  │ FROM dbo.Patient                                                        ││
│  │ WHERE LastName = 'Demo' AND CreatedDate > '2024-01-01'                  ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│                                                                             │
│  [ Run ]   1 row matched · 0.12s                                            │
│                                                                             │
│  ┌─ Preview (first 20 rows) ────────────────────────────────────────────┐   │
│  │ Id    │ FirstName │ LastName │ DateOfBirth │ CreatedDate              │   │
│  │ 1042  │ Anna      │ Demo     │ 1985-04-12  │ 2024-08-15               │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│                                          [ Set as workspace seed ]          │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Implications

- **Root is auto-detected** from the seed query's first `FROM <table>` (or `FROM <schema>.<table>`). The picker shows the inferred value as a passive confirmation; manual override is allowed and surfaces a non-blocking mismatch hint banner under the picker when the operator's pick disagrees with the SQL. Engine inference is UI-only — `RootSchema` + `RootTable` remain the engine's authoritative seed.
- The workspace tracks **the seed query**, not the seed rows (macro decision #6). The query re-runs against fresh data on every dry-run / execute.
- Preview is **read-only** and **bounded** (default 20 rows). It exists to confirm the query targets what the user expected — it is not a data-browsing tool.
- "Set as workspace seed" is the explicit commit. Until pressed, the editor is scratch space and the workspace JSON is unchanged.
- "All rows from root table" mode = no `WHERE` — generates the seed query as `SELECT * FROM <table>`. Safe shortcut for small lookup-style roots.

## Components used

- [TablePicker](./components.md#tablepicker) — root table dropdown. Reused on [07 — Extras](./07-extras.md) for adding standalone tables.
- [SqlEditor](./components.md#sqleditor) — multi-line SQL textbox with light syntax colouring. Reused on [06 — Node inspector](./06-node-inspector.md) (where filter) and [07 — Extras](./07-extras.md) (scripts).
- [RowsPreviewGrid](./components.md#rowspreviewgrid) — read-only `DataGrid` for query results.

## Open questions specific to this screen

- **Multi-row seed.** The mockup shows a single matched row. Multi-row seeds are common (e.g. "all patients with LastName = 'Demo'") — the engine handles them. UI is fine, no change needed; calling out for the spec.
- **Seed query validation.** Should we lint that the SELECT actually targets the chosen Root table (or refuse mismatch)? Today the engine assumes it does.
- **Preview row cap.** 20 hard-coded? User-configurable (50/100/500)?
- **Cancellation.** Long seed query — is there a cancel button while it's running?
