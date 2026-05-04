#nullable enable
using System;
using System.Security.Cryptography;
using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Services.Security;

namespace Tests.Desktop.Security;

[TestFixture]
public class DpapiPasswordProtectorTests
{
    [Test]
    public void Protect_Then_Unprotect_RoundTripsPlaintext()
    {
        var sut = new DpapiPasswordProtector();

        var roundTripped = sut.Unprotect(sut.Protect("hunter2"));

        roundTripped.Should().Be("hunter2");
    }

    [Test]
    public void Protect_SameInputTwice_ProducesDifferentCiphertexts()
    {
        var sut = new DpapiPasswordProtector();

        var a = sut.Protect("x");
        var b = sut.Protect("x");

        a.Should().NotBe(b, "DPAPI includes a per-call salt");
    }

    [Test]
    public void Unprotect_TamperedCiphertext_ThrowsCryptographic()
    {
        var sut = new DpapiPasswordProtector();
        var cipher = sut.Protect("secret");
        // Flip one byte in the middle (still valid base64 chars).
        var bytes = Convert.FromBase64String(cipher);
        bytes[bytes.Length / 2] ^= 0x42;
        var tampered = Convert.ToBase64String(bytes);

        var act = () => sut.Unprotect(tampered);

        act.Should().Throw<CryptographicException>();
    }

    [Test]
    public void Unprotect_NotBase64_ThrowsFormat()
    {
        var sut = new DpapiPasswordProtector();

        var act = () => sut.Unprotect("not base64!");

        act.Should().Throw<FormatException>();
    }

    [Test]
    public void Protect_EmptyString_RoundTrips()
    {
        var sut = new DpapiPasswordProtector();

        sut.Unprotect(sut.Protect("")).Should().Be("");
    }

    [Test]
    public void Protect_UnicodePlaintext_RoundTrips()
    {
        var sut = new DpapiPasswordProtector();
        var input = "пароль 🔒 P@ss";

        sut.Unprotect(sut.Protect(input)).Should().Be(input);
    }
}
