using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.Connectivity;
using Tests.CharacterisationTests.Harness;

namespace Tests.EngineConnectivityTests;

[TestFixture]
public class MSSQLConnectionTesterTests
{
    private static MSSQLConnectionTester NewTester() =>
        new(NullLogger<MSSQLConnectionTester>.Instance);

    [Test]
    public async Task TestAsync_ValidConnection_ReturnsSuccessWithCounts()
    {
        var sut = NewTester();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var result = await sut.TestAsync(connStr, CancellationToken.None);

        result.Success.Should().BeTrue($"because the test DB must be reachable; got '{result.ErrorMessage}'");
        result.TableCount.Should().BeGreaterThan(0);
        result.FkCount.Should().BeGreaterThan(0);
        result.ErrorMessage.Should().BeNull();
    }

    [Test]
    public async Task TestAsync_InvalidServer_ReturnsFailureWithMessage()
    {
        var sut = NewTester();
        var connStr = "Server=nonexistent.invalid,1433;Database=Whatever;User Id=sa;Password=x;TrustServerCertificate=True";

        var result = await sut.TestAsync(connStr, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.TableCount.Should().Be(0);
        result.FkCount.Should().Be(0);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task TestAsync_BadCredentials_ReturnsFailureWithMessage()
    {
        var validConnStr = TestDbConfig.ResolveSourceConnectionString();
        var builder = new SqlConnectionStringBuilder(validConnStr)
        {
            UserID = "sa",
            Password = "definitely-not-the-real-password-9f23a",
            IntegratedSecurity = false,
        };

        var sut = NewTester();
        var result = await sut.TestAsync(builder.ConnectionString, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void TestAsync_CancelledBeforeOpen_ThrowsOperationCancelled()
    {
        var sut = NewTester();
        var connStr = TestDbConfig.ResolveSourceConnectionString();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.TestAsync(connStr, cts.Token);

        act.Should().ThrowAsync<OperationCanceledException>(
            "cancellation must propagate so callers can distinguish it from a logical failure");
    }

    [Test]
    public async Task TestAsync_MalformedConnectionString_ReturnsFailureWithoutThrowing()
    {
        var sut = NewTester();

        var result = await sut.TestAsync("this is not a connection string at all", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
