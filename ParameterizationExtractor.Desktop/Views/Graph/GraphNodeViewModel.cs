namespace Quipu.ParameterizationExtractor.Desktop.Views.Graph;

internal sealed class GraphNodeViewModel
{
    public GraphNodeViewModel(
        string schema, string name,
        bool isSeed,
        NodeState state,
        string strategyChip)
    {
        Schema = schema;
        Name = name;
        IsSeed = isSeed;
        State = state;
        StrategyChip = strategyChip;
    }

    public string Schema { get; }
    public string Name { get; }
    public bool IsSeed { get; }
    public NodeState State { get; }
    public string StrategyChip { get; }
    public int UnexpandedNeighbourCount { get; internal set; }

    public string NodeId => $"{Schema}.{Name}";
    public string Display => NodeId;
}
