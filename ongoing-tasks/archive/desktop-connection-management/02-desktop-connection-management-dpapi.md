# 02 — desktop-connection-management — DPAPI password protector

## Goal

Land `IPasswordProtector` + `DpapiPasswordProtector` per ADR-010 (step 01). **No UI consumer yet** — step 03 (ConnectionEditor) and step 04 (dialog flows) consume it. Step 02 produces a registered service with round-trip tests + a tamper / scope test that pins the security expectations.

## Track

`desktop` (WPF / C#) + `tests`.

## What Exists

- [`adr/010-desktop-password-at-rest.md`](../../adr/010-desktop-password-at-rest.md) (from step 01) — the contract this impl satisfies.
- [`ParameterizationExtractor.Desktop/Services/Workspace/JsonWorkspaceStore.cs`](../../ParameterizationExtractor.Desktop/Services/Workspace/JsonWorkspaceStore.cs) — pattern exemplar for a desktop service in `Services/...`.
- `System.Security.Cryptography.ProtectedData` ships in-box on .NET 10 Windows; no NuGet needed.
- [`Tests/Desktop/Fakes/`](../../Tests/Desktop/Fakes/) — `FakeDialogService` and friends as exemplars for test fakes.

## What to Build

### `Services/Security/IPasswordProtector.cs`

```csharp
namespace Quipu.ParameterizationExtractor.Desktop.Services.Security;

internal interface IPasswordProtector
{
    /// <summary>Encrypts <paramref name="plaintext"/> and returns base64 ciphertext.</summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/>. Throws <see cref="System.Security.Cryptography.CryptographicException"/>
    /// if the ciphertext was produced by a different user / machine, or has been tampered with.
    /// </summary>
    string Unprotect(string ciphertext);
}
```

Visibility: `internal` (Desktop-only).

### `Services/Security/DpapiPasswordProtector.cs`

- `internal sealed class DpapiPasswordProtector : IPasswordProtector`.
- No constructor dependencies.
- `Protect(plaintext)`:
  1. `var bytes = Encoding.UTF8.GetBytes(plaintext);`
  2. `var cipher = ProtectedData.Protect(bytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);`
  3. `return Convert.ToBase64String(cipher);`
- `Unprotect(ciphertext)`:
  1. `var cipher = Convert.FromBase64String(ciphertext);`
  2. `var bytes = ProtectedData.Unprotect(cipher, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);`
  3. `return Encoding.UTF8.GetString(bytes);`
- No try/catch — `CryptographicException` and `FormatException` propagate per the interface contract. Callers (workspace load) handle them.

### DI registration ([`DesktopHost.cs`](../../ParameterizationExtractor.Desktop/DesktopHost.cs))

- `services.AddSingleton<IPasswordProtector, DpapiPasswordProtector>();`

### `Tests/Desktop/Fakes/InMemoryPasswordProtector.cs`

- `internal sealed class InMemoryPasswordProtector : IPasswordProtector`.
- `Protect(plaintext) → Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext))`.
- `Unprotect(b64) → Encoding.UTF8.GetString(Convert.FromBase64String(b64))`.
- **Not a real protect** — base64-encode-only round-trip. VM tests use this so they can produce predictable ciphertexts without DPAPI bound to the test runner's user account.

### Tests

Per `prompts/execution.md` — CODE: TDD cycle.

New file `Tests/Desktop/Security/DpapiPasswordProtectorTests.cs`:

1. `Protect_Then_Unprotect_RoundTripsPlaintext` — `var sut = new DpapiPasswordProtector(); sut.Unprotect(sut.Protect("hunter2")).Should().Be("hunter2");`
2. `Protect_SameInputTwice_ProducesDifferentCiphertexts` — DPAPI encryption is not deterministic (it includes a per-call salt). `sut.Protect("x") != sut.Protect("x")`.
3. `Unprotect_TamperedCiphertext_ThrowsCryptographic` — flip a byte mid-string before passing back. Asserts `CryptographicException`.
4. `Unprotect_NotBase64_ThrowsFormat` — pass `"not base64!"`; asserts `FormatException`.
5. `Protect_EmptyString_RoundTrips` — `""` round-trips back to `""`.
6. `Protect_UnicodePlaintext_RoundTrips` — `"пароль 🔒"` round-trips intact (UTF-8).

New test in `Tests/Desktop/HostCompositionTests.cs`:

- `DesktopHost_ResolvesIPasswordProtector` — DI smoke; assert resolved type is `DpapiPasswordProtector`.

`InMemoryPasswordProtector` itself doesn't need its own test fixture — it's exercised through later VM tests in steps 03-04.

### Verification

- `dotnet build` — 0 errors.
- `dotnet test` — pre-step-01 99 + 7 = **106 tests**, all green.

## Acceptance Criteria

- [ ] `Services/Security/{IPasswordProtector.cs, DpapiPasswordProtector.cs}` exist; visibility per spec.
- [ ] `DpapiPasswordProtector` uses `DataProtectionScope.CurrentUser` and `Encoding.UTF8`. No catches; exceptions propagate.
- [ ] DI registration in `DesktopHost.cs` (Singleton).
- [ ] All 6 `DpapiPasswordProtectorTests` pass.
- [ ] `InMemoryPasswordProtector` exists in `Tests/Desktop/Fakes/`.
- [ ] `DesktopHost_ResolvesIPasswordProtector` passes.
- [ ] No new tripwires introduced.
- [ ] `dotnet build` 0 errors; `dotnet test` 106/106 pass.

## References

- ADR: [`adr/010-desktop-password-at-rest.md`](../../adr/010-desktop-password-at-rest.md) (from step 01).
- Methodology: [`docs/methodology/wpf-desktop.md`](../../docs/methodology/wpf-desktop.md).
- Pattern exemplars: [`Services/Workspace/`](../../ParameterizationExtractor.Desktop/Services/Workspace/), [`Tests/Desktop/Fakes/`](../../Tests/Desktop/Fakes/).
- Depends on: [01 — ADR-010 + engine seam](./01-desktop-connection-management-engine-seam-and-adr.md) (only for the ADR).
