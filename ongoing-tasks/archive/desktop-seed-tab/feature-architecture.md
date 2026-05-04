# Desktop Seed Tab — Architecture Overview

> First feature to land `IUiDispatcher`, the foundational `SqlEditor` (AvalonEdit wrapper), the reusable `TablePicker` and `RowsPreviewGrid`, and the `IDatabaseExplorer` engine seam. Multi-script from day one.

---

## 1. Pipeline / Integration

```
Workspace loaded                                     (existing)
   ↓
MainWindowViewModel.OnWorkspaceChanged(ws)
   ↓
SeedViewModel.Hydrate(ws)                            (NEW)
   ├── Load each ws.Package.Scripts[i] into a fresh ScriptEditorViewModel
   └── Refresh TablePicker.Tables via IDatabaseExplorer.ListTablesAsync(ws.Source → connStr)

User selects a Script → ScriptEditorViewModel becomes active
   ├── Edits ScriptName / RootSchema / RootTable / SeedQuery
   │     each setter:
   │       1. Mutates VM observable property
   │       2. Schedules a debounced save-back (500ms)
   │       3. Save-back: write VM state into ws.Package.Scripts[i] →
   │                    IWorkspaceStore.SaveAsync(ws, _currentPath)
   │
   └── Clicks [ Run preview ]
         RunPreviewCommand
           ├── status = "Loading…"
           ├── (background) await IDatabaseExplorer.PreviewQueryAsync(connStr, SeedQuery, 200, ct)
           │     ↑ engine opens SqlConnection, executes SELECT, materialises up to 200 rows
           ├── continuation: await _ui.InvokeAsync(() => {
           │     PreviewResult = result;
           │     PreviewStatus = $"{result.Rows.Count} rows" + (result.Truncated ? " (truncated)" : "");
           │     })
           └── on cancel: PreviewStatus = "Cancelled"; on failure: PreviewStatus = "Failed: {message}"
```

| Stage | What changes | Where |
|-------|--------------|-------|
| Engine | New `IDatabaseExplorer` + `MSSqlDatabaseExplorer` | `Logic/Schema/` |
| Threading | `IUiDispatcher` interface + WPF impl + `FakeUiDispatcher` | `Desktop/Services/Threading/`, `Tests/Desktop/Fakes/` |
| Reusable controls | `Controls/{SqlEditor, TablePicker, RowsPreviewGrid}/` | Desktop project |
| Seed tab | View + VM pair + child VM | `Desktop/Views/Seed/` |
| Wire-up | `SeedViewModel` Singleton + `ScriptEditorViewModel` Transient; MainWindow swaps Seed tab content | `DesktopHost.cs`, `MainWindow.xaml`, `MainWindowViewModel.cs` |
| Highlighting | T-SQL highlighting registered once at startup | `App.xaml.cs` |

---

## 2. Component diagram

```
+----------------------------------------------------+
|  ParameterizationExtractor.Desktop                 |
|                                                    |
|  Views/Seed/                                       |
|   ├── SeedView.xaml                                |
|   └── SeedViewModel  (Singleton)                   |
|         ├── Scripts: ObservableCollection<        |
|         │      ScriptEditorViewModel>              |
|         ├── SelectedScript                         |
|         ├── AddScriptCommand                       |
|         ├── RemoveScriptCommand                    |
|         └── Hydrate(Workspace)                     |
|              ↓ creates one per Package.Scripts[i]  |
|         +-------------------------------------+    |
|         | ScriptEditorViewModel (Transient)   |    |
|         | ├── ScriptName                      |    |
|         | ├── RootSchema, RootTable           |    |
|         | ├── SeedQuery                       |    |
|         | ├── PreviewResult, PreviewStatus    |    |
|         | ├── RunPreviewCommand (cancellable) |    |
|         | └── debounced save-on-blur          |    |
|         +-------------------------------------+    |
|                                                    |
|  Controls/                                         |
|   ├── SqlEditor   (wraps AvalonEdit)               |
|   ├── TablePicker (combobox over IDatabaseExplorer)|
|   └── RowsPreviewGrid (DataGrid materialiser)      |
|                                                    |
|  Services/Threading/                               |
|   ├── IUiDispatcher                                |
|   └── UiDispatcher (wraps Application.Current.Dispatcher)
+----------------------------------------------------+
                              │
                              │ IDatabaseExplorer
                              ↓
+----------------------------------------------------+
|  ParameterizationExtractor.Logic                   |
|  Schema/                                           |
|   ├── IDatabaseExplorer                            |
|   ├── TableRef, PreviewResult                      |
|   └── MSSqlDatabaseExplorer                        |
|        ├── ListTablesAsync (uses sys.tables×       |
|        │     sys.schemas — same join as            |
|        │     MSSQLSourceSchema after step          |
|        │     engine-schema 02)                     |
|        └── PreviewQueryAsync                       |
|              opens SqlConnection                   |
|              executes user SQL                     |
|              materialises up to maxRows            |
|              stringifies all cells                 |
|              returns PreviewResult                 |
+----------------------------------------------------+
```

