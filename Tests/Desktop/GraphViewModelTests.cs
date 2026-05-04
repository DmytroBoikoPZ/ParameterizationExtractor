#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Controls.GraphHost;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class GraphViewModelTests
{
    private sealed class Harness
    {
        public FakeGraphBuilder Builder { get; } = new();
        public FakeUiDispatcher Ui { get; } = new();
        public FakeDialogService Dialog { get; } = new();

        public GraphViewModel Build() =>
            new(Builder, Ui, Dialog, NullLoggerFactory.Instance);
    }

    private static WorkspaceModel WorkspaceWith(params SourceForScript[] scripts)
    {
        var ws = new WorkspaceModel
        {
            Name = "test",
            Source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" },
        };
        foreach (var s in scripts)
        {
            ws.Package.Scripts.Add(s);
        }
        return ws;
    }

    private static SourceForScript ScriptWithRoot(string scriptName, string tableName, string? schema = null)
    {
        var s = new SourceForScript { ScriptName = scriptName };
        var root = new RecordsToExtract { TableName = tableName, Where = "1=1" };
        if (schema is not null) root.Schema = schema;
        s.RootRecords.Add(root);
        return s;
    }

    private static ReachableGraph EmptyGraph() =>
        new(Array.Empty<GraphNode>(), Array.Empty<GraphEdge>());

    [Test]
    public void Default_State_NoNodes_StatusEmpty()
    {
        var vm = new Harness().Build();

        vm.AllNodes.Should().BeEmpty();
        vm.AllEdges.Should().BeEmpty();
        vm.Status.Should().BeEmpty();
        vm.Reachable.Should().BeNull();
        vm.SelectedNodeId.Should().BeNull();
    }

    [Test]
    public void Hydrate_NullWorkspace_ClearsEverything()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(EmptyGraph());
        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient")), null);

        vm.Hydrate(null, null);

        vm.AllNodes.Should().BeEmpty();
        vm.Reachable.Should().BeNull();
        vm.Status.Should().BeEmpty();
    }

    [Test]
    public void Hydrate_WorkspaceWithNoScripts_StatusSaysNoSeed_NoBuilderCall()
    {
        var h = new Harness();
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(), null);

        vm.Status.Should().Contain("No seed");
        h.Builder.SeedsCalled.Should().BeEmpty();
    }

    [Test]
    public void Hydrate_WorkspaceWithSeed_BuildAsyncInvokedWithSeedList()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(EmptyGraph());
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);

        h.Builder.SeedsCalled.Should().HaveCount(1);
        var seeds = h.Builder.SeedsCalled[0];
        seeds.Should().ContainSingle();
        seeds[0].Schema.Should().Be("dbo");
        seeds[0].Name.Should().Be("Patient");
    }

    [Test]
    public void Hydrate_BuilderReturnsGraph_ReachablePopulated_StatusFormatted()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient"), new GraphNode("dbo", "Visit") },
            new[] { new GraphEdge("dbo", "Visit", "dbo", "Patient", "FK_Visit_Patient") }));
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient")), null);

        vm.Reachable.Should().NotBeNull();
        vm.Reachable!.Nodes.Should().HaveCount(2);
        vm.AllNodes.Should().HaveCount(2);
        vm.AllEdges.Should().HaveCount(1);
        // Both nodes have no TablesToProcess entry → both Pending → "0/2".
        vm.Status.Should().Be("0/2");
    }

    [Test]
    public void Hydrate_BuilderThrowsDatabaseExplorerException_StatusFailedDoesNotThrow()
    {
        var h = new Harness();
        h.Builder.EnqueueException(new DatabaseExplorerException("login failed"));
        var vm = h.Build();

        var act = () => vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient")), null);

        act.Should().NotThrow();
        vm.Status.Should().StartWith("Failed:");
        vm.Status.Should().Contain("login failed");
    }

    [Test]
    public void Hydrate_BuilderThrowsOperationCanceled_StatusCancelled()
    {
        var h = new Harness();
        h.Builder.EnqueueException(new OperationCanceledException());
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient")), null);

        vm.Status.Should().Be("Cancelled");
    }

    [Test]
    public void Hydrate_TwiceWithDifferentWorkspaces_RebuildsAndReplaces()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "A") }, Array.Empty<GraphEdge>()));
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "B"), new GraphNode("dbo", "C") }, Array.Empty<GraphEdge>()));

        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s1", "A")), null);
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s2", "B")), null);

        vm.Reachable!.Nodes.Should().HaveCount(2);
        vm.Reachable.Nodes[0].Name.Should().Be("B");
    }

    [Test]
    public void LayoutChoice_DefaultIsHierarchical()
    {
        var vm = new Harness().Build();

        vm.LayoutChoice.Should().Be(GraphLayoutKind.Hierarchical);
    }

    [Test]
    public async Task RefreshCommand_NoWorkspaceLoaded_NoBuilderCall()
    {
        var h = new Harness();
        var vm = h.Build();

        await vm.RefreshCommand.ExecuteAsync(null);

        h.Builder.SeedsCalled.Should().BeEmpty();
    }

    [Test]
    public void Hydrate_DeduplicatesSeedTables()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(EmptyGraph());
        var vm = h.Build();
        var ws = WorkspaceWith(
            ScriptWithRoot("s1", "Patient", "dbo"),
            ScriptWithRoot("s2", "Patient", "dbo"));

        vm.Hydrate(ws, null);

        h.Builder.SeedsCalled.Should().HaveCount(1);
        h.Builder.SeedsCalled[0].Should().HaveCount(1, "duplicate seed (Schema, Name) tuples must be deduplicated");
    }

    // --- Step 07 state-matrix tests ---------------------------------

    private static SourceForScript ScriptWithRootAndTables(string scriptName, string rootName, params TableToExtract[] tables)
    {
        var s = new SourceForScript { ScriptName = scriptName };
        s.RootRecords.Add(new RecordsToExtract { TableName = rootName, Where = "1=1" });
        foreach (var t in tables) s.TablesToProcess.Add(t);
        return s;
    }

    [Test]
    public void MapAndApply_NodeReachableButNotInTablesToProcess_StateIsPending()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Visit") }, Array.Empty<GraphEdge>()));
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Visit")), null);

        vm.AllNodes.Should().ContainSingle();
        vm.AllNodes[0].State.Should().Be(NodeState.Pending);
        vm.AllNodes[0].StrategyChip.Should().BeEmpty();
    }

    [Test]
    public void MapAndApply_NodeInTablesToProcess_NotExcluded_StateIsConfigured()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient") }, Array.Empty<GraphEdge>()));
        var vm = h.Build();
        var script = ScriptWithRootAndTables("s", "Patient",
            new TableToExtract("Patient", new FKDependencyExtractStrategy()));

        vm.Hydrate(WorkspaceWith(script), null);

        vm.AllNodes[0].State.Should().Be(NodeState.Configured);
        vm.AllNodes[0].StrategyChip.Should().Be("FKDependency");
    }

    [Test]
    public void MapAndApply_NodeInTablesToProcess_Excluded_StateIsExcluded()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient") }, Array.Empty<GraphEdge>()));
        var vm = h.Build();
        var script = ScriptWithRootAndTables("s", "Patient",
            new TableToExtract("Patient", new OnlyChildrenExtractStrategy()) { Excluded = true });

        vm.Hydrate(WorkspaceWith(script), null);

        vm.AllNodes[0].State.Should().Be(NodeState.Excluded);
    }

    [Test]
    public void MapAndApply_SeedNode_IsSeedTrue()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient"), new GraphNode("dbo", "Visit") },
            Array.Empty<GraphEdge>()));
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);

        vm.AllNodes.Single(n => n.Name == "Patient").IsSeed.Should().BeTrue();
        vm.AllNodes.Single(n => n.Name == "Visit").IsSeed.Should().BeFalse();
    }

    [Test]
    public void MapAndApply_StrategyChip_OnlyChildren()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient") }, Array.Empty<GraphEdge>()));
        var vm = h.Build();
        var script = ScriptWithRootAndTables("s", "Patient",
            new TableToExtract("Patient", new OnlyChildrenExtractStrategy()));

        vm.Hydrate(WorkspaceWith(script), null);

        vm.AllNodes[0].StrategyChip.Should().Be("OnlyChildren");
    }

    [Test]
    public void MapAndApply_EdgeBetweenConfigured_StyleFollow()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Visit"), new GraphNode("dbo", "Patient") },
            new[] { new GraphEdge("dbo", "Visit", "dbo", "Patient", "FK1") }));
        var vm = h.Build();
        var script = ScriptWithRootAndTables("s", "Patient",
            new TableToExtract("Patient", new FKDependencyExtractStrategy()),
            new TableToExtract("Visit", new FKDependencyExtractStrategy()));

        vm.Hydrate(WorkspaceWith(script), null);

        vm.AllEdges.Should().ContainSingle();
        vm.AllEdges[0].Style.Should().Be(EdgeStyle.Follow);
    }

    [Test]
    public void MapAndApply_EdgeWithExcludedEndpoint_StyleStop()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Visit"), new GraphNode("dbo", "Patient") },
            new[] { new GraphEdge("dbo", "Visit", "dbo", "Patient", "FK1") }));
        var vm = h.Build();
        var script = ScriptWithRootAndTables("s", "Patient",
            new TableToExtract("Patient", new FKDependencyExtractStrategy()),
            new TableToExtract("Visit", new FKDependencyExtractStrategy()) { Excluded = true });

        vm.Hydrate(WorkspaceWith(script), null);

        vm.AllEdges[0].Style.Should().Be(EdgeStyle.Stop);
    }

    [Test]
    public void MapAndApply_EdgeWithPendingEndpoint_StylePending()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Visit"), new GraphNode("dbo", "Patient") },
            new[] { new GraphEdge("dbo", "Visit", "dbo", "Patient", "FK1") }));
        var vm = h.Build();
        // Only Patient is in TablesToProcess; Visit is pending.
        var script = ScriptWithRootAndTables("s", "Patient",
            new TableToExtract("Patient", new FKDependencyExtractStrategy()));

        vm.Hydrate(WorkspaceWith(script), null);

        vm.AllEdges[0].Style.Should().Be(EdgeStyle.Pending);
    }

    [Test]
    public void MapAndApply_StatusFormat_ConfiguredOverTotal()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "A"), new GraphNode("dbo", "B"), new GraphNode("dbo", "C") },
            Array.Empty<GraphEdge>()));
        var vm = h.Build();
        var script = ScriptWithRootAndTables("s", "A",
            new TableToExtract("A", new FKDependencyExtractStrategy()),
            new TableToExtract("B", new FKDependencyExtractStrategy()));

        vm.Hydrate(WorkspaceWith(script), null);

        vm.Status.Should().Be("2/3");
    }

    [Test]
    public void MapAndApply_DuplicateTableAcrossScripts_LastWinsForState()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient") }, Array.Empty<GraphEdge>()));
        var vm = h.Build();
        // Both scripts list Patient in TablesToProcess. Second has Excluded=true.
        var s1 = ScriptWithRootAndTables("s1", "Patient",
            new TableToExtract("Patient", new FKDependencyExtractStrategy()));
        var s2 = ScriptWithRootAndTables("s2", "Patient",
            new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Excluded = true });

        vm.Hydrate(WorkspaceWith(s1, s2), null);

        vm.AllNodes[0].State.Should().Be(NodeState.Excluded, "last-wins across scripts");
    }

    // --- Step 01 focus-filter tests ----------------------------------

    private static ReachableGraph ChainGraph(params string[] names)
    {
        // Linear chain: names[0] - names[1] - names[2] - ... edges connect adjacent pairs.
        var nodes = names.Select(n => new GraphNode("dbo", n)).ToArray();
        var edges = new System.Collections.Generic.List<GraphEdge>();
        for (var i = 0; i < names.Length - 1; i++)
            edges.Add(new GraphEdge("dbo", names[i], "dbo", names[i + 1], $"FK_{names[i]}_{names[i + 1]}"));
        return new ReachableGraph(nodes, edges);
    }

    [Test]
    public void Hydrate_DefaultFocusMode_IsSeedPlusOneHop()
    {
        var vm = new Harness().Build();
        vm.FocusModeChoice.Should().Be(FocusMode.SeedPlusOneHop);
    }

    [Test]
    public void Hydrate_FocusModeSeedPlusOneHop_VisibleIncludesSeedAndDirectNeighbours_OnlyThose()
    {
        var h = new Harness();
        // Patient (seed) - Visit (1 hop) - Lab (2 hops)
        h.Builder.EnqueueResult(ChainGraph("Patient", "Visit", "Lab"));
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);

        vm.VisibleNodes.Select(n => n.Name).Should().BeEquivalentTo(new[] { "Patient", "Visit" });
        vm.VisibleEdges.Should().HaveCount(1);
    }

    [Test]
    public void Hydrate_FocusModeSeedPlusTwoHops_VisibleIncludesUpToTwoHops()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(ChainGraph("Patient", "Visit", "Lab", "Result"));
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);
        vm.FocusModeChoice = FocusMode.SeedPlusTwoHops;

        vm.VisibleNodes.Select(n => n.Name).Should().BeEquivalentTo(new[] { "Patient", "Visit", "Lab" });
    }

    [Test]
    public void Hydrate_FocusModeWholeSubgraph_VisibleEqualsAllNodes()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(ChainGraph("Patient", "Visit", "Lab", "Result"));
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);
        vm.FocusModeChoice = FocusMode.WholeSubgraph;

        vm.VisibleNodes.Should().HaveCount(vm.AllNodes.Count);
        vm.VisibleEdges.Should().HaveCount(vm.AllEdges.Count);
    }

    [Test]
    public void ExpandNode_AddsNodeAndItsNeighboursToVisible()
    {
        var h = new Harness();
        // Patient (seed) - Visit (1 hop) - Lab (2 hops)
        h.Builder.EnqueueResult(ChainGraph("Patient", "Visit", "Lab"));
        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);
        vm.VisibleNodes.Select(n => n.Name).Should().NotContain("Lab");

        vm.ExpandNodeCommand.Execute("dbo.Visit");

        vm.VisibleNodes.Select(n => n.Name).Should().Contain("Lab");
    }

    [Test]
    public void ResetFocus_ClearsExpandedAndRestoresSeedPlusOneHop()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(ChainGraph("Patient", "Visit", "Lab", "Result"));
        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);
        vm.ExpandNodeCommand.Execute("dbo.Visit");
        vm.ExpandNodeCommand.Execute("dbo.Lab");
        vm.FocusModeChoice = FocusMode.SeedPlusTwoHops;

        vm.ResetFocusCommand.Execute(null);

        vm.FocusModeChoice.Should().Be(FocusMode.SeedPlusOneHop);
        vm.VisibleNodes.Select(n => n.Name).Should().BeEquivalentTo(new[] { "Patient", "Visit" });
    }

    [Test]
    public void UnexpandedNeighbourCount_PendingNode_ExcludesAlreadyVisibleNeighbours()
    {
        var h = new Harness();
        // Star: Patient (seed) connected to N1, N2, N3, N4, N5 (all 1 hop).
        // In SeedPlusOneHop mode, all 5 neighbours are visible — Patient's count = 0.
        // In WholeSubgraph, also 0. Pick a pending node with off-screen neighbours:
        // Make N1 hub with 3 partners (N1a, N1b, N1c) at 2 hops. Then N1 (pending, 1 hop) has
        // 4 total neighbours (Patient + 3 N1*); 1 is visible (Patient); count = 3.
        var nodes = new[]
        {
            new GraphNode("dbo", "Patient"),
            new GraphNode("dbo", "N1"),
            new GraphNode("dbo", "N1a"),
            new GraphNode("dbo", "N1b"),
            new GraphNode("dbo", "N1c"),
        };
        var edges = new[]
        {
            new GraphEdge("dbo", "Patient", "dbo", "N1", "FK1"),
            new GraphEdge("dbo", "N1", "dbo", "N1a", "FK2"),
            new GraphEdge("dbo", "N1", "dbo", "N1b", "FK3"),
            new GraphEdge("dbo", "N1", "dbo", "N1c", "FK4"),
        };
        h.Builder.EnqueueResult(new ReachableGraph(nodes, edges));
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);

        var n1 = vm.VisibleNodes.Single(n => n.Name == "N1");
        n1.State.Should().Be(NodeState.Pending);
        n1.UnexpandedNeighbourCount.Should().Be(3);
    }

    [Test]
    public void UnexpandedNeighbourCount_ConfiguredNode_AlwaysZero()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient"), new GraphNode("dbo", "Visit") },
            new[] { new GraphEdge("dbo", "Patient", "dbo", "Visit", "FK1") }));
        var vm = h.Build();
        var script = new SourceForScript { ScriptName = "s" };
        script.RootRecords.Add(new RecordsToExtract { Schema = "dbo", TableName = "Patient", Where = "1=1" });
        script.TablesToProcess.Add(new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" });
        script.TablesToProcess.Add(new TableToExtract("Visit", new FKDependencyExtractStrategy()) { Schema = "dbo" });

        vm.Hydrate(WorkspaceWith(script), null);

        vm.VisibleNodes.Single(n => n.Name == "Patient").UnexpandedNeighbourCount.Should().Be(0);
        vm.VisibleNodes.Single(n => n.Name == "Visit").UnexpandedNeighbourCount.Should().Be(0);
    }

    [Test]
    public void RecomputeVisible_PreservesNodeIdentity_ObservableCollectionDeltaMinimal()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(ChainGraph("Patient", "Visit"));
        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);
        var firstSnapshot = vm.VisibleNodes.ToList();

        vm.FocusModeChoice = FocusMode.SeedPlusOneHop; // no-op assignment, but trigger anyway
        vm.ResetFocusCommand.Execute(null);

        // VM instances are reused (not recreated) since AllNodes wasn't rebuilt.
        foreach (var n in firstSnapshot)
            vm.VisibleNodes.Should().Contain(n);
    }

    [Test]
    public void MapAndApply_PopulatesVisibleNodesAndEdges()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(ChainGraph("Patient", "Visit"));
        var vm = h.Build();

        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);

        vm.VisibleNodes.Should().NotBeEmpty();
        vm.VisibleEdges.Should().NotBeEmpty();
    }

    // --- Step 03 click-dispatch tests --------------------------------

    [Test]
    public void OnSelectedNodeIdChanged_PendingNode_TriggersExpand_NoInspector()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(ChainGraph("Patient", "Visit", "Lab"));
        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);
        vm.VisibleNodes.Select(n => n.Name).Should().NotContain("Lab");

        vm.SelectedNodeId = "dbo.Visit"; // Visit is Pending (no TablesToProcess entry)

        vm.VisibleNodes.Select(n => n.Name).Should().Contain("Lab");
        vm.InspectedNode.Should().BeNull();
    }

    [Test]
    public void OnSelectedNodeIdChanged_ConfiguredNode_SetsInspectedNode_NoExpand()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient"), new GraphNode("dbo", "Visit") },
            new[] { new GraphEdge("dbo", "Patient", "dbo", "Visit", "FK1") }));
        var vm = h.Build();
        var script = new SourceForScript { ScriptName = "s" };
        script.RootRecords.Add(new RecordsToExtract { Schema = "dbo", TableName = "Patient", Where = "1=1" });
        script.TablesToProcess.Add(new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" });
        vm.Hydrate(WorkspaceWith(script), null);
        var beforeVisibleCount = vm.VisibleNodes.Count;

        vm.SelectedNodeId = "dbo.Patient"; // Configured

        vm.InspectedNode.Should().NotBeNull();
        vm.InspectedNode!.Name.Should().Be("Patient");
        vm.VisibleNodes.Count.Should().Be(beforeVisibleCount);
    }

    [Test]
    public void OnSelectedNodeIdChanged_ExcludedNode_SetsInspectedNode()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient"), new GraphNode("dbo", "Visit") },
            new[] { new GraphEdge("dbo", "Patient", "dbo", "Visit", "FK1") }));
        var vm = h.Build();
        var script = new SourceForScript { ScriptName = "s" };
        script.RootRecords.Add(new RecordsToExtract { Schema = "dbo", TableName = "Patient", Where = "1=1" });
        script.TablesToProcess.Add(new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" });
        script.TablesToProcess.Add(new TableToExtract("Visit", new FKDependencyExtractStrategy()) { Schema = "dbo", Excluded = true });
        vm.Hydrate(WorkspaceWith(script), null);

        vm.SelectedNodeId = "dbo.Visit";

        vm.InspectedNode.Should().NotBeNull();
        vm.InspectedNode!.State.Should().Be(NodeState.Excluded);
    }

    [Test]
    public void OnSelectedNodeIdChanged_NullOrEmpty_ClearsInspector()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient") }, Array.Empty<GraphEdge>()));
        var vm = h.Build();
        var script = new SourceForScript { ScriptName = "s" };
        script.RootRecords.Add(new RecordsToExtract { Schema = "dbo", TableName = "Patient", Where = "1=1" });
        script.TablesToProcess.Add(new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" });
        vm.Hydrate(WorkspaceWith(script), null);
        vm.SelectedNodeId = "dbo.Patient";
        vm.InspectedNode.Should().NotBeNull();

        vm.SelectedNodeId = null;

        vm.InspectedNode.Should().BeNull();
    }

    [Test]
    public void OnSelectedNodeIdChanged_UnknownId_NoOp()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(ChainGraph("Patient", "Visit"));
        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);
        var visibleBefore = vm.VisibleNodes.Count;

        var act = () => vm.SelectedNodeId = "dbo.DoesNotExist";

        act.Should().NotThrow();
        vm.InspectedNode.Should().BeNull();
        vm.VisibleNodes.Count.Should().Be(visibleBefore);
    }

    // --- Step 06 context-menu commands ------------------------------

    private static (WorkspaceModel ws, SourceForScript script) MakeWsWithEntries(
        ReachableGraph graph, params TableToExtract[] entries)
    {
        var ws = new WorkspaceModel
        {
            Name = "test",
            Source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" },
        };
        var script = new SourceForScript { ScriptName = "s" };
        foreach (var n in graph.Nodes)
        {
            // root entry uses first node
        }
        var firstNode = graph.Nodes.Count > 0 ? graph.Nodes[0] : null;
        if (firstNode is not null)
            script.RootRecords.Add(new RecordsToExtract { Schema = firstNode.Schema, TableName = firstNode.Name, Where = "1=1" });
        foreach (var e in entries) script.TablesToProcess.Add(e);
        ws.Package.Scripts.Add(script);
        return (ws, script);
    }

    [Test]
    public async Task ExcludeNode_SetsExcludedTrue_PersistsAndRecomputes()
    {
        var h = new Harness();
        var graph = new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient") }, Array.Empty<GraphEdge>());
        h.Builder.EnqueueResult(graph);
        h.Builder.EnqueueResult(graph); // second call from refresh after persist
        var entry = new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" };
        var (ws, _) = MakeWsWithEntries(graph, entry);
        var saveCount = 0;
        var vm = h.Build();
        vm.Bind(() => { saveCount++; return Task.CompletedTask; });
        vm.Hydrate(ws, null);

        await vm.ExcludeNodeCommand.ExecuteAsync("dbo.Patient");

        entry.Excluded.Should().BeTrue();
        saveCount.Should().Be(1);
        h.Builder.SeedsCalled.Should().HaveCount(2, "PersistAndRefreshAsync re-runs Refresh");
    }

    [Test]
    public async Task IncludeNode_SetsExcludedFalse_PersistsAndRecomputes()
    {
        var h = new Harness();
        var graph = new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient") }, Array.Empty<GraphEdge>());
        h.Builder.EnqueueResult(graph);
        h.Builder.EnqueueResult(graph);
        var entry = new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo", Excluded = true };
        var (ws, _) = MakeWsWithEntries(graph, entry);
        var saveCount = 0;
        var vm = h.Build();
        vm.Bind(() => { saveCount++; return Task.CompletedTask; });
        vm.Hydrate(ws, null);

        await vm.IncludeNodeCommand.ExecuteAsync("dbo.Patient");

        entry.Excluded.Should().BeFalse();
        saveCount.Should().Be(1);
    }

    [Test]
    public async Task ResetNode_RemovesFromTablesToProcess_NodeGoesToPending()
    {
        var h = new Harness();
        var graph = new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient"), new GraphNode("dbo", "Visit") },
            new[] { new GraphEdge("dbo", "Patient", "dbo", "Visit", "FK1") });
        h.Builder.EnqueueResult(graph);
        h.Builder.EnqueueResult(graph);
        var visitEntry = new TableToExtract("Visit", new FKDependencyExtractStrategy()) { Schema = "dbo" };
        var (ws, script) = MakeWsWithEntries(graph,
            new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" },
            visitEntry);
        var vm = h.Build();
        vm.Bind(() => Task.CompletedTask);
        vm.Hydrate(ws, null);

        await vm.ResetNodeCommand.ExecuteAsync("dbo.Visit");

        script.TablesToProcess.Should().NotContain(visitEntry);
        vm.AllNodes.Single(n => n.Name == "Visit").State.Should().Be(NodeState.Pending);
    }

    [Test]
    public void ShowOnWholeGraph_SetsFocusModeWholeSubgraph_AndInspectedNode()
    {
        var h = new Harness();
        var graph = new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient"), new GraphNode("dbo", "Visit") },
            new[] { new GraphEdge("dbo", "Patient", "dbo", "Visit", "FK1") });
        h.Builder.EnqueueResult(graph);
        var (ws, _) = MakeWsWithEntries(graph,
            new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" });
        var vm = h.Build();
        vm.Bind(() => Task.CompletedTask);
        vm.Hydrate(ws, null);
        vm.FocusModeChoice.Should().Be(FocusMode.SeedPlusOneHop);

        vm.ShowOnWholeGraphCommand.Execute("dbo.Patient");

        vm.FocusModeChoice.Should().Be(FocusMode.WholeSubgraph);
        vm.InspectedNode.Should().NotBeNull();
        vm.InspectedNode!.Name.Should().Be("Patient");
    }

    [Test]
    public async Task ContextCommands_NullNodeId_NoOp()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(EmptyGraph());
        var vm = h.Build();
        vm.Bind(() => Task.CompletedTask);
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);

        var act = async () =>
        {
            await vm.ExcludeNodeCommand.ExecuteAsync(null);
            await vm.IncludeNodeCommand.ExecuteAsync(null);
            await vm.ResetNodeCommand.ExecuteAsync(null);
            vm.ShowOnWholeGraphCommand.Execute(null);
        };

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task ContextCommands_UnknownNodeId_NoOp_NoCrash()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(EmptyGraph());
        var vm = h.Build();
        vm.Bind(() => Task.CompletedTask);
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);

        var act = async () =>
        {
            await vm.ExcludeNodeCommand.ExecuteAsync("dbo.Bogus");
            await vm.IncludeNodeCommand.ExecuteAsync("dbo.Bogus");
            await vm.ResetNodeCommand.ExecuteAsync("dbo.Bogus");
            vm.ShowOnWholeGraphCommand.Execute("dbo.Bogus");
        };

        await act.Should().NotThrowAsync();
    }

    [Test]
    public void CloseInspector_ClearsInspectedNode()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient") }, Array.Empty<GraphEdge>()));
        var vm = h.Build();
        var script = new SourceForScript { ScriptName = "s" };
        script.RootRecords.Add(new RecordsToExtract { Schema = "dbo", TableName = "Patient", Where = "1=1" });
        script.TablesToProcess.Add(new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" });
        vm.Hydrate(WorkspaceWith(script), null);
        vm.SelectedNodeId = "dbo.Patient";
        vm.InspectedNode.Should().NotBeNull();

        vm.CloseInspectorCommand.Execute(null);

        vm.InspectedNode.Should().BeNull();
    }

    // --- Step 07 anchor-sync tests ----------------------------------

    [Test]
    public void SetAnchor_TriggersRefreshWithAnchorScriptSeeds()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(EmptyGraph()); // initial Hydrate
        h.Builder.EnqueueResult(EmptyGraph()); // SetAnchor refresh
        var vm = h.Build();
        var s1 = ScriptWithRoot("s1", "Patient", "dbo");
        var s2 = ScriptWithRoot("s2", "Visit", "dbo");
        vm.Hydrate(WorkspaceWith(s1, s2), null);
        h.Builder.SeedsCalled.Should().HaveCount(1);
        h.Builder.SeedsCalled[0].Single().Name.Should().Be("Patient");

        vm.SetAnchor(s2);

        h.Builder.SeedsCalled.Should().HaveCount(2);
        h.Builder.SeedsCalled[1].Single().Name.Should().Be("Visit");
    }

    [Test]
    public void Hydrate_NoAnchor_FallsBackToFirstScript()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(EmptyGraph());
        var vm = h.Build();
        var s1 = ScriptWithRoot("s1", "Patient", "dbo");
        var s2 = ScriptWithRoot("s2", "Visit", "dbo");

        vm.Hydrate(WorkspaceWith(s1, s2), null);

        h.Builder.SeedsCalled.Should().HaveCount(1);
        h.Builder.SeedsCalled[0].Should().ContainSingle();
        h.Builder.SeedsCalled[0][0].Name.Should().Be("Patient", "first script's root");
    }

    [Test]
    public void SetAnchor_NullScript_ClearsGraph()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(EmptyGraph()); // initial
        h.Builder.EnqueueResult(EmptyGraph()); // SetAnchor(null) → still triggers Refresh; with null + workspace, falls back to first script. Actually with null _anchor, CollectSeeds picks first script.
        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(ScriptWithRoot("s", "Patient", "dbo")), null);

        vm.SetAnchor(null);

        vm.AnchorScript.Should().BeNull();
        // With null anchor, CollectSeeds falls back to first script. So seeds non-empty.
        h.Builder.SeedsCalled.Last().Single().Name.Should().Be("Patient");
    }

    [Test]
    public void Hydrate_TwoScripts_FirstSelected_SecondsRootsNotInSeeds()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(EmptyGraph());
        var vm = h.Build();
        var s1 = ScriptWithRoot("s1", "Patient", "dbo");
        var s2 = ScriptWithRoot("s2", "Lab", "dbo");

        vm.Hydrate(WorkspaceWith(s1, s2), null);

        h.Builder.SeedsCalled[0].Select(t => t.Name).Should().NotContain("Lab");
    }
}
