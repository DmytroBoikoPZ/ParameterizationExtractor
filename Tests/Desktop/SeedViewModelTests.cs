#nullable enable
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Seed;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class SeedViewModelTests
{
    private sealed class Harness
    {
        public FakeDatabaseExplorer Explorer { get; } = new();
        public FakeUiDispatcher Ui { get; } = new();
        public FakeDialogService Dialog { get; } = new();
        public ILoggerFactory LoggerFactory { get; } = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        public int SaveCount { get; private set; }

        public SeedViewModel Build()
        {
            var vm = new SeedViewModel(Explorer, Ui, Dialog, LoggerFactory);
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
        foreach (var s in scripts)
        {
            ws.Package.Scripts.Add(s);
        }
        return ws;
    }

    [Test]
    public void Default_State_NoScripts_HasScriptsFalse()
    {
        var vm = new Harness().Build();

        vm.Scripts.Should().BeEmpty();
        vm.HasScripts.Should().BeFalse();
        vm.SelectedScript.Should().BeNull();
    }

    [Test]
    public void Bind_BeforeHydrate_NoErrors()
    {
        var act = () => new Harness().Build();
        act.Should().NotThrow();
    }

    [Test]
    public void Hydrate_NullWorkspace_ClearsScripts()
    {
        var h = new Harness();
        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(new SourceForScript { ScriptName = "x" }), null);
        vm.Scripts.Should().HaveCount(1);

        vm.Hydrate(null, null);

        vm.Scripts.Should().BeEmpty();
        vm.SelectedScript.Should().BeNull();
        vm.HasScripts.Should().BeFalse();
    }

    [Test]
    public void Hydrate_WithEmptyPackage_NoScripts_SelectedScriptNull()
    {
        var vm = new Harness().Build();

        vm.Hydrate(WorkspaceWith(), null);

        vm.Scripts.Should().BeEmpty();
        vm.SelectedScript.Should().BeNull();
    }

    [Test]
    public void Hydrate_WithTwoScripts_PopulatesCollection_SelectsFirst()
    {
        var vm = new Harness().Build();
        var ws = WorkspaceWith(
            new SourceForScript { ScriptName = "First" },
            new SourceForScript { ScriptName = "Second" });

        vm.Hydrate(ws, null);

        vm.Scripts.Should().HaveCount(2);
        vm.Scripts[0].ScriptName.Should().Be("First");
        vm.Scripts[1].ScriptName.Should().Be("Second");
        vm.SelectedScript.Should().BeSameAs(vm.Scripts[0]);
    }

    [Test]
    public void Hydrate_TwiceWithDifferentWorkspaces_RebuildsCollection()
    {
        var vm = new Harness().Build();
        vm.Hydrate(WorkspaceWith(new SourceForScript { ScriptName = "A" }), null);
        vm.Scripts.Should().HaveCount(1);

        vm.Hydrate(WorkspaceWith(
            new SourceForScript { ScriptName = "B" },
            new SourceForScript { ScriptName = "C" }), null);

        vm.Scripts.Should().HaveCount(2);
        vm.Scripts[0].ScriptName.Should().Be("B");
    }

    [Test]
    public async Task AddScript_AppendsToWorkspaceAndCollection_AndSelectsNew()
    {
        var h = new Harness();
        var vm = h.Build();
        var ws = WorkspaceWith();
        vm.Hydrate(ws, null);

        await vm.AddScriptCommand.ExecuteAsync(null);

        vm.Scripts.Should().HaveCount(1);
        ws.Package.Scripts.Should().HaveCount(1);
        vm.SelectedScript.Should().BeSameAs(vm.Scripts[0]);
    }

    [Test]
    public async Task AddScript_GeneratesUniqueDefaultName()
    {
        var vm = new Harness().Build();
        vm.Hydrate(WorkspaceWith(), null);

        await vm.AddScriptCommand.ExecuteAsync(null);
        await vm.AddScriptCommand.ExecuteAsync(null);

        vm.Scripts[0].ScriptName.Should().Be("NewScript");
        vm.Scripts[1].ScriptName.Should().Be("NewScript (2)");
    }

    [Test]
    public async Task AddScript_TriggersSaveCallback()
    {
        var h = new Harness();
        var vm = h.Build();
        vm.Hydrate(WorkspaceWith(), null);
        var before = h.SaveCount;

        await vm.AddScriptCommand.ExecuteAsync(null);

        h.SaveCount.Should().Be(before + 1);
    }

    [Test]
    public void AddScript_NoWorkspaceLoaded_CanExecuteFalse()
    {
        var vm = new Harness().Build();

        vm.AddScriptCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public async Task RemoveSelectedScript_RemovesFromBoth_UpdatesSelection_TriggersSave()
    {
        var h = new Harness();
        var vm = h.Build();
        var ws = WorkspaceWith(
            new SourceForScript { ScriptName = "A" },
            new SourceForScript { ScriptName = "B" });
        vm.Hydrate(ws, null);
        var saveBefore = h.SaveCount;

        await vm.RemoveSelectedScriptCommand.ExecuteAsync(null);

        vm.Scripts.Should().HaveCount(1);
        vm.Scripts[0].ScriptName.Should().Be("B");
        ws.Package.Scripts.Should().HaveCount(1);
        ws.Package.Scripts[0].ScriptName.Should().Be("B");
        vm.SelectedScript.Should().BeSameAs(vm.Scripts[0]);
        h.SaveCount.Should().Be(saveBefore + 1);
    }

    [Test]
    public void RemoveSelectedScript_NoSelection_CanExecuteFalse()
    {
        var vm = new Harness().Build();
        vm.Hydrate(WorkspaceWith(), null);

        vm.RemoveSelectedScriptCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public async Task Hydrate_BuildsConnectionStringAndPassesToChildren()
    {
        var h = new Harness();
        h.Explorer.EnqueuePreviewResult(new PreviewResult(
            new[] { "x" },
            Array.Empty<string?[]>(),
            Truncated: false));

        var vm = h.Build();
        var ws = WorkspaceWith(new SourceForScript { ScriptName = "S" });
        vm.Hydrate(ws, null);

        vm.SelectedScript!.SeedQuery = "SELECT 1";
        await vm.SelectedScript.RunPreviewCommand.ExecuteAsync(null);

        h.Explorer.PreviewCalls.Should().ContainSingle();
        h.Explorer.PreviewCalls[0].ConnectionString.Should().Contain("h").And.Contain("d",
            "the connection string built from WorkspaceSource must reach the child VM");
    }
}
