# 01 — Startup / empty state

> Captures: M1 from the mockup pass. Status: draft.
>
> Shown when the app launches with no workspace open (first run or after closing one).

## Mockup

```
┌─ SQL Buldozer ─────────────────────────────────────────────────[_][□][X]┐
│ File  Edit  View  Workspace  Tools  Help                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│      ┌──────────────────────────────────────────────────────┐           │
│      │  No workspace open                                   │           │
│      │                                                      │           │
│      │    [ New workspace... ]   [ Open workspace... ]      │           │
│      │                                                      │           │
│      │    Recent:                                           │           │
│      │    • patient-export.bws        3 hours ago           │           │
│      │    • employee-clearing.bws     yesterday             │           │
│      └──────────────────────────────────────────────────────┘           │
│                                                                         │
├─────────────────────────────────────────────────────────────────────────┤
│ Ready                                                                   │
└─────────────────────────────────────────────────────────────────────────┘
```

## Implications

- A workspace = a `.bws` (or whatever extension) JSON file on disk. Single-document at a time.
- Recent list is backed by a small app-config file in `%APPDATA%\SqlBuldozer\` (per-user, machine-local).
- Menu bar (`File / Edit / View / Workspace / Tools / Help`) is shared with [03 — Main shell](./03-shell.md). Most items are disabled here until a workspace is open.

## Components used

- [WorkspaceShell](./components.md#workspaceshell) — chrome (menu / status bar). Empty content area in this state.
- [WorkspaceWelcome](./components.md#workspaceweclome) — the centred panel with New / Open / Recent.

## Open questions specific to this screen

- **Recent list size** — 5? 10? Configurable? *(Pass 3.)*
- **Drag-and-drop** — accept dropped `.bws` files onto the empty state to open? *(Pass 3.)*

> File extension locked: **`.bws`**. See [readme — Decisions already locked](./readme.md#file-format-l8).
