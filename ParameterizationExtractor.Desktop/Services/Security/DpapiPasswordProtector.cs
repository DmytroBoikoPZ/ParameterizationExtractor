using System;
using System.Security.Cryptography;
using System.Text;

namespace Quipu.ParameterizationExtractor.Desktop.Services.Security;

internal sealed class DpapiPasswordProtector : IPasswordProtector
{
    public string Protect(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = ProtectedData.Protect(bytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipher);
    }

    public string Unprotect(string ciphertext)
    {
        var cipher = Convert.FromBase64String(ciphertext);
        var bytes = ProtectedData.Unprotect(cipher, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
