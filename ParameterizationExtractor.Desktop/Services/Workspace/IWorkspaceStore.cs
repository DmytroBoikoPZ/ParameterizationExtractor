using System.Threading;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Desktop.Services.Workspace;

/// <summary>
/// Reads and writes <c>.bws</c> workspace files. Implements the seam named in the archived
/// <c>desktop-skeleton/feature-architecture.md § 5</c>; concretely landed by the
/// <c>desktop-workspace-format</c> feature against the schema in
/// <c>docs/methodology/workspace-format.md</c> (ADR-009).
/// </summary>
internal interface IWorkspaceStore
{
    Task<WorkspaceModel> LoadAsync(string path, CancellationToken ct = default);

    Task SaveAsync(WorkspaceModel workspace, string path, CancellationToken ct = default);
}
