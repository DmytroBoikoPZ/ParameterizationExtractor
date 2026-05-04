using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Desktop.Controls.ConnectionEditor;
using Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;

internal enum NewWorkspaceDialogMode { New, Edit }

internal sealed partial class NewWorkspaceDialogViewModel : ObservableObject
{
    private const string WorkspaceFilter = "SQL Buldozer workspace (*.bws)|*.bws";

    private readonly IWorkspaceStore _store;
    private readonly IDialogService _dialog;
    private readonly ILogger<NewWorkspaceDialogViewModel> _log;

    public ConnectionEditorViewModel ConnectionEditor { get; }

    [ObservableProperty] private string _workspaceName = string.Empty;
    [ObservableProperty] private string _savePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewMode))]
    [NotifyPropertyChangedFor(nameof(IsEditMode))]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(ConfirmButtonText))]
    private NewWorkspaceDialogMode _mode = NewWorkspaceDialogMode.New;

    public bool IsNewMode => Mode == NewWorkspaceDialogMode.New;
    public bool IsEditMode => Mode == NewWorkspaceDialogMode.Edit;
    public string Title => IsEditMode ? "Edit connection" : "New workspace";
    public string ConfirmButtonText => IsEditMode ? "Save" : "Create workspace";

    public NewWorkspaceResult? CreatedResult { get; private set; }
    public WorkspaceSource? EditedSource { get; private set; }

    /// <summary>
    /// Set by the View when ShowDialog() is invoked. The dialog wires this to
    /// <see cref="System.Windows.Window.DialogResult"/> via the close action.
    /// </summary>
    public Action<bool>? CloseDialogWithResult { get; set; }

    public NewWorkspaceDialogViewModel(
        ConnectionEditorViewModel editor,
        IWorkspaceStore store,
        IDialogService dialog,
        ILogger<NewWorkspaceDialogViewModel> log)
    {
        ConnectionEditor = editor;
        _store = store;
        _dialog = dialog;
        _log = log;
    }

    public void InitForNew()
    {
        Mode = NewWorkspaceDialogMode.New;
        WorkspaceName = string.Empty;
        SavePath = string.Empty;
        CreatedResult = null;
        EditedSource = null;
    }

    public void InitForEdit(WorkspaceSource current, string? plaintextPassword)
    {
        Mode = NewWorkspaceDialogMode.Edit;
        WorkspaceName = string.Empty;
        SavePath = string.Empty;
        CreatedResult = null;
        EditedSource = null;
        ConnectionEditor.Populate(current, plaintextPassword);
    }

    [RelayCommand]
    private async Task BrowseSavePathAsync()
    {
        var defaultName = string.IsNullOrWhiteSpace(WorkspaceName) ? "workspace.bws" : $"{WorkspaceName}.bws";
        var picked = await _dialog.SaveFileAsync("Save workspace as", WorkspaceFilter, defaultName).ConfigureAwait(true);
        if (!string.IsNullOrEmpty(picked)) SavePath = picked;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (ConnectionEditor.TestStatus != ConnectionTestStatus.Success)
        {
            await _dialog.ShowMessageAsync(
                IsEditMode ? "Test connection first" : "Test connection first",
                "Run a successful Test connection before saving.").ConfigureAwait(true);
            return;
        }

        if (IsNewMode)
        {
            if (string.IsNullOrWhiteSpace(WorkspaceName) || string.IsNullOrWhiteSpace(SavePath))
            {
                await _dialog.ShowMessageAsync(
                    "Missing details",
                    "Provide a workspace name and save path.").ConfigureAwait(true);
                return;
            }

            var model = new WorkspaceModel
            {
                Name = WorkspaceName.Trim(),
                Source = ConnectionEditor.ToWorkspaceSource(),
                Global = new GlobalExtractConfiguration(),
                Package = new Package(),
            };
            await _store.SaveAsync(model, SavePath).ConfigureAwait(true);
            _log.LogInformation("Created workspace {Name} at {Path}", model.Name, SavePath);
            CreatedResult = new NewWorkspaceResult(SavePath);
        }
        else
        {
            EditedSource = ConnectionEditor.ToWorkspaceSource();
        }

        CloseDialogWithResult?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CreatedResult = null;
        EditedSource = null;
        CloseDialogWithResult?.Invoke(false);
    }
}
