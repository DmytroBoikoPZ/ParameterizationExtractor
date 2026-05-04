#nullable enable
using System;
using System.IO;
using System.Threading;
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
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class MainWindowViewModelSeedHydrationTests
{
    private FakeDialogService _dialog = null!;
    private InMemoryRecentFilesStore _recents = null!;
    private InMemoryPasswordProtector _protector = null!;
    private FakeWorkspaceStore _store = null!;
    private FakeDatabaseExplorer _explorer = null!;
    private SeedViewModel _seed = null!;

    [SetUp]
    public void Init()
    {
        _dialog = new FakeDialogService();
        _recents = new InMemoryRecentFilesStore();
        _protector = new InMemoryPasswordProtector();
        _store = new FakeWorkspaceStore();
        _explorer = new FakeDatabaseExplorer();
        // Pre-enqueue an empty list so the Hydrate-triggered RefreshTablesAsync doesn't throw.
        _explorer.EnqueueListResult(System.Array.Empty<TableRef>());
        _explorer.EnqueueListResult(System.Array.Empty<TableRef>());
        _explorer.EnqueueListResult(System.Array.Empty<TableRef>());
        _seed = new SeedViewModel(
            _explorer,
            new FakeUiDispatcher(),
            _dialog,
            NullLoggerFactory.Instance);
    }

    private MainWindowViewModel NewVm()
    {
        var welcome = new WelcomeViewModel(_dialog, _recents);
        var graph = new GraphViewModel(
            new FakeGraphBuilder(),
            new FakeUiDispatcher(),
            _dialog,
            NullLoggerFactory.Instance);
        var extras = new ExtrasViewModel(
            graph,
            new FakeDatabaseExplorer(),
            _dialog,
            new FakeUiDispatcher(),
            NullLoggerFactory.Instance);
        return new MainWindowViewModel(
            _store,
            new OverviewViewModel(),
            welcome,
            _seed,
            graph,
            extras,
            _dialog,
            _recents,
            _protector,
            NullLogger<MainWindowViewModel>.Instance);
    }

    [Test]
    public async Task OnWorkspaceChanged_HydratesSeedWithDecryptedPassword()
    {
        var path = "C:\\hydrate.bws";
        var ws = new WorkspaceModel
        {
            Name = "hydrate",
            Source = new WorkspaceSource
            {
                Server = "h", Database = "d", Auth = "sql", User = "alice",
                PasswordEncrypted = _protector.Protect("hunter2"),
            },
        };
        ws.Package.Scripts.Add(new SourceForScript { ScriptName = "First" });
        _store.Seed(path, ws);

        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);

        _seed.Scripts.Should().ContainSingle();
        _explorer.ListConnectionStringsCalled.Should().NotBeEmpty();
        _explorer.ListConnectionStringsCalled[0].Should().Contain("Password=hunter2",
            "the decrypted plaintext must reach the engine via the workspace connection string");
    }

    [Test]
    public async Task OnWorkspaceChanged_DecryptFailure_PassesNullPlaintextToSeed()
    {
        var path = "C:\\decrypt-fail.bws";
        var ws = new WorkspaceModel
        {
            Name = "decrypt-fail",
            Source = new WorkspaceSource
            {
                Server = "h", Database = "d", Auth = "sql", User = "alice",
                PasswordEncrypted = "this-is-not-base64!!!",
            },
        };
        ws.Package.Scripts.Add(new SourceForScript { ScriptName = "First" });
        _store.Seed(path, ws);

        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);

        _seed.Scripts.Should().ContainSingle();
        _explorer.ListConnectionStringsCalled.Should().NotBeEmpty();
        _explorer.ListConnectionStringsCalled[0].Should().NotContain("Password=this-is-not-base64",
            "decrypt failure must surface as null plaintext, not raw base64");
    }

    [Test]
    public async Task OnWorkspaceChanged_NullWorkspace_ClearsSeed()
    {
        var path = "C:\\clear.bws";
        var ws = new WorkspaceModel
        {
            Name = "clear",
            Source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" },
        };
        ws.Package.Scripts.Add(new SourceForScript { ScriptName = "X" });
        _store.Seed(path, ws);

        var vm = NewVm();
        await vm.OpenWorkspaceAsync(path);
        _seed.Scripts.Should().HaveCount(1);

        vm.CloseCommand.Execute(null);

        _seed.Scripts.Should().BeEmpty();
        _seed.SelectedScript.Should().BeNull();
    }

    [Test]
    public async Task SaveWorkspaceAsync_OnIOException_ShowsDialog()
    {
        var path = "C:\\save-fail.bws";
        var ws = new WorkspaceModel
        {
            Name = "save-fail",
            Source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" },
        };
        _store.Seed(path, ws);

        var failingStore = new ThrowingWorkspaceStore(_store, new IOException("disk full"));
        var welcome = new WelcomeViewModel(_dialog, _recents);
        var graph = new GraphViewModel(
            new FakeGraphBuilder(),
            new FakeUiDispatcher(),
            _dialog,
            NullLoggerFactory.Instance);
        var extras2 = new ExtrasViewModel(
            graph,
            new FakeDatabaseExplorer(),
            _dialog,
            new FakeUiDispatcher(),
            NullLoggerFactory.Instance);
        var vm = new MainWindowViewModel(
            failingStore,
            new OverviewViewModel(),
            welcome,
            _seed,
            graph,
            extras2,
            _dialog,
            _recents,
            _protector,
            NullLogger<MainWindowViewModel>.Instance);
        await vm.OpenWorkspaceAsync(path);

        await _seed.AddScriptCommand.ExecuteAsync(null);

        _dialog.Calls.Should().Contain(c => c.Method == "ShowMessage" && c.Title == "Save failed");
    }

    /// <summary>Wraps a store and throws on save; used to exercise the dialog branch.</summary>
    private sealed class ThrowingWorkspaceStore : IWorkspaceStore
    {
        private readonly IWorkspaceStore _inner;
        private readonly Exception _saveException;

        public ThrowingWorkspaceStore(IWorkspaceStore inner, Exception saveException)
        {
            _inner = inner;
            _saveException = saveException;
        }

        public Task<WorkspaceModel> LoadAsync(string path, CancellationToken ct = default) =>
            _inner.LoadAsync(path, ct);

        public Task SaveAsync(WorkspaceModel workspace, string path, CancellationToken ct = default) =>
            throw _saveException;
    }
}
