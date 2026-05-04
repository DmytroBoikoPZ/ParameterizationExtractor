#nullable enable
using System.Threading.Tasks;
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
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class EditConnectionFlowTests
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

    private MainWindowViewModel NewVm()
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
        return new MainWindowViewModel(
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
    }

    [Test]
    public void EditConnectionCommand_NoWorkspace_CanExecuteFalse()
    {
        var vm = NewVm();

        vm.EditConnectionCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public async Task EditConnectionCommand_DialogReturnsUpdated_WorkspaceSourceReplacedAndSaved()
    {
        var path = "C:\\edit.bws";
        _store.Seed(path, new WorkspaceModel
        {
            Name = "edit-target",
            Source = new WorkspaceSource { Server = "old.host", Database = "DB", Auth = "windows" },
        });
        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);
        _store.SaveCalls.Clear();

        var updated = new WorkspaceSource { Server = "new.host", Database = "DB", Auth = "windows" };
        _dialog.EnqueueEditConnectionResponse(updated);

        await vm.EditConnectionCommand.ExecuteAsync(null);

        vm.Workspace.Should().NotBeNull();
        vm.Workspace!.Source.Server.Should().Be("new.host");
        _store.SaveCalls.Should().ContainSingle();
        _store.SaveCalls[0].Path.Should().Be(path);
        _store.SaveCalls[0].Workspace.Source.Server.Should().Be("new.host");
    }

    [Test]
    public async Task EditConnectionCommand_DialogReturnsNull_NoSaveCalled()
    {
        var path = "C:\\edit.bws";
        _store.Seed(path, new WorkspaceModel
        {
            Name = "noop",
            Source = new WorkspaceSource { Server = "host", Database = "DB", Auth = "windows" },
        });
        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);
        _store.SaveCalls.Clear();
        _dialog.EnqueueEditConnectionResponse(null);

        await vm.EditConnectionCommand.ExecuteAsync(null);

        _store.SaveCalls.Should().BeEmpty();
        vm.Workspace!.Source.Server.Should().Be("host");
    }

    [Test]
    public async Task EditConnectionCommand_StoredPasswordPresent_DecryptsBeforeOpeningDialog()
    {
        var path = "C:\\edit.bws";
        _store.Seed(path, new WorkspaceModel
        {
            Name = "with-creds",
            Source = new WorkspaceSource
            {
                Server = "h", Database = "d", Auth = "sql",
                User = "sa",
                PasswordEncrypted = _protector.Protect("hunter2"),
            },
        });
        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);
        _dialog.EnqueueEditConnectionResponse(null);

        await vm.EditConnectionCommand.ExecuteAsync(null);

        _dialog.EditConnectionCalls.Should().ContainSingle();
        _dialog.EditConnectionCalls[0].PlaintextPassword.Should().Be("hunter2");
    }

    [Test]
    public async Task EditConnectionCommand_PasswordIsCorrupt_PassesNullPlaintextToDialog()
    {
        var path = "C:\\edit.bws";
        _store.Seed(path, new WorkspaceModel
        {
            Name = "bad-creds",
            Source = new WorkspaceSource
            {
                Server = "h", Database = "d", Auth = "sql",
                User = "sa",
                PasswordEncrypted = "not!base64!at!all",
            },
        });
        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);
        _dialog.EnqueueEditConnectionResponse(null);

        await vm.EditConnectionCommand.ExecuteAsync(null);

        _dialog.EditConnectionCalls.Should().ContainSingle();
        _dialog.EditConnectionCalls[0].PlaintextPassword.Should().BeNull();
    }
}
