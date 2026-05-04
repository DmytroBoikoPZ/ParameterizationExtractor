using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Desktop.Services.Threading;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Helpers;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Quipu.ParameterizationExtractor.Desktop.Views.Seed;

internal sealed partial class ScriptEditorViewModel : ObservableObject
{
    private const int PreviewRowCap = 200;

    private readonly SourceForScript _source;
    private readonly IDatabaseExplorer _explorer;
    private readonly IUiDispatcher _ui;
    private readonly Func<Task> _saveCallback;
    private readonly ILogger<ScriptEditorViewModel> _log;
    private readonly TimeSpan _saveDebounce;

    private string? _connectionString;
    private bool _loading;
    private bool _suppressSaveDuringAutoPopulate;
    private bool _suppressMismatchOnNextRootChange;
    private CancellationTokenSource? _saveCts;

    public ScriptEditorViewModel(
        SourceForScript source,
        IDatabaseExplorer explorer,
        IUiDispatcher ui,
        Func<Task> saveCallback,
        ILogger<ScriptEditorViewModel> log,
        TimeSpan? saveDebounce = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _explorer = explorer ?? throw new ArgumentNullException(nameof(explorer));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _saveDebounce = saveDebounce ?? TimeSpan.FromMilliseconds(500);

        HydrateFromSource();
    }

    [ObservableProperty]
    private string _scriptName = string.Empty;

    [ObservableProperty]
    private string _rootSchema = string.Empty;

    [ObservableProperty]
    private string _rootTable = string.Empty;

    [ObservableProperty]
    private string _seedQuery = string.Empty;

    [ObservableProperty]
    private PreviewResult? _previewResult;

    [ObservableProperty]
    private string _previewStatus = string.Empty;

    [ObservableProperty]
    private string _rootMismatchHint = string.Empty;

    public string RootDisplay =>
        string.IsNullOrEmpty(RootSchema) ? RootTable : $"{RootSchema}.{RootTable}";

    public TableRef? RootRef
    {
        get => string.IsNullOrEmpty(RootTable) ? null : new TableRef(RootSchema, RootTable);
        set
        {
            RootSchema = value?.Schema ?? string.Empty;
            RootTable = value?.Name ?? string.Empty;
        }
    }

    /// <summary>
    /// Underlying engine record this VM edits. Exposed so <c>SeedViewModel</c> can identify
    /// scripts by reference when removing them from <c>Workspace.Package.Scripts</c>.
    /// </summary>
    internal SourceForScript Source => _source;

    public void Initialize(string? connectionString) => _connectionString = connectionString;

    public void HydrateFromSource()
    {
        _loading = true;
        try
        {
            ScriptName = _source.ScriptName ?? string.Empty;
            SeedQuery = _source.Query ?? string.Empty;

            var firstRoot = _source.RootRecords?.Count > 0 ? _source.RootRecords[0] : null;
            RootSchema = firstRoot?.Schema ?? string.Empty;
            RootTable = firstRoot?.TableName ?? string.Empty;
        }
        finally
        {
            _loading = false;
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RunPreviewAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            PreviewStatus = "No connection — open a workspace first";
            return;
        }

        PreviewStatus = "Loading…";
        PreviewResult = null;

        try
        {
            var result = await _explorer
                .PreviewQueryAsync(_connectionString, SeedQuery, PreviewRowCap, ct)
                .ConfigureAwait(false);

            await _ui.InvokeAsync(() =>
            {
                PreviewResult = result;
                PreviewStatus = $"{result.Rows.Count} rows" + (result.Truncated ? " (truncated)" : string.Empty);
            });
        }
        catch (OperationCanceledException)
        {
            await _ui.InvokeAsync(() => PreviewStatus = "Cancelled");
        }
        catch (DatabaseExplorerException ex)
        {
            await _ui.InvokeAsync(() => PreviewStatus = $"Failed: {ex.Message}");
        }

        _log.LogInformation("Preview run for {Script} ended {Status}", ScriptName, PreviewStatus);
    }

    [RelayCommand]
    private void DismissRootMismatchHint() => RootMismatchHint = string.Empty;

    partial void OnScriptNameChanged(string value) => OnAnyEditableChanged();

    partial void OnRootSchemaChanged(string value)
    {
        if (!_suppressMismatchOnNextRootChange)
            RootMismatchHint = string.Empty;
        OnAnyEditableChanged();
    }

    partial void OnRootTableChanged(string value)
    {
        if (!_suppressMismatchOnNextRootChange)
            RootMismatchHint = string.Empty;
        OnAnyEditableChanged();
    }

    partial void OnSeedQueryChanged(string value)
    {
        InferRootFromSeedQuery(value);
        OnAnyEditableChanged();
    }

    private void InferRootFromSeedQuery(string sql)
    {
        var parsed = SeedQueryParser.TryExtract(sql);
        if (parsed is null)
        {
            if (string.IsNullOrEmpty(RootSchema) && string.IsNullOrEmpty(RootTable))
                RootMismatchHint = string.Empty;
            return;
        }

        if (string.IsNullOrEmpty(RootTable))
        {
            _suppressSaveDuringAutoPopulate = true;
            _suppressMismatchOnNextRootChange = true;
            try
            {
                RootSchema = parsed.Schema;
                RootTable = parsed.Name;
            }
            finally
            {
                _suppressSaveDuringAutoPopulate = false;
                _suppressMismatchOnNextRootChange = false;
            }
            RootMismatchHint = string.Empty;
            return;
        }

        var matches =
            string.Equals(RootSchema ?? string.Empty, parsed.Schema, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(RootTable ?? string.Empty, parsed.Name, StringComparison.OrdinalIgnoreCase);

        RootMismatchHint = matches
            ? string.Empty
            : $"SQL targets {Format(parsed.Schema, parsed.Name)}; picked root is {Format(RootSchema ?? string.Empty, RootTable ?? string.Empty)}";
    }

    private static string Format(string schema, string name) =>
        string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";

    private void OnAnyEditableChanged()
    {
        if (_loading) return;
        if (_suppressSaveDuringAutoPopulate) return;

        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        _ = SaveDebouncedAsync(_saveCts.Token);
    }

    private async Task SaveDebouncedAsync(CancellationToken ct)
    {
        try
        {
            if (_saveDebounce > TimeSpan.Zero)
            {
                await Task.Delay(_saveDebounce, ct).ConfigureAwait(true);
            }

            if (ct.IsCancellationRequested) return;

            WriteThroughToSource();
            await _saveCallback().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer keystroke supersedes this debounce.
        }
    }

    private void WriteThroughToSource()
    {
        _source.ScriptName = ScriptName;
        _source.Query = SeedQuery;

        _source.RootRecords ??= new System.Collections.Generic.List<RecordsToExtract>();
        if (_source.RootRecords.Count == 0)
        {
            _source.RootRecords.Add(new RecordsToExtract());
        }

        var root = _source.RootRecords[0];
        root.Schema = RootSchema;
        root.TableName = RootTable;
    }
}
