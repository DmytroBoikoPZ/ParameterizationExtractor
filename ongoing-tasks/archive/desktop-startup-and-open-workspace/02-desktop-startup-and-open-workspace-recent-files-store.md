# 02 — desktop-startup-and-open-workspace — IRecentFilesStore + JSON impl

## Goal

Add a per-user recent-workspaces store backed by `%APPDATA%\SqlBuldozer\recent-files.json`. Capped at 5 entries, de-duplicated by canonical path, atomic write. **No UI consumer yet** — that lands in step 03. Step 02 produces a registered, resolvable, tested store ready for injection.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`ParameterizationExtractor.Desktop/Services/Workspace/JsonWorkspaceStore.cs`](../../ParameterizationExtractor.Desktop/Services/Workspace/JsonWorkspaceStore.cs) — pattern exemplar for a JSON-backed local store (uses the engine's `JsonOptions.Default`).
- [`ParameterizationExtractor.Logic/Configs/Json/JsonOptions.cs`](../../ParameterizationExtractor.Logic/Configs/Json/JsonOptions.cs) — shared `JsonSerializerOptions` (camelCase + comments + trailing commas). Reuse, do **not** create a desktop-local copy.
- [`Tests/Desktop/WorkspaceStoreTests.cs`](../../Tests/Desktop/WorkspaceStoreTests.cs) — pattern exemplar for tests of a JSON store.
- Mockup [`docs/design/desktop-ui/01-startup.md`](../../docs/design/desktop-ui/01-startup.md) implications — cap-at-5 (mockup question; this feature locks 5), `%APPDATA%\SqlBuldozer\` location.

## What to Build

### `Services/RecentFiles/RecentFile.cs`

```csharp
internal sealed record RecentFile(string Path, DateTime LastOpenedUtc);
```

Namespace `Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles`. The `Path` property is the **canonicalised** full path (output of `Path.GetFullPath`); callers don't need to canonicalise themselves.

### `Services/RecentFiles/IRecentFilesStore.cs`

```csharp
internal interface IRecentFilesStore
{
    /// Returns the recent files, most-recent first, capped at MaxItems.
    Task<IReadOnlyList<RecentFile>> GetAsync(CancellationToken ct = default);

    /// Adds (or moves to top) the given path. Path is canonicalised internally.
    /// On disk-write failure, logs a warning and silently swallows — recents is a UX cache, not authoritative.
    Task PushAsync(string path, CancellationToken ct = default);

    /// Empties the list and persists.
    Task ClearAsync(CancellationToken ct = default);
}
```

Add a `public const int MaxItems = 5;` on `IRecentFilesStore` so consumers (Welcome view, tests) reference the same constant.

### `Services/RecentFiles/JsonRecentFilesStore.cs`

- `internal sealed class JsonRecentFilesStore : IRecentFilesStore`.
- Two constructors:
  - `public JsonRecentFilesStore(ILogger<JsonRecentFilesStore> log)` — production: file path is `Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), "SqlBuldozer", "recent-files.json")`.
  - `internal JsonRecentFilesStore(string filePath, ILogger<JsonRecentFilesStore> log)` — test seam.
- Internal record for serialisation: `private sealed record State(IReadOnlyList<RecentFile> Items);`. JSON shape:
  ```json
  { "items": [ { "path": "...", "lastOpenedUtc": "..." } ] }
  ```
  Note: top-level `{ "items": [...] }` is forward-compatible with future fields (e.g. `version`).
- `GetAsync`: load state, return `state.Items` (or empty list when file absent / unreadable). Treat JSON errors as "empty"; log warning. **Do not** delete the bad file — leave it so a developer can inspect it.
- `PushAsync(path)`:
  1. `path = Path.GetFullPath(path);` (canonicalisation).
  2. Load current state.
  3. `var filtered = state.Items.Where(x => !string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)).ToList();`
  4. `filtered.Insert(0, new RecentFile(path, DateTime.UtcNow));`
  5. `var trimmed = filtered.Take(IRecentFilesStore.MaxItems).ToList();`
  6. Persist. Atomic write: write to `{filePath}.tmp` then `File.Move(tmp, filePath, overwrite: true)`. Ensure parent directory exists via `Directory.CreateDirectory(Path.GetDirectoryName(filePath)!)`.
  7. On write failure (`IOException`, `UnauthorizedAccessException`): log warning, swallow.
- `ClearAsync`: write an empty `State(Array.Empty<RecentFile>())`. Same atomic-write + log-and-swallow pattern.
- Use `JsonOptions.Default` from the engine; no desktop-local options.
- All file IO is `await using` + async (`SerializeAsync` / `DeserializeAsync`). No blocking.

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- `services.AddSingleton<IRecentFilesStore, JsonRecentFilesStore>();`

The default ctor's `%APPDATA%` resolution runs at first use; no path bound at registration time.

### Tests

New file `Tests/Desktop/RecentFiles/JsonRecentFilesStoreTests.cs`:

The tests use a temp file path per test (constructor with `filePath` parameter) and clean up in `[TearDown]`. Pattern mirrors how `WorkspaceStoreTests` handles ephemeral files.

1. `GetAsync_FileMissing_ReturnsEmpty`.
2. `PushAsync_FirstCall_CreatesFileWithSingleEntry` — also verifies the parent directory is created.
3. `PushAsync_TwoDistinctPaths_BothPresentMostRecentFirst`.
4. `PushAsync_DuplicatePath_MovesToTop_DoesNotGrow` — verify count stays at 1, `LastOpenedUtc` updated.
5. `PushAsync_DuplicatePath_CaseInsensitive` — `C:\X\W.bws` vs. `c:\x\w.bws` collapse to one entry.
6. `PushAsync_BeyondMax_TrimsToFive` — push 6 distinct paths, assert 5 remain, the oldest is gone.
7. `PushAsync_NonCanonicalPath_StoresCanonicalForm` — push `C:\foo\..\foo\x.bws`, assert stored path has no `..`.
8. `ClearAsync_RemovesAllEntries`.
9. `RoundTrip_SurvivesNewStoreInstance` — push via store A, dispose, new store B over same file → `GetAsync` returns the entry.
10. `GetAsync_FileIsInvalidJson_ReturnsEmpty_LogsWarning` — write `"not json"` to the file, assert empty list, assert one warning logged via a captured `ILogger` test double (NUnit uses a small inline `ListLogger<T>` if not already in tests; otherwise reuse if present).

New test in `Tests/Desktop/HostCompositionTests.cs`:

- `DesktopHost_ResolvesIRecentFilesStore` — DI smoke; assert resolved type is `JsonRecentFilesStore`.

New `Tests/Desktop/Fakes/InMemoryRecentFilesStore.cs`:

- `internal sealed class InMemoryRecentFilesStore : IRecentFilesStore` with a `List<RecentFile>` backing store. Mirrors `JsonRecentFilesStore` semantics (cap-at-5, de-dup by path case-insensitive, canonicalisation). Used by VM tests in step 03 so they don't touch the file system.
- Tests: skip — the WPF impl tests cover the behaviour; the in-memory fake is exercised through step 03's VM tests.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test "SQL Buldozer.sln"` — pre-step-01 70 + new (10 store tests + 1 DI smoke) = **81 tests**, all green.

