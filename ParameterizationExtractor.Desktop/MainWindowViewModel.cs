using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;
using Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles;
using Quipu.ParameterizationExtractor.Desktop.Services.Security;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Extras;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Desktop.Views.Overview;
using Quipu.ParameterizationExtractor.Desktop.Views.Seed;
using Quipu.ParameterizationExtractor.Desktop.Views.Welcome;

namespace Quipu.ParameterizationExtractor.Desktop;

internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IWorkspaceStore _store;
    private readonly IDialogService _dialog;
    private readonly IRecentFilesStore _recents;
    private readonly IPasswordProtector _protector;
    private readonly ILogger<MainWindowViewModel> _log;

    private string? _currentPath;

    public OverviewViewModel Overview { get; }
    public WelcomeViewModel Welcome { get; }
    public SeedViewModel Seed { get; }
    public GraphViewModel Graph { get; }
    public ExtrasViewModel Extras { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(ConnectionDisplay))]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceLoaded))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditConnectionCommand))]
    private WorkspaceModel? _workspace;

    public bool IsDirty => false;

    public bool IsWorkspaceLoaded => Workspace is not null;

    public string WindowTitle =>
        Workspace is null
            ? "SQL Buldozer"
            : $"SQL Buldozer — {Workspace.Name}{(IsDirty ? " *" : string.Empty)}";

    public string LastSavedDisplay => "never";

    public string ConnectionDisplay =>
        Workspace is null
            ? string.Empty
            : $"{Workspace.Source.Database} @ {Workspace.Source.Server}";

    public MainWindowViewModel(
        IWorkspaceStore store,
        OverviewViewModel overview,
        WelcomeViewModel welcome,
        SeedViewModel seed,
        GraphViewModel graph,
        ExtrasViewModel extras,
        IDialogService dialog,
        IRecentFilesStore recents,
        IPasswordProtector protector,
        ILogger<MainWindowViewModel> log)
    {
        _store = store;
        _dialog = dialog;
        _recents = recents;
        _protector = protector;
        _log = log;
        Overview = overview;
        Welcome = welcome;
        Seed = seed;
        Graph = graph;
        Extras = extras;

        Welcome.Bind(OpenWorkspaceAsync);
        Seed.Bind(SaveWorkspaceAsync);
        Graph.Bind(SaveWorkspaceAsync);
        Extras.Bind(SaveWorkspaceAsync);
        Seed.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SeedViewModel.SelectedScript))
            {
                var anchor = Seed.SelectedScript?.Source;
                Graph.SetAnchor(anchor);
                Extras.SetAnchor(anchor);
            }
        };
        Overview.Show(null);
        _ = Welcome.RefreshAsync();
    }

    private async Task SaveWorkspaceAsync()
    {
        if (Workspace is null || _currentPath is null) return;
        try
        {
            await _store.SaveAsync(Workspace, _currentPath).ConfigureAwait(true);
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Workspace save failed");
            await _dialog.ShowMessageAsync("Save failed", ex.Message).ConfigureAwait(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogWarning(ex, "Workspace save denied");
            await _dialog.ShowMessageAsync("Save failed", ex.Message).ConfigureAwait(true);
        }
    }

    public async Task InitializeAsync(string? workspacePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            Workspace = null;
            _currentPath = null;
            return;
        }

        await OpenWorkspaceAsync(workspacePath, ct).ConfigureAwait(true);
    }

    public async Task OpenWorkspaceAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var ws = await _store.LoadAsync(path, ct).ConfigureAwait(true);
            Workspace = ws;
            _currentPath = path;
            await _recents.PushAsync(path, ct).ConfigureAwait(true);
            await Welcome.RefreshAsync().ConfigureAwait(true);
        }
        catch (FileNotFoundException ex)
        {
            _log.LogWarning(ex, "Workspace not found: {Path}", path);
            await _dialog.ShowMessageAsync("Workspace not found", ex.Message).ConfigureAwait(true);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "Workspace JSON invalid: {Path}", path);
            await _dialog.ShowMessageAsync("Workspace is not valid JSON", ex.Message).ConfigureAwait(true);
        }
        catch (InvalidDataException ex)
        {
            _log.LogWarning(ex, "Workspace cannot be opened: {Path}", path);
            await _dialog.ShowMessageAsync("Workspace cannot be opened", ex.Message).ConfigureAwait(true);
        }
    }

    public Task OpenWorkspaceAsync(string path) => OpenWorkspaceAsync(path, CancellationToken.None);

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = await _dialog.OpenFileAsync(
            "Open workspace",
            "SQL Buldozer workspace (*.bws)|*.bws|All files (*.*)|*.*").ConfigureAwait(true);
        if (!string.IsNullOrEmpty(path))
        {
            await OpenWorkspaceAsync(path).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task NewWorkspaceAsync()
    {
        var result = await _dialog.ShowNewWorkspaceDialogAsync().ConfigureAwait(true);
        if (result is not null)
        {
            await OpenWorkspaceAsync(result.Path).ConfigureAwait(true);
        }
    }

    private bool CanEditConnection() => IsWorkspaceLoaded;

    [RelayCommand(CanExecute = nameof(CanEditConnection))]
    private async Task EditConnectionAsync()
    {
        if (Workspace is null || _currentPath is null) return;

        string? plaintext = null;
        if (!string.IsNullOrEmpty(Workspace.Source.PasswordEncrypted))
        {
            try
            {
                plaintext = _protector.Unprotect(Workspace.Source.PasswordEncrypted);
            }
            catch (CryptographicException ex)
            {
                _log.LogWarning(ex, "Stored password could not be decrypted on this machine");
                plaintext = null;
            }
            catch (FormatException ex)
            {
                _log.LogWarning(ex, "Stored password is not valid base64");
                plaintext = null;
            }
        }

        var updated = await _dialog.ShowEditConnectionDialogAsync(Workspace.Source, plaintext).ConfigureAwait(true);
        if (updated is null) return;

        Workspace = Workspace.WithSource(updated);
        await _store.SaveAsync(Workspace, _currentPath).ConfigureAwait(true);
        _log.LogInformation("Workspace connection edit saved to {Path}", _currentPath);
    }

    private bool CanClose() => IsWorkspaceLoaded;

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close()
    {
        Workspace = null;
        _currentPath = null;
    }

    [RelayCommand]
    private static void Exit()
    {
        // Service-level access to Application.Current is allowed; VMs never see it.
        Application.Current?.Shutdown();
    }

    partial void OnWorkspaceChanged(WorkspaceModel? value)
    {
        Overview.Show(value);

        string? plaintextForSeed = null;
        if (value?.Source.PasswordEncrypted is { Length: > 0 } cipher)
        {
            try
            {
                plaintextForSeed = _protector.Unprotect(cipher);
            }
            catch (CryptographicException) { /* picker/preview will surface an auth failure */ }
            catch (FormatException) { /* corrupt base64 — same path */ }
        }
        Seed.Hydrate(value, plaintextForSeed);
        Graph.Hydrate(value, plaintextForSeed);
        Extras.Hydrate(value, plaintextForSeed);

        if (value is null)
        {
            _ = Welcome.RefreshAsync();
        }
    }
}
