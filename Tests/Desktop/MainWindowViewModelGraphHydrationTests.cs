#nullable enable
using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Extras;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Desktop.Views.Overview;
using Quipu.ParameterizationExtractor.Desktop.Views.Seed;
using Quipu.ParameterizationExtractor.Desktop.Views.Welcome;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class MainWindowViewModelGraphHydrationTests
{
    private FakeDialogService _dialog = null!;
    private InMemoryRecentFilesStore _recents = null!;
    private InMemoryPasswordProtector _protector = null!;
    private FakeWorkspaceStore _store = null!;
    private FakeGraphBuilder _builder = null!;
    private GraphViewModel _graph = null!;

    [SetUp]
    public void Init()
    {
        _dialog = new FakeDialogService();
        _recents = new InMemoryRecentFilesStore();
        _protector = new InMemoryPasswordProtector();
        _store = new FakeWorkspaceStore();
        _builder = new FakeGraphBuilder();
        _graph = new GraphViewModel(_builder, new FakeUiDispatcher(), _dialog, NullLoggerFactory.Instance);
    }

    private MainWindowViewModel NewVm()
    {
        var welcome = new WelcomeViewModel(_dialog, _recents);
        var seed = new SeedViewModel(
            new FakeDatabaseExplorer(),
            new FakeUiDispatcher(),
            _dialog,
            NullLoggerFactory.Instance);
        var extras = new ExtrasViewModel(
            _graph,
            new FakeDatabaseExplorer(),
            _dialog,
            new FakeUiDispatcher(),
            NullLoggerFactory.Instance);
        return new MainWindowViewModel(
            _store,
            new OverviewViewModel(),
            welcome,
            seed,
            _graph,
            extras,
            _dialog,
            _recents,
            _protector,
            NullLogger<MainWindowViewModel>.Instance);
    }

    private static SourceForScript ScriptWithRoot(string name, string tableName)
    {
        var s = new SourceForScript { ScriptName = name };
        s.RootRecords.Add(new RecordsToExtract { TableName = tableName, Where = "1=1" });
        return s;
    }

    [Test]
    public async System.Threading.Tasks.Task OnWorkspaceChanged_HydratesGraphWithDecryptedPassword()
    {
        var path = "C:\\graph-hydrate.bws";
        var ws = new WorkspaceModel
        {
            Name = "graph-hydrate",
            Source = new WorkspaceSource
            {
                Server = "h", Database = "d", Auth = "sql", User = "alice",
                PasswordEncrypted = _protector.Protect("hunter2"),
            },
        };
        ws.Package.Scripts.Add(ScriptWithRoot("s", "Patient"));
        _store.Seed(path, ws);
        _builder.EnqueueResult(new ReachableGraph(Array.Empty<GraphNode>(), Array.Empty<GraphEdge>()));

        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);

        _builder.ConnectionStringsCalled.Should().NotBeEmpty();
        _builder.ConnectionStringsCalled[0].Should().Contain("Password=hunter2");
    }

    [Test]
    public async System.Threading.Tasks.Task OnWorkspaceChanged_DecryptFailure_PassesNullPlaintextToGraph()
    {
        var path = "C:\\graph-decrypt-fail.bws";
        var ws = new WorkspaceModel
        {
            Name = "graph-decrypt-fail",
            Source = new WorkspaceSource
            {
                Server = "h", Database = "d", Auth = "sql", User = "alice",
                PasswordEncrypted = "this-is-not-base64!!!",
            },
        };
        ws.Package.Scripts.Add(ScriptWithRoot("s", "Patient"));
        _store.Seed(path, ws);
        _builder.EnqueueResult(new ReachableGraph(Array.Empty<GraphNode>(), Array.Empty<GraphEdge>()));

        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);

        _builder.ConnectionStringsCalled.Should().NotBeEmpty();
        _builder.ConnectionStringsCalled[0].Should().NotContain("Password=this-is-not-base64",
            "decrypt failure must surface as null plaintext, not raw base64");
    }

    [Test]
    public async System.Threading.Tasks.Task OnWorkspaceChanged_NullWorkspace_ClearsGraph()
    {
        var path = "C:\\graph-clear.bws";
        var ws = new WorkspaceModel
        {
            Name = "clear",
            Source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" },
        };
        ws.Package.Scripts.Add(ScriptWithRoot("s", "Patient"));
        _store.Seed(path, ws);
        _builder.EnqueueResult(new ReachableGraph(
            new[] { new GraphNode("dbo", "Patient") }, Array.Empty<GraphEdge>()));

        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);
        _graph.Reachable.Should().NotBeNull();

        vm.CloseCommand.Execute(null);

        _graph.Reachable.Should().BeNull();
        _graph.AllNodes.Should().BeEmpty();
    }

    [Test]
    public async System.Threading.Tasks.Task OnWorkspaceChanged_GraphBuilderThrows_StatusFailedNoCrash()
    {
        var path = "C:\\graph-fail.bws";
        var ws = new WorkspaceModel
        {
            Name = "graph-fail",
            Source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" },
        };
        ws.Package.Scripts.Add(ScriptWithRoot("s", "Patient"));
        _store.Seed(path, ws);
        _builder.EnqueueException(new DatabaseExplorerException("login failed"));

        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);

        _graph.Status.Should().StartWith("Failed:");
        _graph.Status.Should().Contain("login failed");
    }

    [Test]
    public async System.Threading.Tasks.Task SeedSelectedScriptChanged_UpdatesGraphAnchor()
    {
        var path = "C:\\seed-anchor-sync.bws";
        var ws = new WorkspaceModel
        {
            Name = "anchor",
            Source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" },
        };
        var s1 = ScriptWithRoot("s1", "Patient");
        var s2 = ScriptWithRoot("s2", "Visit");
        ws.Package.Scripts.Add(s1);
        ws.Package.Scripts.Add(s2);
        _store.Seed(path, ws);
        // Three results: initial Hydrate (which sets Seed.SelectedScript=s1 → SetAnchor(s1) → refresh fires twice)
        // Pragmatic: enqueue plenty.
        for (var i = 0; i < 6; i++)
            _builder.EnqueueResult(new ReachableGraph(Array.Empty<GraphNode>(), Array.Empty<GraphEdge>()));

        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);

        // Switch SelectedScript → Graph anchor follows.
        var seedVm = vm.Seed;
        var seedScript2 = seedVm.Scripts.Single(c => c.ScriptName == "s2");
        seedVm.SelectedScript = seedScript2;

        _graph.AnchorScript.Should().Be(s2);
        _builder.SeedsCalled.Last().Single().Name.Should().Be("Visit");
    }
}
