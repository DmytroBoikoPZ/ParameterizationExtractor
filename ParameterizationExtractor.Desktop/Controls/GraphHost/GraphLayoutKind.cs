namespace Quipu.ParameterizationExtractor.Desktop.Controls.GraphHost;

internal enum GraphLayoutKind
{
    /// <summary>Sugiyama / layered top-down. Default — best for FK hierarchies.</summary>
    Hierarchical,

    /// <summary>Variant of layered with explicit row constraints. Reserved.</summary>
    Layered,

    /// <summary>Physics-based MDS / force-directed. Useful for dense graphs.</summary>
    ForceDirected,
}
