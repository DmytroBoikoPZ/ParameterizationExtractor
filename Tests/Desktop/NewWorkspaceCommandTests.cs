#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop;
using Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Extras;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Desktop.Views.Overview;
using Quipu.ParameterizationExtractor.Desktop.Views.Seed;
using Quipu.ParameterizationExtractor.Desktop.Views.Welcome;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class NewWorkspaceCommandTests
{
    private FakeDialogService _dialog = null!;
    private InMemoryRecentFilesStore _recents = null!;
    private InMemoryPasswordProtector _protector = null!;
    private FakeWorkspaceStore _store = null!;

    [SetUp]
    public void Init()
    {
        _dialog = new FakeDialogService();
        _recents = new InMemoryRecentFilesStore();
        _protector = new InMemoryPasswordProtector();
        _store = new FakeWorkspaceStore();
    }

    private (MainWindowViewModel main, WelcomeViewModel welcome) NewVms()
    {
        var welcome = new WelcomeViewModel(_dialog, _recents);
        var seed = new SeedViewModel(
            new FakeDatabaseExplorer(),
            new FakeUiDispatcher(),
            _dialog,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var graph = new GraphViewModel(
            new FakeGraphBuilder(),
            new FakeUiDispatcher(),
            _dialog,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var extras = new ExtrasViewModel(
            graph,
            new FakeDatabaseExplorer(),
            _dialog,
            new FakeUiDispatcher(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var main = new MainWindowViewModel(
            _store,
            new OverviewViewModel(),
            welcome,
            seed,
            graph,
            extras,
            _dialog,
            _recents,
            _protector,
            NullLogger<MainWindowViewModel>.Instance);
        return (main, welcome);
    }

    [Test]
    public async Task WelcomeViewModel_NewWorkspaceCommand_DialogReturnsResult_BubblesPathToOpenHandler()
    {
        var workspacePath = "C:\\new-ws.bws";
        _store.Seed(workspacePath, new WorkspaceModel { Name = "new-ws" });
        _dialog.EnqueueNewWorkspaceResponse(new NewWorkspaceResult(workspacePath));
        var (main, welcome) = NewVms();

        await welcome.NewWorkspaceCommand.ExecuteAsync(null);

        main.Workspace.Should().NotBeNull();
        main.Workspace!.Name.Should().Be("new-ws");
    }

    [Test]
    public async Task WelcomeViewModel_NewWorkspaceCommand_DialogReturnsNull_DoesNotCallHandler()
    {
        _dialog.EnqueueNewWorkspaceResponse(null);
        var (main, welcome) = NewVms();

        await welcome.NewWorkspaceCommand.ExecuteAsync(null);

        main.Workspace.Should().BeNull();
    }

    [Test]
    public async Task MainWindowViewModel_NewWorkspaceCommand_DialogReturnsResult_OpensWorkspace()
    {
        var workspacePath = "C:\\fresh.bws";
        _store.Seed(workspacePath, new WorkspaceModel { Name = "fresh" });
        _dialog.EnqueueNewWorkspaceResponse(new NewWorkspaceResult(workspacePath));
        var (main, _) = NewVms();

        await main.NewWorkspaceCommand.ExecuteAsync(null);

        main.Workspace.Should().NotBeNull();
        main.Workspace!.Name.Should().Be("fresh");
        var entries = await _recents.GetAsync();
        entries.Should().ContainSingle().Which.Path.Should().Be(Path.GetFullPath(workspacePath));
    }

    [Test]
    public async Task MainWindowViewModel_NewWorkspaceCommand_DialogReturnsNull_LeavesWorkspaceUnchanged()
    {
        _dialog.EnqueueNewWorkspaceResponse(null);
        var (main, _) = NewVms();

        await main.NewWorkspaceCommand.ExecuteAsync(null);

        main.Workspace.Should().BeNull();
    }
}
