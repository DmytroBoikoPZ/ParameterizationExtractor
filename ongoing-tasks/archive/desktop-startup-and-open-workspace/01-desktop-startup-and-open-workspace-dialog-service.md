# 01 — desktop-startup-and-open-workspace — IDialogService + WPF impl + test fake

## Goal

Land the `IDialogService` abstraction defined in [`docs/methodology/wpf-desktop.md` § Service abstractions](../../docs/methodology/wpf-desktop.md), its WPF implementation (MahApps `MetroDialogManager` + `Microsoft.Win32` file pickers), and the queue-driven `FakeDialogService` test double. **No UI consumer yet** — that's step 03's job. Step 01 produces a registered, resolvable, tested service ready to be injected.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`docs/methodology/wpf-desktop.md` § Service abstractions](../../docs/methodology/wpf-desktop.md) — verbatim contract for `IDialogService` and the test-fake convention.
- [`ParameterizationExtractor.Desktop/DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs) — Generic Host composition root; pattern exemplar for adding singletons.
- [`ParameterizationExtractor.Desktop/Services/Workspace/`](../../ParameterizationExtractor.Desktop/Services/Workspace/) — folder layout precedent for service abstractions (`I*.cs` + impl in same folder).
- MahApps.Metro 2.4.10 — provides `MahApps.Metro.Controls.Dialogs.DialogCoordinator` for confirm/message popups owned by a `MetroWindow`.
- `Microsoft.Win32.OpenFileDialog` / `SaveFileDialog` — built into WPF for file pickers.
- [`Tests/Desktop/HostCompositionTests.cs`](../../Tests/Desktop/HostCompositionTests.cs) — DI-resolution test pattern.

## What to Build

### `Services/Dialogs/IDialogService.cs`

Verbatim from the recipe pre-spec:

```csharp
internal interface IDialogService
{
    Task ShowMessageAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> OpenFileAsync(string title, string filter);
    Task<string?> SaveFileAsync(string title, string filter, string? defaultName = null);
}
```

Namespace `Quipu.ParameterizationExtractor.Desktop.Services.Dialogs`.

### `Services/Dialogs/DialogService.cs`

- `internal sealed class DialogService : IDialogService`.
- Constructor takes `MahApps.Metro.Controls.Dialogs.IDialogCoordinator` (MahApps singleton `DialogCoordinator.Instance`) and an `ILogger<DialogService>`.
- `ShowMessageAsync` — uses `IDialogCoordinator.ShowMessageAsync(this, title, message)` where `this` is the active `MetroWindow`. The dialog coordinator needs the window context: pass `Application.Current.MainWindow` cast to `MetroWindow`. **VM rule reminder:** this is a service, not a VM — using `Application.Current` here is allowed (the tripwire forbids it in VMs). Add a one-line comment: `// Service-level access to Application.Current is allowed; VMs never see it.`
- `ConfirmAsync` — uses `ShowMessageAsync` with `MessageDialogStyle.AffirmativeAndNegative`; returns `result == MessageDialogResult.Affirmative`.
- `OpenFileAsync` — instantiates `Microsoft.Win32.OpenFileDialog` with `Title`, `Filter`, `Multiselect = false`, `CheckFileExists = true`. Returns `dialog.FileName` if `dialog.ShowDialog() == true`, else `null`. Wrap in `Task.FromResult` — the WPF dialog is synchronous.
- `SaveFileAsync` — same shape with `Microsoft.Win32.SaveFileDialog`, `OverwritePrompt = true`. `defaultName` populates `dialog.FileName`. Returns `dialog.FileName` or `null`.

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- `services.AddSingleton<MahApps.Metro.Controls.Dialogs.IDialogCoordinator>(_ => MahApps.Metro.Controls.Dialogs.DialogCoordinator.Instance);`
- `services.AddSingleton<IDialogService, DialogService>();`

### `Tests/Desktop/Fakes/FakeDialogService.cs` (new)

Per the recipe's test-fake convention:

- `internal sealed class FakeDialogService : IDialogService`.
- Three response queues: `Queue<bool> _confirms = new();`, `Queue<string?> _openResults = new();`, `Queue<string?> _saveResults = new();`. (Messages have no return value.)
- Methods to enqueue:
  - `void EnqueueConfirmResponse(bool result)`.
  - `void EnqueueOpenFileResponse(string? path)`.
  - `void EnqueueSaveFileResponse(string? path)`.
- Public records `record DialogCall(string Method, string Title, string Message, string? Filter)` and a `List<DialogCall> Calls { get; } = new();` so tests assert "the user was prompted with title=X, filter=Y".
- `ShowMessageAsync` — records the call; returns `Task.CompletedTask`.
- `ConfirmAsync` — records the call; returns `_confirms.Dequeue()` (throws `InvalidOperationException("FakeDialogService: no enqueued confirm response")` if queue empty — better than returning a default and hiding test bugs).
- `OpenFileAsync` / `SaveFileAsync` — record + dequeue with the same throw-if-empty contract.

The `Tests/Desktop/Fakes/` folder doesn't exist yet — create it. No need to add a `<Folder Include>` in the csproj; SDK-style projects auto-include `.cs` files.

### Tests

New file `Tests/Desktop/DialogServiceTests.cs`:

- `DesktopHost_ResolvesIDialogService` — DI smoke: build host, resolve `IDialogService`, assert it's `DialogService`.

The WPF `DialogService` itself is **not unit-tested** — per recipe `WPF UI is not unit-tested`. Its behaviour is exercised through manual smoke + the consumer tests in step 03 that use `FakeDialogService`.

New file `Tests/Desktop/Fakes/FakeDialogServiceTests.cs`:

- `Confirm_ReturnsEnqueuedResponse` — Enqueue `true`, call `ConfirmAsync`, assert `true`.
- `Confirm_RecordsCall` — call `ConfirmAsync("Title", "Message")` (after enqueue), assert `Calls` has one entry with method `"Confirm"`, title, message.
- `OpenFile_ReturnsEnqueuedPath` — Enqueue `@"C:\x.bws"`, call `OpenFileAsync`, assert path returned.
- `OpenFile_RecordsCallWithFilter` — assert `Calls[0].Filter` matches.
- `Confirm_NoResponseEnqueued_Throws` — call `ConfirmAsync` without enqueueing, assert throws `InvalidOperationException` with descriptive message.

### Verification

- `dotnet build "SQL Buldozer.sln"` — 0 errors.
- `dotnet test "SQL Buldozer.sln"` — pre-existing 64 + new (DI smoke 1 + fake tests 5) = **70 tests**, all green.

## Acceptance Criteria

- [ ] `Services/Dialogs/IDialogService.cs` exists with the four methods exactly as the recipe spec lists them.
- [ ] `Services/Dialogs/DialogService.cs` implements all four methods. `DialogService` is `internal sealed`.
- [ ] `IDialogCoordinator` and `IDialogService` are registered in `DesktopHost.cs`.
- [ ] `Tests/Desktop/Fakes/FakeDialogService.cs` exists with three enqueue methods, a `Calls` list, and throw-if-empty dequeue semantics.
- [ ] `DesktopHost_ResolvesIDialogService` passes.
- [ ] All 5 `FakeDialogServiceTests` pass.
- [ ] No `MessageBox.Show` introduced anywhere (grep verifies). Service-level access to `Application.Current` exists only in `DialogService.cs` and carries a one-line comment.
- [ ] No `IConfiguration[..]`, no `Console.WriteLine`, no `TODO`/`FIXME`, no `Thread.Sleep`/`.Result`/`.Wait()`/`async void` introduced.
- [ ] `dotnet build` 0 errors; `dotnet test` 70/70 pass.

## References

- ADR: [`adr/007-desktop-wpf-stack.md`](../../adr/007-desktop-wpf-stack.md), [`adr/008-desktop-ui-controls.md`](../../adr/008-desktop-ui-controls.md)
- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md) § Service abstractions, § Dialogs / file pickers, § Testing
- Pattern exemplars in code: [`Services/Workspace/`](../../ParameterizationExtractor.Desktop/Services/Workspace/), [`Tests/Desktop/HostCompositionTests.cs`](../../Tests/Desktop/HostCompositionTests.cs)
- Depends on: nothing (first step).