## Acceptance Criteria

- [ ] `Services/RecentFiles/{IRecentFilesStore.cs, JsonRecentFilesStore.cs, RecentFile.cs}` exist; namespace and visibility per spec.
- [ ] `IRecentFilesStore.MaxItems` is `5` and is a `public const int` on the interface.
- [ ] `JsonRecentFilesStore` uses the engine's shared `JsonOptions.Default` (grep: `using Quipu.ParameterizationExtractor.Logic.Configs.Json` or equivalent).
- [ ] `PushAsync` canonicalises via `Path.GetFullPath` and de-duplicates case-insensitively.
- [ ] `PushAsync` writes atomically (`{path}.tmp` → `File.Move(.., overwrite: true)`); parent directory created if absent.
- [ ] On read of an invalid file, `GetAsync` returns empty + logs a warning (not an exception).
- [ ] All 10 `JsonRecentFilesStoreTests` pass.
- [ ] `DesktopHost_ResolvesIRecentFilesStore` passes.
- [ ] `InMemoryRecentFilesStore` exists in `Tests/Desktop/Fakes/`; mirrors the cap / de-dup / canonicalisation semantics.
- [ ] No `Thread.Sleep`, `.Result`, `.Wait()`, `async void`, `Console.WriteLine`, `TODO`/`FIXME` introduced.
- [ ] `dotnet build` 0 errors; `dotnet test` 81/81 pass.

## References

- ADR: [`adr/009-workspace-format.md`](../../adr/009-workspace-format.md) (rationale for shared `JsonOptions.Default`)
- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § Workspace store (pattern parallel)
- Mockup: [`docs/design/desktop-ui/01-startup.md`](../../docs/design/desktop-ui/01-startup.md) (locked: cap-at-5; `%APPDATA%\SqlBuldozer\`)
- Pattern exemplars in code: [`Services/Workspace/JsonWorkspaceStore.cs`](../../ParameterizationExtractor.Desktop/Services/Workspace/JsonWorkspaceStore.cs), [`Tests/Desktop/WorkspaceStoreTests.cs`](../../Tests/Desktop/WorkspaceStoreTests.cs)
- Depends on: nothing strictly — independent of step 01.
