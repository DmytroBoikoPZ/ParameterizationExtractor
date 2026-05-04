#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;

namespace Tests.Desktop.Fakes;

/// <summary>
/// In-memory <see cref="IWorkspaceStore"/> that records every <see cref="SaveAsync"/> call.
/// Loads return whatever was last saved against the same path (so `OpenWorkspaceAsync` round-trips).
/// </summary>
internal sealed class FakeWorkspaceStore : IWorkspaceStore
{
    public sealed record SaveCall(WorkspaceModel Workspace, string Path);

    private readonly Dictionary<string, WorkspaceModel> _byPath = new();

    public List<SaveCall> SaveCalls { get; } = new();

    public void Seed(string path, WorkspaceModel workspace) => _byPath[path] = workspace;

    public Task<WorkspaceModel> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!_byPath.TryGetValue(path, out var ws))
        {
            throw new System.IO.FileNotFoundException($"FakeWorkspaceStore: no workspace at {path}", path);
        }
        return Task.FromResult(ws);
    }

    public Task SaveAsync(WorkspaceModel workspace, string path, CancellationToken ct = default)
    {
        SaveCalls.Add(new SaveCall(workspace, path));
        _byPath[path] = workspace;
        return Task.CompletedTask;
    }
}