No new project references. Desktop still references `Common` + `Logic`.

---

## 3. Multi-script handling

`Package.Scripts` is `List<SourceForScript>`. Hydration:

```
SeedViewModel.Hydrate(Workspace ws):
  Scripts.Clear()
  foreach (s in ws.Package.Scripts):
    Scripts.Add(new ScriptEditorViewModel(s, _explorer, _ui, _store, _save, _log))
  SelectedScript = Scripts.FirstOrDefault()
```

`ScriptEditorViewModel` holds a reference to its source `SourceForScript` (or copies the relevant fields). On save-back, it writes into `ws.Package.Scripts[i]` — the index is preserved because `Add` appends in order.

**Add:** creates `new SourceForScript { ScriptName = "NewScript{N}", Query = "" }` + a fresh `ScriptEditorViewModel` wrapping it; appends to both `ws.Package.Scripts` and `Scripts`. **Remove:** removes from both. **Rename:** updates both. All three trigger save-back.

The engine's `SourceForScript.RootRecordsToExtract` (or equivalent — verify against the model) holds the root table reference. Decision: the seed tab manages exactly **one root per script** (today's `RecordsToExtract` typically is one table). If the engine model supports >1 root per script, the UI hides that complexity v1; future feature can expose it.

---

## 4. Dispatcher marshalling — `IUiDispatcher` debut

The preview run is the first place where:
1. The work happens off the dispatcher (`Task.Run` inside `MSSqlDatabaseExplorer.PreviewQueryAsync` via `await conn.OpenAsync()` continuation).
2. The result mutates an observable collection / property bound to UI.

The pattern (per recipe spec):

```csharp
[RelayCommand(IncludeCancelCommand = true)]
private async Task RunPreviewAsync(CancellationToken ct)
{
    PreviewStatus = "Loading…";
    try
    {
        var result = await _explorer.PreviewQueryAsync(connStr, SeedQuery, 200, ct).ConfigureAwait(false);
        await _ui.InvokeAsync(() =>
        {
            PreviewResult = result;
            PreviewStatus = $"{result.Rows.Count} rows" + (result.Truncated ? " (truncated)" : "");
        });
    }
    catch (OperationCanceledException) { ... }
    catch (Exception ex) { ... }
}
```

`ConfigureAwait(false)` on the engine call lets the continuation run on a thread-pool thread; `_ui.InvokeAsync` brings the observable mutation back. `FakeUiDispatcher` runs inline so tests don't need a Dispatcher.

Without the explicit `_ui.InvokeAsync`, `[RelayCommand]`'s default `ConfigureAwait(true)` would also work — but the recipe wants explicit marshalling so the dispatcher dependency is visible. **Decision: explicit `_ui.InvokeAsync` for any continuation that touches observable state from a `Task.Run`-style call.** Documented in step 04.

---

## 5. SqlEditor — AvalonEdit isolation

```
+-----------------------------------------+
|  Controls/SqlEditor/SqlEditorView.xaml  |
|                                         |
|  <UserControl>                          |
|    <avalon:TextEditor                   |
|      x:Name="Editor"                    |
|      SyntaxHighlighting="TSQL" ... />   |
|  </UserControl>                         |
|                                         |
|  Code-behind:                           |
|  - InitializeComponent()                |
|  - DependencyProperty: Text (string)    |
|  - DependencyProperty: IsReadOnly (bool)|
|  - DependencyProperty: IsSingleLine     |
|  - PropertyChanged callbacks bridge     |
|    DP ↔ Editor.Text / Editor.IsReadOnly |
|  - Subscribe to Editor.TextChanged →    |
|    raise PropertyChanged on Text DP     |
+-----------------------------------------+
```

**Tripwire:** ViewModels see only `Text` (string) and `IsReadOnly` (bool). They never touch `TextDocument`, `TextEditor`, `ICSharpCode.AvalonEdit.*`. The `Editor.TextChanged` ↔ DP bridge is the only place AvalonEdit types are visible — confined to `SqlEditorView.xaml.cs`.

T-SQL highlighting: `App.xaml.cs` `OnStartup` calls `HighlightingManager.Instance.RegisterHighlighting("TSQL", new[] { ".sql" }, ...)` once at app start. The XAML `SyntaxHighlighting="TSQL"` lookup picks it up.

