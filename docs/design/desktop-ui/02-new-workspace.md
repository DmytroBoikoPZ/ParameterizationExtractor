# 02 — New workspace dialog

> Captures: M2 from the mockup pass. Status: draft.
>
> Modal dialog launched from [01 — Startup](./01-startup.md) "New workspace…" or from `File → New workspace`. Same form is reused as "Edit connection" once a workspace is open.

## Mockup — SQL auth

```
┌─ New workspace ─────────────────────────────────────────────[X]─┐
│  Workspace name:  [ patient-clearing                       ]    │
│  Save to:         [ C:\Workspaces\                  ][ … ]      │
│                                                                 │
│  ─── Source connection ───────────────────────────────────────  │
│  Server:          [ 129.212.168.210,1433                   ]    │
│  Database:        [ budzdorov_Core                       v ]    │
│  Auth:            ( ) Windows   (•) SQL                         │
│  User:            [ sa                                     ]    │
│  Password:        [ ************                          ]     │
│  [✓] Store credentials (DPAPI-encrypted in the workspace file)  │
│                                                                 │
│       [ Test connection ]   OK Connected — 372 tables, 489 FKs  │
│                                                                 │
│                              [ Cancel ]   [ Create workspace ] │
└─────────────────────────────────────────────────────────────────┘
```

## Mockup — Windows auth

```
┌─ New workspace ─────────────────────────────────────────────[X]─┐
│  Workspace name:  [ patient-clearing                       ]    │
│  Save to:         [ C:\Workspaces\                  ][ … ]      │
│                                                                 │
│  ─── Source connection ───────────────────────────────────────  │
│  Server:          [ 129.212.168.210,1433                   ]    │
│  Database:        [ budzdorov_Core                       v ]    │
│  Auth:            (•) Windows   ( ) SQL                         │
│                                                                 │
│       [ Test connection ]   OK Connected — 372 tables, 489 FKs  │
│                                                                 │
│                              [ Cancel ]   [ Create workspace ] │
└─────────────────────────────────────────────────────────────────┘
```

## Implications

- Connection lives **inside** the workspace JSON (locked L5).
- **Auth radio** drives field visibility: Windows → User/Password/Store-credentials hidden; SQL → all visible.
- **Password at rest:** when "Store credentials" is checked (default), the password is DPAPI-encrypted on save. When unchecked, the workspace file holds an empty password and the user is prompted on every open. (Locked L6 — full ADR lands inside the `desktop-connection-management` feature.)
- "Database" dropdown is populated only after the server / auth fields validate; reuses the connection editor's "Test connection" path.
- The dialog is the same one reused for "Edit connection" later, with the workspace name / save path read-only.
- "Create workspace" runs the connection test, loads metadata (`MSSQLSourceSchema.GetMetaData`), writes the initial JSON, and transitions to [03 — Main shell](./03-shell.md).

## Components used

- [ConnectionEditor](./components.md#connectioneditor) — server / db / auth / user / pwd + Test button. Reused on [08 — Run](./08-run.md) for the target connection.
- [PathPicker](./components.md#pathpicker) — file path with `…` browse button.

## Open questions specific to this screen

- **First-run save path default.** `Documents\SqlBuldozer\Workspaces\` or `%USERPROFILE%\.buldozer\workspaces\`? *(Pass 3.)*
- **Database list discovery.** `sys.databases` filtered by accessible-to-current-login, or free-form text? *(Pass 3.)*

> Resolved: password storage = DPAPI on save with opt-out checkbox (L6). Windows-auth UX = hide User/Password (Pass 1 inconsistency fix — both mockups now reflect it).
