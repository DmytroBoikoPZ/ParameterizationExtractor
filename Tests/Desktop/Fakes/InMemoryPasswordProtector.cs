#nullable enable
using System;
using System.Text;
using Quipu.ParameterizationExtractor.Desktop.Services.Security;

namespace Tests.Desktop.Fakes;

/// <summary>
/// Test double for <see cref="IPasswordProtector"/>. Round-trips via base64 only — there is no
/// real protection. Lets VM tests produce predictable ciphertext without binding to the test
/// runner's Windows user account (which DPAPI would do).
/// </summary>
internal sealed class InMemoryPasswordProtector : IPasswordProtector
{
    public string Protect(string plaintext) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));

    public string Unprotect(string ciphertext) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(ciphertext));
}
