using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Quipu.ParameterizationExtractor.Desktop.Dialogs.AddStandaloneTable;

internal sealed partial class AddStandaloneTableDialogViewModel : ObservableObject
{
    public ObservableCollection<TableRef> Tables { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private TableRef? _selectedTable;

    public TableRef? PickedTable { get; private set; }

    public Action<bool>? CloseDialogWithResult { get; set; }

    public void Initialize(IReadOnlyList<TableRef> available)
    {
        Tables.Clear();
        foreach (var t in available) Tables.Add(t);
        SelectedTable = null;
        PickedTable = null;
    }

    private bool CanConfirm() => SelectedTable is not null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        PickedTable = SelectedTable;
        CloseDialogWithResult?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        PickedTable = null;
        CloseDialogWithResult?.Invoke(false);
    }
}