`IsSingleLine = true` mode: hides line numbers, suppresses Enter (turns it into Tab), adjusts height. Used by future `WhereFilterEditor`. Not exercised by the Seed tab's seed-query editor (multi-line) but the slot exists.

---

## 6. State model

`SeedViewModel` (Singleton):

| Member | Type | Source |
|---|---|---|
| `Scripts` | `ObservableCollection<ScriptEditorViewModel>` | hydrated from `Workspace.Package.Scripts` |
| `SelectedScript` | `ScriptEditorViewModel?` | user selection |
| `IsHydrated` | computed `bool` | `Workspace != null` |
| `EmptyStateMessage` | `string` | "No scripts yet. Click + Add to create one." when `Scripts.Count == 0` |
| `AddScriptCommand` | `IRelayCommand` | append fresh script |
| `RemoveScriptCommand` | `IRelayCommand<ScriptEditorViewModel?>` | remove by reference |
| `RenameScriptCommand` | `IRelayCommand` | inline-edit the selected script's name |
| `Hydrate(WorkspaceModel?)` | method | called from `MainWindowViewModel.OnWorkspaceChanged` |

`ScriptEditorViewModel` (Transient):

| Member | Type | Notes |
|---|---|---|
| `ScriptName` | `string` | save-on-blur |
| `RootSchema` | `string` | written via TablePicker selection |
| `RootTable` | `string` | written via TablePicker selection |
| `SeedQuery` | `string` | save-on-blur (debounced) |
| `PreviewResult` | `PreviewResult?` | transient — never persisted |
| `PreviewStatus` | `string` | transient |
| `IsRunningPreview` | computed `bool` | from `RunPreviewCommand.IsRunning` |
| `RunPreviewCommand` | `IAsyncRelayCommand` | `[RelayCommand(IncludeCancelCommand = true)]` |
| `RunPreviewCancelCommand` | source-gen | morphs button label "Run preview ↔ Cancel" |

Computed: `RootDisplay => $"{RootSchema}.{RootTable}"` for picker display fallback.

---

## 7. Save-on-blur lifecycle

- Each setter on `ScriptEditorViewModel` (except `PreviewResult`/`PreviewStatus`/`PreviewCommand` state) fires the `OnAnyEditableChanged()` partial callback.
- `OnAnyEditableChanged` cancels the previous debounce CTS and schedules a 500ms-delayed `SaveAsync()` continuation:
  - Updates the `Workspace.Package.Scripts[i]` from VM state.
  - Calls `_save(Workspace)` — a delegate the parent `SeedViewModel` provides at construction (mirrors the `WelcomeViewModel.Bind(Func<string,Task>)` pattern from `desktop-startup-and-open-workspace`).
- `_save` ultimately invokes `MainWindowViewModel`'s save delegate, which calls `_store.SaveAsync(workspace, _currentPath)` with try/catch around `IOException` / `UnauthorizedAccessException` (analogous to `JsonRecentFilesStore`'s tolerant write).
- Save failures surface via `IDialogService.ShowMessageAsync("Save failed", ex.Message)` and the in-memory edit stays — operator doesn't lose work.

The 500ms debounce avoids a write per keystroke. Test fakes set the debounce to 0ms via an injected `IDebounceTimer` (or just suppress the timer by passing `TimeSpan.Zero`) so tests don't sleep.

---

## 8. Engine seam contract (`IDatabaseExplorer`)

```csharp
public interface IDatabaseExplorer
{
    Task<IReadOnlyList<TableRef>> ListTablesAsync(string connectionString, CancellationToken ct = default);
    Task<PreviewResult> PreviewQueryAsync(string connectionString, string sql, int maxRows, CancellationToken ct = default);
}

public sealed record TableRef(string Schema, string Name);

public sealed record PreviewResult(
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<string?[]> Rows,
    bool Truncated);
```

Visibility: `public` (engine surface — Desktop consumes via project reference).

`MSSqlDatabaseExplorer.ListTablesAsync` returns ordered by `(Schema, Name)`. Filters `is_ms_shipped = 0` (mirrors `MSSQLConnectionTester`).

`MSSqlDatabaseExplorer.PreviewQueryAsync` runs the user's SQL against the connection. Reads up to `maxRows + 1` rows (so it can tell if the result was truncated); returns `Rows.Count == maxRows` and `Truncated = true` when the +1 row exists. Cells are stringified via `Convert.ToString(value, CultureInfo.InvariantCulture)` so NULLs become `null`, dates become ISO strings, etc. **No type-aware rendering** in this feature.

