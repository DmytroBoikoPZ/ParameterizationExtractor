namespace Quipu.ParameterizationExtractor.Desktop.Views.Graph;

internal sealed class GraphEdgeViewModel
{
    public GraphEdgeViewModel(string fromNodeId, string toNodeId, EdgeStyle style, string constraintName)
    {
        FromNodeId = fromNodeId;
        ToNodeId = toNodeId;
        Style = style;
        ConstraintName = constraintName;
    }

    public string FromNodeId { get; }
    public string ToNodeId { get; }
    public EdgeStyle Style { get; }
    public string ConstraintName { get; }
}
