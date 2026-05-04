#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Extras;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class ExtrasViewModelTests
{
    private sealed class Harness
    {
        public FakeGraphBuilder Builder { get; } = new();
        public FakeUiDispatcher Ui { get; } = new();
        public FakeDialogService Dialog { get; } = new();
        public FakeDatabaseExplorer Explorer { get; } = new();
        public int SaveCount;

        public GraphViewModel BuildGraph() =>
            new(Builder, Ui, Dialog, NullLoggerFactory.Instance);

        public ExtrasViewModel Build(GraphViewModel graph)
        {
            var vm = new ExtrasViewModel(graph, Explorer, Dialog, Ui, NullLoggerFactory.Instance);
            vm.Bind(() => { SaveCount++; return Task.CompletedTask; });
            return vm;
        }
    }

    private static WorkspaceModel WorkspaceWith(params SourceForScript[] scripts)
    {
        var ws = new WorkspaceModel
        {
            Name = "test",
            Source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" },
        };
        foreach (var s in scripts) ws.Package.Scripts.Add(s);
        return ws;
    }

    private static SourceForScript ScriptWithRootAndTables(string scriptName, string rootName,
        params TableToExtract[] tables)
    {
        var s = new SourceForScript { ScriptName = scriptName };
        s.RootRecords.Add(new RecordsToExtract { Schema = "dbo", TableName = rootName, Where = "1=1" });
        foreach (var t in tables) s.TablesToProcess.Add(t);
        return s;
    }

    private static TableToExtract Entry(string name, string schema = "dbo",
        ExtractStrategy? strategy = null) =>
        new(name, strategy ?? new OnlyOneTableExtractStrategy()) { Schema = schema };

    private static ReachableGraph Graph(params (string Schema, string Name)[] nodes)
    {
        var n = nodes.Select(t => new GraphNode(t.Schema, t.Name)).ToArray();
        return new ReachableGraph(n, Array.Empty<GraphEdge>());
    }

    [Test]
    public void Default_State_NoTables_HintEmpty()
    {
        var h = new Harness();
        var vm = h.Build(h.BuildGraph());

        vm.StandaloneTables.Should().BeEmpty();
        vm.EmptyHint.Should().BeEmpty();
    }

    [Test]
    public void Hydrate_NullWorkspace_ClearsAll()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(Graph(("dbo", "Patient")));
        var graph = h.BuildGraph();
        var vm = h.Build(graph);
        graph.Hydrate(WorkspaceWith(ScriptWithRootAndTables("s", "Patient", Entry("Lookup1"))), null);
        vm.Hydrate(WorkspaceWith(ScriptWithRootAndTables("s", "Patient", Entry("Lookup1"))), null);

        vm.Hydrate(null, null);

        vm.StandaloneTables.Should().BeEmpty();
        vm.AnchorScript.Should().BeNull();
    }

    [Test]
    public void Hydrate_GraphHasNoMatches_AllTablesAreStandalone()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(Graph(("dbo", "Patient")));
        var graph = h.BuildGraph();
        var script = ScriptWithRootAndTables("s", "Patient",
            Entry("Patient"),         // Patient is reachable (in graph)
            Entry("LookupCountry"),
            Entry("LookupCurrency"));
        var ws = WorkspaceWith(script);
        graph.Hydrate(ws, null);

        var vm = h.Build(graph);
        vm.Hydrate(ws, null);

        vm.StandaloneTables.Select(t => t.Name).Should()
            .BeEquivalentTo(new[] { "LookupCountry", "LookupCurrency" });
    }

    [Test]
    public void Hydrate_GraphReachableMatchesAll_NoStandaloneTables_HintShown()
    {
        var h = new Harness();
        h.Builder.EnqueueResult(Graph(("dbo", "Patient"), ("dbo", "Visit")));
        var graph = h.BuildGraph();
        var script = ScriptWithRootAndTables("s", "Patient",
            Entry("Patient"), Entry("Visit"));
        var ws = WorkspaceWith(script);
        graph.Hydrate(ws, null);

        var vm = h.Build(graph);
        vm.Hydrate(ws, null);

        vm.StandaloneTables.Should().BeEmpty();
        vm.EmptyHint.Should().NotBeEmpty();
    }

    [Test]
    public void Hydrate_GraphNotLoadedYet_FallsBackToShowAll()
    {
        var h = new Harness();
        var graph = h.BuildGraph();   // graph never hydrated → AllNodes empty
        var script = ScriptWithRootAndTables("s", "Patient",
            Entry("Lookup1"), Entry("Lookup2"));
        var ws = WorkspaceWith(script);

        var vm = h.Build(graph);
        vm.Hydrate(ws, null);

        vm.StandaloneTables.Select(t => t.Name).Should()
            .BeEquivalentTo(new[] { "Lookup1", "Lookup2" });
    }

    [Test]
    public void OnGraphAllNodesChanged_RecomputesFilter()
    {
        var h = new Harness();
        var graph = h.BuildGraph();
        var script = ScriptWithRootAndTables("s", "Patient", Entry("Lookup1"));
        var ws = WorkspaceWith(script);

        var vm = h.Build(graph);
        vm.Hydrate(ws, null);
        vm.StandaloneTables.Should().HaveCount(1);

        // Now hydrate the graph with Lookup1 reachable.
        h.Builder.EnqueueResult(Graph(("dbo", "Lookup1")));
        graph.Hydrate(ws, null);

        vm.StandaloneTables.Should().BeEmpty(
            "graph hydration must trigger ExtrasViewModel re-filter via INotifyCollectionChanged");
    }

    [Test]
    public void SetAnchor_TwoScripts_FiltersByAnchorScriptOnly()
    {
        var h = new Harness();
        var graph = h.BuildGraph();
        var s1 = ScriptWithRootAndTables("s1", "Patient", Entry("Lookup1"));
        var s2 = ScriptWithRootAndTables("s2", "Visit", Entry("Lookup2"));
        var ws = WorkspaceWith(s1, s2);

        var vm = h.Build(graph);
        vm.Hydrate(ws, null);
        vm.StandaloneTables.Select(t => t.Name).Should().BeEquivalentTo(new[] { "Lookup1" });

        vm.SetAnchor(s2);

        vm.StandaloneTables.Select(t => t.Name).Should().BeEquivalentTo(new[] { "Lookup2" });
    }

    [Test]
    public async Task AddStandaloneTableAsync_DialogReturnsTable_AppendsToScriptAndSaves()
    {
        var h = new Harness();
        var graph = h.BuildGraph();
        var script = ScriptWithRootAndTables("s", "Patient");
        var ws = WorkspaceWith(script);

        var vm = h.Build(graph);
        vm.Hydrate(ws, null);
        h.Dialog.EnqueueAddStandaloneTableResponse(new TableRef("dbo", "NewLookup"));

        await vm.AddStandaloneTableCommand.ExecuteAsync(null);

        script.TablesToProcess.Should().ContainSingle();
        script.TablesToProcess[0].TableName.Should().Be("NewLookup");
        script.TablesToProcess[0].Schema.Should().Be("dbo");
        script.TablesToProcess[0].ExtractStrategy.Should().BeOfType<OnlyOneTableExtractStrategy>();
        h.SaveCount.Should().Be(1);
        vm.StandaloneTables.Should().HaveCount(1);
    }

    [Test]
    public async Task AddStandaloneTableAsync_DialogReturnsNull_NoOp()
    {
        var h = new Harness();
        var graph = h.BuildGraph();
        var script = ScriptWithRootAndTables("s", "Patient");
        var ws = WorkspaceWith(script);

        var vm = h.Build(graph);
        vm.Hydrate(ws, null);
        h.Dialog.EnqueueAddStandaloneTableResponse(null);

        await vm.AddStandaloneTableCommand.ExecuteAsync(null);

        script.TablesToProcess.Should().BeEmpty();
        h.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task RemoveStandaloneTableAsync_RemovesFromScriptAndSaves()
    {
        var h = new Harness();
        var graph = h.BuildGraph();
        var entry = Entry("Lookup1");
        var script = ScriptWithRootAndTables("s", "Patient", entry);
        var ws = WorkspaceWith(script);

        var vm = h.Build(graph);
        vm.Hydrate(ws, null);
        var row = vm.StandaloneTables.Single();

        await row.RemoveCommand.ExecuteAsync(null);

        script.TablesToProcess.Should().NotContain(entry);
        vm.StandaloneTables.Should().BeEmpty();
        h.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task Hydrate_PopulatesTablesViaExplorer()
    {
        var h = new Harness();
        h.Explorer.EnqueueListResult(new[]
        {
            new TableRef("dbo", "Lookup1"),
            new TableRef("dbo", "Lookup2"),
        });
        var graph = h.BuildGraph();
        var script = ScriptWithRootAndTables("s", "Patient");
        var ws = WorkspaceWith(script);

        var vm = h.Build(graph);
        vm.Hydrate(ws, null);

        // RefreshTablesAsync is fire-and-forget; let it complete.
        await Task.Delay(20);

        vm.Tables.Select(t => t.Name).Should().BeEquivalentTo(new[] { "Lookup1", "Lookup2" });
    }
}
