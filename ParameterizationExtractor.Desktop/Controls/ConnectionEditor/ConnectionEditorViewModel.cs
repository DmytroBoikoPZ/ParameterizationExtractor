using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Desktop.Services.Security;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Connectivity;

namespace Quipu.ParameterizationExtractor.Desktop.Controls.ConnectionEditor;

internal sealed partial class ConnectionEditorViewModel : ObservableObject
{
    private readonly IConnectionTester _tester;
    private readonly IPasswordProtector _protector;
    private readonly ILogger<ConnectionEditorViewModel> _log;

    [ObservableProperty] private string _server = string.Empty;
    [ObservableProperty] private string _database = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSqlAuth))]
    private bool _isWindowsAuth = true;

    [ObservableProperty] private string _user = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _storeCredentials = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTesting))]
    private ConnectionTestStatus _testStatus = ConnectionTestStatus.Idle;

    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool IsSqlAuth
    {
        get => !IsWindowsAuth;
        set => IsWindowsAuth = !value;
    }

    public bool IsTesting => TestStatus == ConnectionTestStatus.Testing;

    public ConnectionEditorViewModel(
        IConnectionTester tester,
        IPasswordProtector protector,
        ILogger<ConnectionEditorViewModel> log)
    {
        _tester = tester;
        _protector = protector;
        _log = log;
    }

    public void Populate(WorkspaceSource source, string? plaintextPassword)
    {
        Server = source.Server;
        Database = source.Database;
        IsWindowsAuth = source.Auth == "windows";
        User = source.User ?? string.Empty;
        Password = plaintextPassword ?? string.Empty;
        // ADR-010: DPAPI password-at-rest with an OPT-OUT checkbox. Default to "store"
        // every time the dialog opens; the operator unchecks to skip persistence.
        // (The previous logic defaulted to false when the workspace had no prior
        // stored password — making first-time SQL-auth setup silently lose the
        // typed password after Save, which contradicts the opt-out model.)
        StoreCredentials = true;
        TestStatus = ConnectionTestStatus.Idle;
        StatusMessage = string.Empty;
    }

    public WorkspaceSource ToWorkspaceSource()
    {
        var source = new WorkspaceSource
        {
            Server = Server,
            Database = Database,
            Auth = IsWindowsAuth ? "windows" : "sql",
        };

        if (IsWindowsAuth)
        {
            source.User = null;
            source.PasswordEncrypted = null;
        }
        else
        {
            source.User = string.IsNullOrEmpty(User) ? null : User;
            source.PasswordEncrypted = StoreCredentials && !string.IsNullOrEmpty(Password)
                ? _protector.Protect(Password)
                : null;
        }

        return source;
    }

    public string BuildConnectionString()
    {
        var pseudoSource = new WorkspaceSource
        {
            Server = Server,
            Database = Database,
            Auth = IsWindowsAuth ? "windows" : "sql",
            User = IsWindowsAuth ? null : User,
        };
        return WorkspaceConnectionStringBuilder.Build(pseudoSource, IsWindowsAuth ? null : Password);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task TestAsync(CancellationToken ct)
    {
        TestStatus = ConnectionTestStatus.Testing;
        StatusMessage = "Testing…";

        try
        {
            var connStr = BuildConnectionString();
            var result = await _tester.TestAsync(connStr, ct).ConfigureAwait(true);

            if (result.Success)
            {
                TestStatus = ConnectionTestStatus.Success;
                StatusMessage = $"OK Connected — {result.TableCount} tables, {result.FkCount} FKs";
            }
            else
            {
                TestStatus = ConnectionTestStatus.Failure;
                StatusMessage = result.ErrorMessage ?? "Connection failed";
            }
        }
        catch (OperationCanceledException)
        {
            TestStatus = ConnectionTestStatus.Cancelled;
            StatusMessage = "Cancelled";
        }
        finally
        {
            _log.LogInformation("Connection test resulted in {Status} for {Server}/{Database}",
                TestStatus, Server, Database);
        }
    }

    partial void OnIsWindowsAuthChanged(bool value)
    {
        if (value)
        {
            User = string.Empty;
            Password = string.Empty;
        }
    }
}
