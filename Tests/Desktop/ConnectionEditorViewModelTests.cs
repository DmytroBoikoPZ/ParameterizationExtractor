#nullable enable
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Controls.ConnectionEditor;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Connectivity;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class ConnectionEditorViewModelTests
{
    private FakeConnectionTester _tester = null!;
    private InMemoryPasswordProtector _protector = null!;

    [SetUp]
    public void Init()
    {
        _tester = new FakeConnectionTester();
        _protector = new InMemoryPasswordProtector();
    }

    private ConnectionEditorViewModel NewVm() =>
        new(_tester, _protector, NullLogger<ConnectionEditorViewModel>.Instance);

    [Test]
    public void Default_State_IsIdle_WindowsAuthOn_SqlAuthOff()
    {
        var vm = NewVm();

        vm.TestStatus.Should().Be(ConnectionTestStatus.Idle);
        vm.IsWindowsAuth.Should().BeTrue("Windows-auth is the safer default");
        vm.IsSqlAuth.Should().BeFalse();
        vm.IsTesting.Should().BeFalse();
        vm.StoreCredentials.Should().BeTrue();
    }

    [Test]
    public void IsSqlAuth_True_FlipsIsWindowsAuth()
    {
        var vm = NewVm();

        vm.IsSqlAuth = true;

        vm.IsWindowsAuth.Should().BeFalse();
        vm.IsSqlAuth.Should().BeTrue();
    }

    [Test]
    public void SwitchingToWindowsAuth_ClearsUserAndPassword()
    {
        var vm = NewVm();
        vm.IsSqlAuth = true;
        vm.User = "sa";
        vm.Password = "secret";

        vm.IsWindowsAuth = true;

        vm.User.Should().BeEmpty();
        vm.Password.Should().BeEmpty();
    }

    [Test]
    public void Populate_WithSqlAuthSource_PreFillsAllFields()
    {
        var vm = NewVm();
        var source = new WorkspaceSource
        {
            Server = "host,1433",
            Database = "DB",
            Auth = "sql",
            User = "sa",
            PasswordEncrypted = _protector.Protect("hunter2"),
        };

        vm.Populate(source, plaintextPassword: "hunter2");

        vm.Server.Should().Be("host,1433");
        vm.Database.Should().Be("DB");
        vm.IsSqlAuth.Should().BeTrue();
        vm.User.Should().Be("sa");
        vm.Password.Should().Be("hunter2");
        vm.StoreCredentials.Should().BeTrue();
    }

    [Test]
    public void ToWorkspaceSource_StoreCredentialsTrue_EncryptsPassword()
    {
        var vm = NewVm();
        vm.IsSqlAuth = true;
        vm.Server = "h";
        vm.Database = "d";
        vm.User = "sa";
        vm.Password = "hunter2";
        vm.StoreCredentials = true;

        var source = vm.ToWorkspaceSource();

        source.Auth.Should().Be("sql");
        source.User.Should().Be("sa");
        source.PasswordEncrypted.Should().NotBeNullOrEmpty();
        _protector.Unprotect(source.PasswordEncrypted!).Should().Be("hunter2");
    }

    [Test]
    public void ToWorkspaceSource_StoreCredentialsFalse_LeavesPasswordEncryptedNull()
    {
        var vm = NewVm();
        vm.IsSqlAuth = true;
        vm.User = "sa";
        vm.Password = "hunter2";
        vm.StoreCredentials = false;

        var source = vm.ToWorkspaceSource();

        source.PasswordEncrypted.Should().BeNull();
    }

    [Test]
    public void ToWorkspaceSource_WindowsAuth_LeavesUserAndPasswordEncryptedNull()
    {
        var vm = NewVm();
        vm.IsWindowsAuth = true;
        // even if some lingering text, it must not be persisted
        vm.User = "leftover";
        vm.Password = "leftover";

        var source = vm.ToWorkspaceSource();

        source.Auth.Should().Be("windows");
        source.User.Should().BeNull();
        source.PasswordEncrypted.Should().BeNull();
    }

    [Test]
    public async Task TestCommand_Success_SetsStatusToSuccess_PopulatesCounts()
    {
        var vm = NewVm();
        vm.Server = "h";
        vm.Database = "d";
        _tester.EnqueueResult(new ConnectionTestResult(true, 100, 200, null));

        await vm.TestCommand.ExecuteAsync(null);

        vm.TestStatus.Should().Be(ConnectionTestStatus.Success);
        vm.StatusMessage.Should().Contain("100 tables").And.Contain("200 FKs");
    }

    [Test]
    public async Task TestCommand_Failure_SetsStatusToFailure_UsesErrorMessage()
    {
        var vm = NewVm();
        vm.Server = "h";
        vm.Database = "d";
        _tester.EnqueueResult(new ConnectionTestResult(false, 0, 0, "Login failed for user 'sa'."));

        await vm.TestCommand.ExecuteAsync(null);

        vm.TestStatus.Should().Be(ConnectionTestStatus.Failure);
        vm.StatusMessage.Should().Contain("Login failed");
    }

    [Test]
    public async Task TestCommand_Cancellation_SetsStatusToCancelled()
    {
        var vm = NewVm();
        vm.Server = "h";
        vm.Database = "d";
        _tester.EnqueueException(new OperationCanceledException());

        await vm.TestCommand.ExecuteAsync(null);

        vm.TestStatus.Should().Be(ConnectionTestStatus.Cancelled);
        vm.StatusMessage.Should().Be("Cancelled");
    }

    [Test]
    public async Task TestCommand_BuildsConnectionStringFromFields()
    {
        var vm = NewVm();
        vm.Server = "myhost,1433";
        vm.Database = "mydb";
        vm.IsSqlAuth = true;
        vm.User = "sa";
        vm.Password = "p";
        _tester.EnqueueResult(new ConnectionTestResult(true, 1, 1, null));

        await vm.TestCommand.ExecuteAsync(null);

        _tester.ConnectionStringsCalled.Should().ContainSingle();
        _tester.ConnectionStringsCalled[0].Should()
            .Contain("myhost,1433")
            .And.Contain("mydb")
            .And.Contain("sa");
    }

    [Test]
    public void BuildConnectionString_WindowsAuth_DoesNotIncludePasswordToken()
    {
        var vm = NewVm();
        vm.Server = "h";
        vm.Database = "d";
        vm.IsWindowsAuth = true;

        var connStr = vm.BuildConnectionString();

        connStr.Should().NotContain("Password=");
        connStr.Should().Contain("Integrated Security=True");
    }
}
