namespace Quipu.ParameterizationExtractor.Desktop.Views.Graph;

internal enum EdgeStyle
{
    /// <summary>Both endpoints Configured — solid line.</summary>
    Follow,

    /// <summary>Either endpoint Excluded — dashed grey.</summary>
    Stop,

    /// <summary>Either endpoint Pending (and neither Excluded) — thin line.</summary>
    Pending,
}
