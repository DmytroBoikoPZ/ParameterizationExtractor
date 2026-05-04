# 03 — Main shell

> Captures: M3 from the mockup pass. Status: draft.
>
> The persistent IDE chrome shown whenever a workspace is open. Hosts the tab strip and the active tab content. Single-document — opening a different workspace replaces the current one (with a dirty-prompt if needed).

## Mockup

```
┌─ SQL Buldozer — patient-clearing.bws * ───────────────────────[_][□][X]─┐
│ File  Edit  View  Workspace  Tools  Help                                │
├─ [Run] [Dry-run] [Save]   budzdorov_Core @ 129.212.168.210 ●──────────── │
├─────────────────────────────────────────────────────────────────────────┤
│ [ Overview ][ Seed ][ Graph ][ Extras ][ Run ]                          │
│ ┌─────────────────────────────────────────────────────────────────────┐ │
│ │ Workspace:  patient-clearing                                        │ │
│ │ Source:     budzdorov_Core (372 tables · 489 FKs)                   │ │
│ │                                                                     │ │
│ │ Seed:       Patient · custom query · 1 row matched                  │ │
│ │ Path:       14 nodes configured · 3 pending                         │ │
│ │ Extras:     2 tables · 1 pre-script · 0 post-script                 │ │
│ │                                                                     │ │
│ │ Last dry-run:  14:23 — 1,847 rows / 11 tbls / 312 KB                │ │
│ │                                                                     │ │
│ │ !  3 nodes still pending — graph not fully designed                 │ │
│ │                                                                     │ │
│ │                                                                     │ │
│ └─────────────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────────────┤
│ * unsaved · last save 14:21                                              │
└──────────────────────────────────────────────────────────────────────────┘
```

## Implications

- **Tabbed shell**, not separate windows. Familiar VS-style. (Macro decision #1, locked L1.)
- **No left explorer tree** in v1. The graph + tab content carry the workspace structure. (Macro decision #2, locked L2.)
- Tab strip: `Overview / Seed / Graph / Extras / Run`. **Scripts content lives inside the Extras tab** ([07](./07-extras.md)) — it's not a separate tab.
- **Overview tab** is a read-only summary view of the workspace JSON — answers "what is this workspace, currently".
- **Toolbar** carries global Run / Dry-run / Save actions plus a connection indicator (also a clickable shortcut to "Edit connection").
- **Title-bar `*`** marks dirty state. Save is explicit. (Macro decision #8, locked L7.)
- **Status bar** carries workspace-scoped state only: dirty marker + last-save timestamp. Source schema details live on the Overview tab — no duplication.
- **Single-document.** New / Open from the `File` menu prompts the user about unsaved changes before replacing the current workspace.

## Components used

- [WorkspaceShell](./components.md#workspaceshell) — the chrome.
- [TabHost](./components.md#tabhost) — tab strip + tab content host.
- [OverviewView](./components.md#overviewview) — the Overview tab content.
- [ConnectionIndicator](./components.md#connectionindicator) — toolbar pill showing current source.
- [DirtyTitleBar](./components.md#dirtytitlebar) — `*` marker.

## Open questions specific to this screen

- **Overview "warnings" surface** — should pending-node warnings click through to the Graph tab with the offending nodes pre-selected? (Pass 3 — refine during the Overview-tab feature.)
- **`File` menu shape** — `New / Open / Save / Save As / Recent / Exit`. Standard, but the Recent submenu duplicates the [01 — Startup](./01-startup.md) recent list and they must stay in sync.