Failure modes: `SqlException` / `InvalidOperationException` / `ArgumentException` are **caught** and packaged into `PreviewResult` with `Truncated = false` and an `ErrorMessage` field — wait, actually no, the contract returns `PreviewResult` only. Decision: failures throw `DatabaseExplorerException(message, inner)` (a new public type in `Logic/Schema/`); the VM catches and renders. Cleaner than overloading `PreviewResult`. `OperationCanceledException` propagates.

---

## 9. Threading & Cancellation

- `RunPreviewCommand` is `[RelayCommand(IncludeCancelCommand = true)]` on `async Task RunPreviewAsync(CancellationToken ct)` — CTK.MVVM gen produces `RunPreviewCommand` + `RunPreviewCancelCommand`.
- `MSSqlDatabaseExplorer.PreviewQueryAsync` calls `await conn.OpenAsync(ct)` and `await reader.ReadAsync(ct)` — every async observes the token.
- Cancellation surfaces as `OperationCanceledException` → VM catches → status = "Cancelled".
- `IUiDispatcher` is the ONLY way the VM marshals back to UI. No `Dispatcher.Invoke` anywhere in VMs (tripwire).
- `Save-on-blur` runs on the dispatcher (debounce timer is a `DispatcherTimer`-equivalent abstracted away). Save IO itself is async; the await runs on the dispatcher (no `ConfigureAwait(false)` because we want continuations on UI thread).

---

## 10. Risks & open questions

- **Highlight registration in App startup.** AvalonEdit's `HighlightingManager` is process-global. Calling it twice is idempotent. Test pollution risk: tests don't construct WPF `Application`, so the registration never runs in tests. Confirmed safe.
- **Schema-aware table list.** Depends on `engine-schema-aware-resolution` step 02 (`MSSQLSourceSchema.GetMetaData` populates Schema). `MSSqlDatabaseExplorer` runs the same `sys.tables × sys.schemas` query; could share the SQL with `MetaDataInitializer`. Decision: keep separate — they're the same shape today but may diverge (MetaDataInitializer reads columns + relations too; explorer needs only names). Document.
- **Save-on-blur loops.** Setters trigger save → save mutates `Workspace.Package.Scripts[i]` → does that trigger any other observer that loops back? Audit via tests: `Hydrate` after a save must not re-Hydrate (it should be idempotent if the Scripts collection isn't structurally changed). Pin with a regression test.
- **`PreviewResult` size.** 200 rows × N columns × stringified cells → could be large for wide tables. Per architecture: the hard cap is rows; column count isn't artificially capped (but the DataGrid's perf will suffer at 200+ columns — uncommon in practice). Operator-visible degradation is acceptable.
- **Debounce timer abstraction.** Don't introduce yet another DI seam unless tests need it. Use a private `CancellationTokenSource`-based `Task.Delay(500, cts.Token)` pattern. Tests pass `TimeSpan.Zero` via a constructor parameter.
- **Connection-string assembly for explorer calls.** The Seed tab needs to build a connection string from `Workspace.Source` + the (possibly absent) decrypted password. Decision: lift the `BuildConnectionString()` logic from `ConnectionEditorViewModel` into a small static helper `WorkspaceConnectionStringBuilder.Build(WorkspaceSource source, string? plaintextPassword)` so both consumers share it. Lives in `Desktop/Services/Workspace/`.
- **What if `WorkspaceSource.Auth == "sql"` and password is missing/decrypt-failed?** The `BuildConnectionString` returns a string without `Password=`; the engine call fails with login error; the VM surfaces "Failed: Login failed for user 'sa'" via `PreviewStatus` — the operator's signal to Edit-connection. We do NOT prompt automatically.

---

## 11. Security & isolation

- **No new trust boundaries.** Engine explorer uses the same SQL Server the operator already authenticated to.
- **Decrypted password lifetime.** `MainWindowViewModel.OpenWorkspaceAsync` decrypts the password on load; `SeedViewModel` reads it (via the connection-string builder) when invoking `IDatabaseExplorer`. Plaintext lives only in process memory. Cleared on Close.
- **SQL injection surface.** The seed query is operator-supplied free-form SQL — that's the feature. It's executed in the operator's own SQL session against the operator's DB. No injection concern; it's a SQL editor by design.
- **Preview output rendering.** Cells are stringified by the engine before crossing the boundary. No HTML, no script execution — `DataGrid` displays text. No XSS surface.

---

## 12. What this feature does NOT build

- No per-node strategy editing (graph tab).
- No standalone tables / pre-post scripts (extras tab).
- No SQL syntax / semantic validation in the editor.
- No automatic re-auth prompt.
- No type-aware preview rendering.
- No general Save / Save-As feature.
- No `sys.databases` enumeration.
- No live-typing preview.
