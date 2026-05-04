#nullable enable
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Controls.StrategyPicker;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.Desktop;

[TestFixture]
public class NodeInspectorViewModelTests
{
    private sealed class Harness
    {
        public int SaveCount;
        public int RecomputeCount;

        public NodeInspectorViewModel Build(TimeSpan? debounce = null)
        {
            return new NodeInspectorViewModel(
                () => { SaveCount++; return Task.CompletedTask; },
                () => { RecomputeCount++; return Task.CompletedTask; },
                NullLogger<NodeInspectorViewModel>.Instance,
                debounce ?? TimeSpan.Zero);
        }
    }

    private static (WorkspaceModel ws, SourceForScript script) WorkspaceWithScript(params TableToExtract[] tables)
    {
        var ws = new WorkspaceModel
        {
            Name = "test",
            Source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" },
        };
        var script = new SourceForScript { ScriptName = "s" };
        script.RootRecords.Add(new RecordsToExtract { Schema = "dbo", TableName = "Patient", Where = "1=1" });
        foreach (var t in tables) script.TablesToProcess.Add(t);
        ws.Package.Scripts.Add(script);
        return (ws, script);
    }

    private static GraphNodeViewModel ConfiguredNode(string name = "Patient", string schema = "dbo") =>
        new(schema, name, isSeed: false, NodeState.Configured, "FKDependency");

    private static GraphNodeViewModel PendingNode(string name = "Visit", string schema = "dbo") =>
        new(schema, name, isSeed: false, NodeState.Pending, string.Empty);

    [Test]
    public void Open_ConfiguredNode_HydratesStrategyAndWhere()
    {
        var h = new Harness();
        var entry = new TableToExtract("Patient", new OnlyChildrenExtractStrategy { Where = "Id > 0" }) { Schema = "dbo" };
        var (ws, script) = WorkspaceWithScript(entry);
        var vm = h.Build();

        vm.Open(ConfiguredNode(), ws, script);

        vm.IsAddable.Should().BeFalse();
        vm.StrategyChoice.Should().Be(StrategyKind.OnlyChildren);
        vm.Where.Should().Be("Id > 0");
        vm.Excluded.Should().BeFalse();
        h.SaveCount.Should().Be(0, "hydrate must not fire save");
    }

    [Test]
    public void Open_PendingNode_IsAddableTrue_FieldsDefault()
    {
        var h = new Harness();
        var (ws, script) = WorkspaceWithScript();
        var vm = h.Build();

        vm.Open(PendingNode(), ws, script);

        vm.IsAddable.Should().BeTrue();
        vm.StrategyChoice.Should().Be(StrategyKind.FKDependency);
        vm.Where.Should().BeEmpty();
        vm.Excluded.Should().BeFalse();
    }

    [Test]
    public void OnStrategyChoiceChanged_ReplacesEngineStrategy_PreservesWhere()
    {
        var h = new Harness();
        var entry = new TableToExtract("Patient", new FKDependencyExtractStrategy { Where = "Active = 1" }) { Schema = "dbo" };
        var (ws, script) = WorkspaceWithScript(entry);
        var vm = h.Build();
        vm.Open(ConfiguredNode(), ws, script);

        vm.StrategyChoice = StrategyKind.OnlyParent;

        entry.ExtractStrategy.Should().BeOfType<OnlyParentExtractStrategy>();
        entry.ExtractStrategy.Where.Should().Be("Active = 1");
        h.SaveCount.Should().Be(1);
    }

    [Test]
    public void OnWhereChanged_MutatesEngineWhere_FiresSave()
    {
        var h = new Harness();
        var entry = new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" };
        var (ws, script) = WorkspaceWithScript(entry);
        var vm = h.Build();
        vm.Open(ConfiguredNode(), ws, script);

        vm.Where = "Id < 100";

        entry.ExtractStrategy.Where.Should().Be("Id < 100");
        h.SaveCount.Should().Be(1);
    }

    [Test]
    public void OnExcludedChanged_MutatesEngineExcluded_FiresSave()
    {
        var h = new Harness();
        var entry = new TableToExtract("Patient", new FKDependencyExtractStrategy()) { Schema = "dbo" };
        var (ws, script) = WorkspaceWithScript(entry);
        var vm = h.Build();
        vm.Open(ConfiguredNode(), ws, script);

        vm.Excluded = true;

        entry.Excluded.Should().BeTrue();
        h.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task AddToExtractAsync_CreatesTableToExtractEntry_FiresSaveAndRecompute()
    {
        var h = new Harness();
        var (ws, script) = WorkspaceWithScript();
        var vm = h.Build();
        vm.Open(PendingNode("Visit", "dbo"), ws, script);

        await vm.AddToExtractCommand.ExecuteAsync(null);

        script.TablesToProcess.Should().ContainSingle();
        script.TablesToProcess[0].TableName.Should().Be("Visit");
        script.TablesToProcess[0].Schema.Should().Be("dbo");
        script.TablesToProcess[0].ExtractStrategy.Should().BeOfType<FKDependencyExtractStrategy>();
        h.SaveCount.Should().Be(1);
        h.RecomputeCount.Should().Be(1);
    }

    [Test]
    public async Task AddToExtractAsync_SetsSourceAndIsAddableFalse()
    {
        var h = new Harness();
        var (ws, script) = WorkspaceWithScript();
        var vm = h.Build();
        vm.Open(PendingNode(), ws, script);
        vm.IsAddable.Should().BeTrue();

        await vm.AddToExtractCommand.ExecuteAsync(null);

        vm.Source.Should().NotBeNull();
        vm.IsAddable.Should().BeFalse();
    }

    [Test]
    public void HydrateFromSource_DoesNotFireSave()
    {
        var h = new Harness();
        var entry = new TableToExtract("Patient", new OnlyChildrenExtractStrategy { Where = "X = 1" }) { Schema = "dbo", Excluded = true };
        var (ws, script) = WorkspaceWithScript(entry);
        var vm = h.Build();

        vm.Open(ConfiguredNode(), ws, script);

        h.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task RecomputeGraphAsync_DelegatesToCallback()
    {
        var h = new Harness();
        var vm = h.Build();

        await vm.RecomputeGraphCommand.ExecuteAsync(null);

        h.RecomputeCount.Should().Be(1);
    }
}
