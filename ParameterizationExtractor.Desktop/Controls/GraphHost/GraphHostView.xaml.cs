using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.WpfGraphControl;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Logic.Schema;
using AglEdge = Microsoft.Msagl.Drawing.Edge;
using AglGraph = Microsoft.Msagl.Drawing.Graph;
using AglNode = Microsoft.Msagl.Drawing.Node;

namespace Quipu.ParameterizationExtractor.Desktop.Controls.GraphHost;

/// <summary>
/// AGL (AutomaticGraphLayout) wrapper. AGL types live ONLY inside this control's code-behind;
/// ViewModels see only the engine's <see cref="ReachableGraph"/> record, the
/// <see cref="GraphLayoutKind"/> enum, and <see cref="SelectedNodeId"/> (string).
/// Mirrors the AvalonEdit isolation pattern in <c>SqlEditor</c>.
/// </summary>
internal partial class GraphHostView : UserControl
{
    private GraphViewer? _viewer;
    private bool _isUpdatingSelection;
    private bool _renderQueued;

    public GraphHostView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public static readonly DependencyProperty GraphProperty = DependencyProperty.Register(
        nameof(Graph),
        typeof(ReachableGraph),
        typeof(GraphHostView),
        new FrameworkPropertyMetadata(null, OnGraphChanged));

    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout),
        typeof(GraphLayoutKind),
        typeof(GraphHostView),
        new FrameworkPropertyMetadata(GraphLayoutKind.Hierarchical, OnLayoutChanged));

    public static readonly DependencyProperty VisibleNodesProperty = DependencyProperty.Register(
        nameof(VisibleNodes),
        typeof(IEnumerable<GraphNodeViewModel>),
        typeof(GraphHostView),
        new FrameworkPropertyMetadata(null, OnVisibleSubsetChanged));

    public static readonly DependencyProperty VisibleEdgesProperty = DependencyProperty.Register(
        nameof(VisibleEdges),
        typeof(IEnumerable<GraphEdgeViewModel>),
        typeof(GraphHostView),
        new FrameworkPropertyMetadata(null, OnVisibleSubsetChanged));

    public static readonly DependencyProperty SelectedNodeIdProperty = DependencyProperty.Register(
        nameof(SelectedNodeId),
        typeof(string),
        typeof(GraphHostView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty RightClickedNodeIdProperty = DependencyProperty.Register(
        nameof(RightClickedNodeId),
        typeof(string),
        typeof(GraphHostView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public ReachableGraph? Graph
    {
        get => (ReachableGraph?)GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public GraphLayoutKind Layout
    {
        get => (GraphLayoutKind)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public string? SelectedNodeId
    {
        get => (string?)GetValue(SelectedNodeIdProperty);
        set => SetValue(SelectedNodeIdProperty, value);
    }

    public string? RightClickedNodeId
    {
        get => (string?)GetValue(RightClickedNodeIdProperty);
        set => SetValue(RightClickedNodeIdProperty, value);
    }

    public IEnumerable<GraphNodeViewModel>? VisibleNodes
    {
        get => (IEnumerable<GraphNodeViewModel>?)GetValue(VisibleNodesProperty);
        set => SetValue(VisibleNodesProperty, value);
    }

    public IEnumerable<GraphEdgeViewModel>? VisibleEdges
    {
        get => (IEnumerable<GraphEdgeViewModel>?)GetValue(VisibleEdgesProperty);
        set => SetValue(VisibleEdgesProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewer is not null) return; // idempotent: Loaded can fire after tab re-show

        var viewer = new GraphViewer();
        viewer.BindToPanel(HostPanel);
        viewer.ObjectUnderMouseCursorChanged += OnObjectUnderMouseChanged;
        viewer.MouseDown += OnViewerMouseDown;
        _viewer = viewer;

        // Render whichever source was set before Loaded fired. VisibleNodes/Edges win over Graph.
        if (VisibleNodes is not null || VisibleEdges is not null) RenderFromVisible();
        else if (Graph is not null) Render(Graph);
    }

    private static void OnGraphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GraphHostView view) return;
        view.Render(e.NewValue as ReachableGraph);
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GraphHostView view) return;
        // Re-render with the existing graph + new layout choice.
        if (view.VisibleNodes is not null || view.VisibleEdges is not null) view.QueueRenderFromVisible();
        else view.Render(view.Graph);
    }

    private static void OnVisibleSubsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GraphHostView view) return;

        if (e.OldValue is INotifyCollectionChanged oldNc)
            oldNc.CollectionChanged -= view.OnVisibleCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newNc)
            newNc.CollectionChanged += view.OnVisibleCollectionChanged;

        view.QueueRenderFromVisible();
    }

    private void OnVisibleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        QueueRenderFromVisible();

    // Coalesce N CollectionChanged events (Clear + N×Add when the VM rebuilds the visible set)
    // into a single AGL layout pass — Sugiyama is O(V²+E), so per-event rendering on a 200-node
    // graph stalls the UI for tens of seconds.
    private void QueueRenderFromVisible()
    {
        if (_renderQueued) return;
        _renderQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _renderQueued = false;
            RenderFromVisible();
        }));
    }

    private void RenderFromVisible()
    {
        if (_viewer is null) return;

        var nodes = VisibleNodes;
        var edges = VisibleEdges;

        if ((nodes is null || !nodes.Any()) && (edges is null || !edges.Any()))
        {
            _viewer.Graph = null;
            return;
        }

        var drawing = new AglGraph();
        ApplyLayoutSettings(drawing, Layout);

        if (nodes is not null)
        {
            foreach (var n in nodes)
            {
                var label = n.UnexpandedNeighbourCount > 0
                    ? $"{n.NodeId}  +{n.UnexpandedNeighbourCount}"
                    : n.NodeId;
                drawing.AddNode(new AglNode(n.NodeId) { LabelText = label });
            }
        }

        if (edges is not null)
        {
            foreach (var e in edges) drawing.AddEdge(e.FromNodeId, e.ToNodeId);
        }

        _viewer.Graph = drawing;
    }

    private void Render(ReachableGraph? graph)
    {
        if (_viewer is null) return;

        if (graph is null)
        {
            _viewer.Graph = null;
            return;
        }

        var drawing = new AglGraph();
        ApplyLayoutSettings(drawing, Layout);

        foreach (var n in graph.Nodes)
        {
            var id = $"{n.Schema}.{n.Name}";
            var node = new AglNode(id) { LabelText = id };
            drawing.AddNode(node);
        }

        foreach (var e in graph.Edges)
        {
            var fromId = $"{e.FromSchema}.{e.FromName}";
            var toId = $"{e.ToSchema}.{e.ToName}";
            drawing.AddEdge(fromId, toId);
        }

        _viewer.Graph = drawing;
    }

    private static void ApplyLayoutSettings(AglGraph drawing, GraphLayoutKind kind)
    {
        LayoutAlgorithmSettings settings = kind switch
        {
            GraphLayoutKind.ForceDirected => new MdsLayoutSettings(),
            GraphLayoutKind.Layered => new SugiyamaLayoutSettings { LayerSeparation = 40 },
            _ => new SugiyamaLayoutSettings(),
        };
        drawing.LayoutAlgorithmSettings = settings;
    }

    private void OnObjectUnderMouseChanged(object? sender, ObjectUnderMouseCursorChangedEventArgs e)
    {
        // Reserved for future hover-feedback wiring (desktop-graph-tab inspector).
    }

    private void OnViewerMouseDown(object? sender, MsaglMouseEventArgs e)
    {
        if (_isUpdatingSelection || _viewer is null) return;

        var picked = _viewer.ObjectUnderMouseCursor;
        if (picked is IViewerNode viewerNode && viewerNode.Node is AglNode node)
        {
            _isUpdatingSelection = true;
            try
            {
                if (e.RightButtonIsPressed)
                {
                    SetValue(RightClickedNodeIdProperty, node.Id);
                }
                else
                {
                    SetValue(SelectedNodeIdProperty, node.Id);
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
    }
}
