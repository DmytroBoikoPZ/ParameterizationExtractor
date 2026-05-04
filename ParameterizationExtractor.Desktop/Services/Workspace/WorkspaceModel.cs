using System.Text.Json.Serialization;
using ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Quipu.ParameterizationExtractor.Desktop.Services.Workspace;

/// <summary>
/// In-memory representation of a `.bws` workspace file. Wraps the engine-input subtrees
/// (<see cref="GlobalExtractConfiguration"/>, <see cref="Package"/>) with desktop-only
/// metadata: workspace name and source-connection metadata.
/// Schema is defined in <c>docs/methodology/workspace-format.md</c> and locked by ADR-009.
/// </summary>
internal sealed class WorkspaceModel
{
    [JsonPropertyName("$version")]
    public int Version { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public WorkspaceSource Source { get; set; } = new();

    public GlobalExtractConfiguration Global { get; set; } = new();

    public Package Package { get; set; } = new();

    /// <summary>
    /// Returns a fresh <see cref="WorkspaceModel"/> with the given <paramref name="source"/>.
    /// Callers replace <c>MainWindowViewModel.Workspace</c> with the result so that the
    /// existing <c>[NotifyPropertyChangedFor]</c> chain on <c>Workspace</c> fires
    /// (in-place mutation of <c>Source</c>'s fields would not).
    /// </summary>
    public WorkspaceModel WithSource(WorkspaceSource source) =>
        new()
        {
            Version = this.Version,
            Name = this.Name,
            Source = source,
            Global = this.Global,
            Package = this.Package,
        };
}
