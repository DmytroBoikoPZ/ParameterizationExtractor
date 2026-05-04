namespace Quipu.ParameterizationExtractor.Desktop.Views.Graph;

internal enum NodeState
{
    /// <summary>Discovered via FK walk but absent from <c>TablesToProcess</c> (◯).</summary>
    Pending,

    /// <summary>Present in <c>TablesToProcess</c> with <c>Excluded == false</c> (✓).</summary>
    Configured,

    /// <summary>Present in <c>TablesToProcess</c> with <c>Excluded == true</c> (✗).</summary>
    Excluded,
}
