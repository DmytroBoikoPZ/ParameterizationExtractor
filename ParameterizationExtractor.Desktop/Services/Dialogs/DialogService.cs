using System;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.Generic;
using Quipu.ParameterizationExtractor.Desktop.Dialogs.AddStandaloneTable;
using Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;

internal sealed class DialogService : IDialogService
{
    private readonly IDialogCoordinator _coordinator;
    private readonly Func<NewWorkspaceDialog> _newWorkspaceDialogFactory;
    private readonly Func<AddStandaloneTableDialog> _addStandaloneTableDialogFactory;
    private readonly ILogger<DialogService> _log;

    public DialogService(
        IDialogCoordinator coordinator,
        Func<NewWorkspaceDialog> newWorkspaceDialogFactory,
        Func<AddStandaloneTableDialog> addStandaloneTableDialogFactory,
        ILogger<DialogService> log)
    {
        _coordinator = coordinator;
        _newWorkspaceDialogFactory = newWorkspaceDialogFactory;
        _addStandaloneTableDialogFactory = addStandaloneTableDialogFactory;
        _log = log;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        // Service-level access to Application.Current is allowed; VMs never see it.
        var owner = Application.Current?.MainWindow as MetroWindow;
        if (owner is null)
        {
            _log.LogWarning("ShowMessageAsync called before MainWindow is available; title={Title}", title);
            return;
        }

        await _coordinator.ShowMessageAsync(owner, title, message);
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var owner = Application.Current?.MainWindow as MetroWindow;
        if (owner is null)
        {
            _log.LogWarning("ConfirmAsync called before MainWindow is available; title={Title}", title);
            return false;
        }

        var result = await _coordinator.ShowMessageAsync(
            owner,
            title,
            message,
            MessageDialogStyle.AffirmativeAndNegative);

        return result == MessageDialogResult.Affirmative;
    }

    public Task<string?> OpenFileAsync(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = false,
            CheckFileExists = true,
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public Task<string?> SaveFileAsync(string title, string filter, string? defaultName = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            OverwritePrompt = true,
        };

        if (!string.IsNullOrEmpty(defaultName))
        {
            dialog.FileName = defaultName;
        }

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public Task<NewWorkspaceResult?> ShowNewWorkspaceDialogAsync()
    {
        var dialog = _newWorkspaceDialogFactory();
        var vm = (NewWorkspaceDialogViewModel)dialog.DataContext;
        vm.InitForNew();
        dialog.Owner = Application.Current?.MainWindow;
        var ok = dialog.ShowDialog() == true;
        return Task.FromResult(ok ? vm.CreatedResult : null);
    }

    public Task<WorkspaceSource?> ShowEditConnectionDialogAsync(WorkspaceSource current, string? plaintextPassword)
    {
        var dialog = _newWorkspaceDialogFactory();
        var vm = (NewWorkspaceDialogViewModel)dialog.DataContext;
        vm.InitForEdit(current, plaintextPassword);
        dialog.Owner = Application.Current?.MainWindow;
        var ok = dialog.ShowDialog() == true;
        return Task.FromResult(ok ? vm.EditedSource : null);
    }

    public Task<TableRef?> ShowAddStandaloneTableDialogAsync(IReadOnlyList<TableRef> available)
    {
        var dialog = _addStandaloneTableDialogFactory();
        var vm = (AddStandaloneTableDialogViewModel)dialog.DataContext;
        vm.Initialize(available);
        dialog.Owner = Application.Current?.MainWindow;
        var ok = dialog.ShowDialog() == true;
        return Task.FromResult(ok ? vm.PickedTable : null);
    }
}
