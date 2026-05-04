namespace Quipu.ParameterizationExtractor.Desktop.Services.Security;

/// <summary>
/// One-way seam between VMs and the OS-level password-protection scheme.
/// The default impl (<see cref="DpapiPasswordProtector"/>) uses Windows DPAPI under
/// <c>DataProtectionScope.CurrentUser</c> per ADR-010. VMs and views never call
/// <c>System.Security.Cryptography.ProtectedData</c> directly.
/// </summary>
internal interface IPasswordProtector
{
    /// <summary>Encrypts <paramref name="plaintext"/> and returns base64 ciphertext.</summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/>. Throws
    /// <see cref="System.Security.Cryptography.CryptographicException"/> if the ciphertext was
    /// produced by a different user / machine, or has been tampered with.
    /// Throws <see cref="System.FormatException"/> if the input is not valid base64.
    /// </summary>
    string Unprotect(string ciphertext);
}
