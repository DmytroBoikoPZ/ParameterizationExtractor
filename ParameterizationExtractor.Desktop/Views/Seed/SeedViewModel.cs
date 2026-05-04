using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;
using Quipu.ParameterizationExtractor.Desktop.Services.Threading;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Quipu.ParameterizationExtractor.Desktop.Views.Seed;

internal sealed partial class SeedViewModel : ObservableObject
{
    private const string DefaultScriptName = "NewScript";

    private readonly IDatabaseExplorer _explorer;
    private readonly IUiDispatcher _ui;
    private readonly IDialogService _dialog;
    private readonly ILoggerFactory _loggerFactory;

    private WorkspaceModel? _workspace;
    private string? _connectionString;
    private Func<Task>? _saveCallback;

    public SeedViewModel(
        IDatabaseExplorer explorer,
        IUiDispatcher ui,
        IDialogService dialog,
        ILoggerFactory loggerFactory)
    {
        _explorer = explorer ?? throw new ArgumentNullException(nameof(explorer));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        Scripts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasScripts));
    }

    public ObservableCollection<ScriptEditorViewModel> Scripts { get; } = new();

    /// <summary>Tables available in the workspace's database — shared across all script editors so the picker is one source of truth per workspace.</summary>
    public ObservableCollection<TableRef> Tables { get; } = new();

    [ObservableProperty]
    private ScriptEditorViewModel? _selectedScript;

    public bool HasScripts => Scripts.Count > 0;

    internal void Bind(Func<Task> saveCallback)
    {
        _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
    }

    public void Hydrate(WorkspaceModel? workspace, string? plaintextPassword)
    {
        Scripts.Clear();
        Tables.Clear();
        SelectedScript = null;

        if (workspace is null)
        {
            _workspace = null;
            _connectionString = null;
            NotifyCommandsCanExecute();
            return;
        }

        _workspace = workspace;
        _connectionString = WorkspaceConnectionStringBuilder.Build(workspace.Source, plaintextPassword);

        workspace.Package ??= new Package();
        workspace.Package.Scripts ??= new List<SourceForScript>();

        foreach (var s in workspace.Package.Scripts)
        {
            Scripts.Add(BuildChild(s));
        }

        SelectedScript = Scripts.FirstOrDefault();
        NotifyCommandsCanExecute();

        _ = RefreshTablesAsync();
    }

    private async Task RefreshTablesAsync()
    {
        if (string.IsNullOrEmpty(_connectionString)) return;
        try
        {
            var refs = await _explorer.ListTablesAsync(_connectionString).ConfigureAwait(true);
            Tables.Clear();
            foreach (var t in refs) Tables.Add(t);
        }
        catch (DatabaseExplorerException)
        {
            // Picker will show empty list; the operator's signal is the per-script preview status.
        }
    }

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private async Task AddScriptAsync()
    {
        if (_workspace is null) return;

        var s = new SourceForScript { ScriptName = NextScriptName() };
        _workspace.Package.Scripts.Add(s);

        var child = BuildChild(s);
        Scripts.Add(child);
        SelectedScript = child;

        await SaveAsync().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private async Task RemoveSelectedScriptAsync()
    {
        var target = SelectedScript;
        if (target is null || _workspace is null) return;

        _workspace.Package.Scripts.Remove(target.Source);
        Scripts.Remove(target);
        SelectedScript = Scripts.FirstOrDefault();

        await SaveAsync().ConfigureAwait(true);
    }

    private bool CanMutate() => _workspace is not null;

    private bool CanRemoveSelected() => _workspace is not null && SelectedScript is not null;

    partial void OnSelectedScriptChanged(ScriptEditorViewModel? value)
    {
        RemoveSelectedScriptCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCommandsCanExecute()
    {
        AddScriptCommand.NotifyCanExecuteChanged();
        RemoveSelectedScriptCommand.NotifyCanExecuteChanged();
    }

    private ScriptEditorViewModel BuildChild(SourceForScript s)
    {
        var child = new ScriptEditorViewModel(
            s,
            _explorer,
            _ui,
            SaveAsync,
            _loggerFactory.CreateLogger<ScriptEditorViewModel>());
        child.Initialize(_connectionString);
        return child;
    }

    private string NextScriptName()
    {
        var existing = new HashSet<string>(Scripts.Select(c => c.ScriptName), StringComparer.Ordinal);
        if (!existing.Contains(DefaultScriptName)) return DefaultScriptName;
        for (var i = 2; ; i++)
        {
            var candidate = $"{DefaultScriptName} ({i})";
            if (!existing.Contains(candidate)) return candidate;
        }
    }

    private Task SaveAsync()
    {
        return _saveCallback is null ? Task.CompletedTask : _saveCallback();
    }
}
