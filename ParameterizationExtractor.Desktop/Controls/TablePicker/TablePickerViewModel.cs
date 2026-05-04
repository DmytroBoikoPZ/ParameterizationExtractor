using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Quipu.ParameterizationExtractor.Desktop.Controls.TablePicker;

internal sealed partial class TablePickerViewModel : ObservableObject
{
    private readonly IDatabaseExplorer _explorer;
    private readonly ILogger<TablePickerViewModel> _log;

    public TablePickerViewModel(IDatabaseExplorer explorer, ILogger<TablePickerViewModel> log)
    {
        _explorer = explorer ?? throw new ArgumentNullException(nameof(explorer));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public ObservableCollection<TableRef> Tables { get; } = new();

    [ObservableProperty]
    private TableRef? _selectedTable;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Connection string the host VM hands in. Set before invoking <see cref="RefreshCommand"/>.</summary>
    public string? ConnectionString { get; set; }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            ErrorMessage = "No connection";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var refs = await _explorer.ListTablesAsync(ConnectionString, ct).ConfigureAwait(true);
            Tables.Clear();
            foreach (var t in refs)
            {
                Tables.Add(t);
            }
        }
        catch (DatabaseExplorerException ex)
        {
            _log.LogWarning(ex, "TablePicker refresh failed");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
