using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Quipu.ParameterizationExtractor.Logic.Configs.Json;

namespace Quipu.ParameterizationExtractor.Desktop.Services.Workspace;

/// <summary>
/// JSON-backed <see cref="IWorkspaceStore"/>. Reuses the engine's shared
/// <see cref="JsonOptions.Default"/> so the embedded <c>global</c> and <c>package</c>
/// subtrees deserialise identically to a standalone JSON package fed to the engine.
/// </summary>
internal sealed class JsonWorkspaceStore : IWorkspaceStore
{
    private const int SupportedVersion = 1;

    public async Task<WorkspaceModel> LoadAsync(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        var workspace = await JsonSerializer.DeserializeAsync<WorkspaceModel>(stream, JsonOptions.Default, ct).ConfigureAwait(false)
            ?? throw new InvalidDataException($"Workspace at '{path}' deserialised to null.");

        if (workspace.Version != SupportedVersion)
            throw new InvalidDataException(
                $"Workspace '{path}' has $version={workspace.Version}; only v{SupportedVersion} is supported.");

        if (workspace.Source.Auth is not ("windows" or "sql"))
            throw new InvalidDataException(
                $"Workspace '{path}' has invalid source.auth '{workspace.Source.Auth}'; expected 'windows' or 'sql'.");

        return workspace;
    }

    public async Task SaveAsync(WorkspaceModel workspace, string path, CancellationToken ct = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, workspace, JsonOptions.Default, ct).ConfigureAwait(false);
    }
}
