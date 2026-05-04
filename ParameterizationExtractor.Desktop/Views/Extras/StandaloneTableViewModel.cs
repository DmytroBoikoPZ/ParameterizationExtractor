using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Desktop.Controls.StrategyPicker;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Quipu.ParameterizationExtractor.Desktop.Views.Extras;

/// <summary>
/// Per-row VM in the Extras tab's standalone-tables list. Wraps a single
/// <see cref="TableToExtract"/> reference; observable Schema / Name / StrategyChoice / Where /
/// Excluded / IsExpanded; save-on-change debounced (mirrors <c>NodeInspectorViewModel</c>).
/// </summary>
internal sealed partial class StandaloneTableViewModel : ObservableObject
{
    private readonly Func<Task> _saveCallback;
    private readonly Func<StandaloneTableViewModel, Task> _removeCallback;
    private readonly ILogger<StandaloneTableViewModel> _log;
    private readonly TimeSpan _saveDebounce;

    private bool _loading;
    private CancellationTokenSource? _saveCts;

    public StandaloneTableViewModel(
        TableToExtract source,
        Func<Task> saveCallback,
        Func<StandaloneTableViewModel, Task> removeCallback,
        ILogger<StandaloneTableViewModel> log,
        TimeSpan? saveDebounce = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
        _removeCallback = removeCallback ?? throw new ArgumentNullException(nameof(removeCallback));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _saveDebounce = saveDebounce ?? TimeSpan.FromMilliseconds(500);

        Schema = source.Schema ?? string.Empty;
        Name = source.TableName ?? string.Empty;

        HydrateFromSource();
    }

    public TableToExtract Source { get; }

    public string Schema { get; }
    public string Name { get; }
    public string Display => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrategyChip))]
    private StrategyKind _strategyChoice = StrategyKind.OnlyOneTable;

    [ObservableProperty]
    private string _where = string.Empty;

    [ObservableProperty]
    private bool _excluded;

    [ObservableProperty]
    private bool _isExpanded;

    public string StrategyChip => StrategyChoice.ToString();

    private void HydrateFromSource()
    {
        _loading = true;
        try
        {
            StrategyChoice = ToStrategyKind(Source.ExtractStrategy);
            Where = Source.ExtractStrategy?.Where ?? string.Empty;
            Excluded = Source.Excluded;
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnStrategyChoiceChanged(StrategyKind value) => OnAnyEditableChanged(replaceStrategy: true);
    partial void OnWhereChanged(string value) => OnAnyEditableChanged();
    partial void OnExcludedChanged(bool value) => OnAnyEditableChanged();

    private void OnAnyEditableChanged(bool replaceStrategy = false)
    {
        if (_loading) return;

        if (replaceStrategy)
        {
            Source.ExtractStrategy = StrategyChoice switch
            {
                StrategyKind.FKDependency => new FKDependencyExtractStrategy { Where = Where },
                StrategyKind.OnlyChildren => new OnlyChildrenExtractStrategy { Where = Where },
                StrategyKind.OnlyParent => new OnlyParentExtractStrategy { Where = Where },
                StrategyKind.OnlyOneTable => new OnlyOneTableExtractStrategy { Where = Where },
                _ => Source.ExtractStrategy,
            };
        }
        else if (Source.ExtractStrategy is { } s)
        {
            s.Where = Where;
        }

        Source.Excluded = Excluded;

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
            await _saveCallback().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer edit supersedes this debounce.
        }
    }

    [RelayCommand]
    private async Task RemoveAsync() => await _removeCallback(this).ConfigureAwait(true);

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;

    private static StrategyKind ToStrategyKind(ExtractStrategy? s) => s switch
    {
        FKDependencyExtractStrategy _ => StrategyKind.FKDependency,
        OnlyChildrenExtractStrategy _ => StrategyKind.OnlyChildren,
        OnlyParentExtractStrategy _ => StrategyKind.OnlyParent,
        OnlyOneTableExtractStrategy _ => StrategyKind.OnlyOneTable,
        _ => StrategyKind.OnlyOneTable,
    };
}
