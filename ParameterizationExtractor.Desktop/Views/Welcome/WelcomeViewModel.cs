using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;
using Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles;

namespace Quipu.ParameterizationExtractor.Desktop.Views.Welcome;

internal sealed partial class WelcomeViewModel : ObservableObject
{
    private const string OpenFilter = "SQL Buldozer workspace (*.bws)|*.bws|All files (*.*)|*.*";
    private const string OpenTitle = "Open workspace";

    private readonly IDialogService _dialog;
    private readonly IRecentFilesStore _recents;
    private Func<string, Task>? _openHandler;

    [ObservableProperty]
    private bool _isLoadingRecents;

    public ObservableCollection<RecentFile> Recents { get; } = new();

    public bool HasRecents => Recents.Count > 0;

    public bool IsNewWorkspaceEnabled => true;

    public string NewWorkspaceTooltip => "Create a new workspace and connect to a database";

    public WelcomeViewModel(IDialogService dialog, IRecentFilesStore recents)
    {
        _dialog = dialog;
        _recents = recents;
        Recents.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecents));
    }

    internal void Bind(Func<string, Task> openHandler) => _openHandler = openHandler;

    public async Task RefreshAsync()
    {
        IsLoadingRecents = true;
        try
        {
            var items = await _recents.GetAsync().ConfigureAwait(true);
            Recents.Clear();
            foreach (var item in items)
            {
                Recents.Add(item);
            }
        }
        finally
        {
            IsLoadingRecents = false;
        }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        EnsureBound();
        var path = await _dialog.OpenFileAsync(OpenTitle, OpenFilter).ConfigureAwait(true);
        if (!string.IsNullOrEmpty(path))
        {
            await _openHandler!(path).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task NewWorkspaceAsync()
    {
        EnsureBound();
        var result = await _dialog.ShowNewWorkspaceDialogAsync().ConfigureAwait(true);
        if (result is not null)
        {
            await _openHandler!(result.Path).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string? path)
    {
        EnsureBound();
        if (string.IsNullOrEmpty(path)) return;
        await _openHandler!(path).ConfigureAwait(true);
    }

    private void EnsureBound()
    {
        if (_openHandler is null)
        {
            throw new InvalidOperationException(
                "WelcomeViewModel.Bind must be called before commands execute (MainWindowViewModel ctor wires the open handler).");
        }
    }
}
