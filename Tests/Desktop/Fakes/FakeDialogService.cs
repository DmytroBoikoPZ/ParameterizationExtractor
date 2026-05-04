#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;
using Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Tests.Desktop.Fakes;

/// <summary>
/// Queue-driven test double for <see cref="IDialogService"/> per the recipe spec
/// (<c>docs/methodology/wpf-desktop.md § Service abstractions — Test-fake convention</c>).
/// Tests enqueue responses; the fake records every call so assertions can verify
/// the user was prompted with the expected title / message / filter.
/// </summary>
internal sealed class FakeDialogService : IDialogService
{
    public sealed record DialogCall(string Method, string Title, string Message, string? Filter);

    public sealed record EditConnectionCall(WorkspaceSource Current, string? PlaintextPassword);

    private readonly Queue<bool> _confirms = new();
    private readonly Queue<string?> _openResults = new();
    private readonly Queue<string?> _saveResults = new();
    private readonly Queue<NewWorkspaceResult?> _newWorkspaceResults = new();
    private readonly Queue<WorkspaceSource?> _editConnectionResults = new();
    private readonly Queue<TableRef?> _addStandaloneTableResults = new();

    public List<DialogCall> Calls { get; } = new();
    public List<EditConnectionCall> EditConnectionCalls { get; } = new();

    public void EnqueueConfirmResponse(bool result) => _confirms.Enqueue(result);

    public void EnqueueOpenFileResponse(string? path) => _openResults.Enqueue(path);

    public void EnqueueSaveFileResponse(string? path) => _saveResults.Enqueue(path);

    public void EnqueueNewWorkspaceResponse(NewWorkspaceResult? result) => _newWorkspaceResults.Enqueue(result);

    public void EnqueueEditConnectionResponse(WorkspaceSource? result) => _editConnectionResults.Enqueue(result);

    public void EnqueueAddStandaloneTableResponse(TableRef? result) => _addStandaloneTableResults.Enqueue(result);

    public List<IReadOnlyList<TableRef>> AddStandaloneTableCalls { get; } = new();

    public Task ShowMessageAsync(string title, string message)
    {
        Calls.Add(new DialogCall("ShowMessage", title, message, null));
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        Calls.Add(new DialogCall("Confirm", title, message, null));
        if (_confirms.Count == 0)
        {
            throw new InvalidOperationException("FakeDialogService: no enqueued confirm response");
        }
        return Task.FromResult(_confirms.Dequeue());
    }

    public Task<string?> OpenFileAsync(string title, string filter)
    {
        Calls.Add(new DialogCall("OpenFile", title, string.Empty, filter));
        if (_openResults.Count == 0)
        {
            throw new InvalidOperationException("FakeDialogService: no enqueued open-file response");
        }
        return Task.FromResult(_openResults.Dequeue());
    }

    public Task<string?> SaveFileAsync(string title, string filter, string? defaultName = null)
    {
        Calls.Add(new DialogCall("SaveFile", title, defaultName ?? string.Empty, filter));
        if (_saveResults.Count == 0)
        {
            throw new InvalidOperationException("FakeDialogService: no enqueued save-file response");
        }
        return Task.FromResult(_saveResults.Dequeue());
    }

    public Task<NewWorkspaceResult?> ShowNewWorkspaceDialogAsync()
    {
        Calls.Add(new DialogCall("ShowNewWorkspace", string.Empty, string.Empty, null));
        if (_newWorkspaceResults.Count == 0)
        {
            throw new InvalidOperationException("FakeDialogService: no enqueued new-workspace response");
        }
        return Task.FromResult(_newWorkspaceResults.Dequeue());
    }

    public Task<WorkspaceSource?> ShowEditConnectionDialogAsync(WorkspaceSource current, string? plaintextPassword)
    {
        Calls.Add(new DialogCall("ShowEditConnection", string.Empty, string.Empty, null));
        EditConnectionCalls.Add(new EditConnectionCall(current, plaintextPassword));
        if (_editConnectionResults.Count == 0)
        {
            throw new InvalidOperationException("FakeDialogService: no enqueued edit-connection response");
        }
        return Task.FromResult(_editConnectionResults.Dequeue());
    }

    public Task<TableRef?> ShowAddStandaloneTableDialogAsync(IReadOnlyList<TableRef> available)
    {
        Calls.Add(new DialogCall("ShowAddStandaloneTable", string.Empty, string.Empty, null));
        AddStandaloneTableCalls.Add(available);
        if (_addStandaloneTableResults.Count == 0)
        {
            throw new InvalidOperationException("FakeDialogService: no enqueued add-standalone-table response");
        }
        return Task.FromResult(_addStandaloneTableResults.Dequeue());
    }
}
