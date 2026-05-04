# 02 — desktop-seed-tab — IUiDispatcher + WPF impl + FakeUiDispatcher

## Goal

Land `IUiDispatcher` per the recipe pre-spec — first feature to need it (preview rows arrive on a `Task.Run` continuation in step 04). WPF impl wraps `Application.Current.Dispatcher`; `FakeUiDispatcher` runs every `InvokeAsync` inline so tests don't deal with a real Dispatcher. **No UI consumer yet** — step 04. Step 02 produces a registered, resolvable, tested service.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`docs/methodology/wpf-desktop.md` § Service abstractions](../../docs/methodology/wpf-desktop.md) — verbatim contract for `IUiDispatcher` (3 methods).
- Recipe's `Implementation timing` table shows `IUiDispatcher` as `desktop-seed-tab — pre-spec`. Step 05 of this feature flips it to `landed`.
- [`Services/Dialogs/`](../../ParameterizationExtractor.Desktop/Services/Dialogs/) — pattern exemplar for a desktop service folder + impl.
- [`Tests/Desktop/Fakes/`](../../Tests/Desktop/Fakes/) — pattern exemplar for test fakes.

## What to Build

### `Services/Threading/IUiDispatcher.cs`

Verbatim from the recipe pre-spec:

```csharp
namespace Quipu.ParameterizationExtractor.Desktop.Services.Threading;

internal interface IUiDispatcher
{
    /// True when the calling thread is the UI thread.
    bool CheckAccess();

    /// Marshal an action onto the UI thread. Awaits its completion.
    Task InvokeAsync(Action action);

    /// Marshal a function onto the UI thread. Awaits its result.
    Task<T> InvokeAsync<T>(Func<T> func);
}
```

### `Services/Threading/UiDispatcher.cs`

- `internal sealed class UiDispatcher : IUiDispatcher`.
- Constructor parameterless.
- `CheckAccess()` returns `Application.Current?.Dispatcher.CheckAccess() ?? true` — service-level `Application.Current` access (canonical comment).
- `InvokeAsync(Action)` and `InvokeAsync<T>(Func<T>)`:
  - If `Application.Current?.Dispatcher` is null (e.g. tests against a non-WPF host) → run inline via `Task.FromResult`/`Task.CompletedTask`.
  - Else if `dispatcher.CheckAccess() == true` → run inline (already on UI thread; no extra hop).
  - Else → `await dispatcher.InvokeAsync(action).Task` (or the typed variant).

The "run inline when no Application or already on UI thread" semantics keep the API safe in unit tests AND avoid pointless cross-thread hops in hot paths.

### `Services/Threading/FakeUiDispatcher.cs` ... wait — fakes go in tests

Actually: `FakeUiDispatcher` lives at `Tests/Desktop/Fakes/FakeUiDispatcher.cs` per the test-fake convention.

### `Tests/Desktop/Fakes/FakeUiDispatcher.cs`

- `internal sealed class FakeUiDispatcher : IUiDispatcher`.
- `CheckAccess()` returns `true` (always).
- `InvokeAsync(Action)`: invokes inline; returns `Task.CompletedTask`. Records that the call happened (counter / list, simple).
- `InvokeAsync<T>(Func<T>)`: invokes inline; returns `Task.FromResult(result)`. Records.

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- `services.AddSingleton<IUiDispatcher, UiDispatcher>();`

### Tests

Per `prompts/execution.md` — CODE: TDD cycle, but the WPF impl is "WPF UI is not unit-tested" per recipe; only DI smoke + the fake's tests exercise behaviour.

New file `Tests/Desktop/Threading/UiDispatcherTests.cs`:

1. `DesktopHost_ResolvesIUiDispatcher` — DI smoke.

New file `Tests/Desktop/Fakes/FakeUiDispatcherTests.cs`:

2. `InvokeAsync_Action_RunsInline` — pass an action that mutates a captured int; assert mutation visible immediately.
3. `InvokeAsync_Func_ReturnsResult` — pass a func returning 42; assert result.
4. `CheckAccess_AlwaysReturnsTrue` — sanity.
5. `Records_CallCounts` — invoke twice; assert counter is 2.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-01 N + 5 = N + 5, all green.
- The WPF `UiDispatcher` has a "no Application.Current → run inline" guard so tests resolving it through DI don't crash, even though they don't exercise the dispatcher path proper.

## Acceptance Criteria

- [ ] `Services/Threading/{IUiDispatcher.cs, UiDispatcher.cs}` exist; visibility per recipe.
- [ ] `UiDispatcher.InvokeAsync` runs inline when on UI thread; marshals via `Application.Current.Dispatcher.InvokeAsync` otherwise; safe when Application.Current is null.
- [ ] `Application.Current` access carries the canonical comment.
- [ ] `IUiDispatcher` registered Singleton in `DesktopHost.cs`.
- [ ] `FakeUiDispatcher` exists in `Tests/Desktop/Fakes/`; runs everything inline; records calls.
- [ ] All 5 new tests pass.
- [ ] No `MessageBox.Show` / `IConfiguration[..]` / `Console.WriteLine` / `TODO` / `FIXME` introduced.
- [ ] `dotnet build` 0 errors; `dotnet test` green.

## References

- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § Service abstractions, § Threading.
- Pattern exemplars: `Services/Dialogs/`, `Tests/Desktop/Fakes/FakeDialogService.cs`.
- Depends on: nothing within this feature (independent of step 01).
