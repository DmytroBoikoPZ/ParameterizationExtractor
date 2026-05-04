#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Views.Welcome;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class WelcomeViewModelTests
{
    private FakeDialogService _dialog = null!;
    private InMemoryRecentFilesStore _recents = null!;
    private List<string> _openCalls = null!;

    [SetUp]
    public void Init()
    {
        _dialog = new FakeDialogService();
        _recents = new InMemoryRecentFilesStore();
        _openCalls = new List<string>();
    }

    private WelcomeViewModel NewBoundVm()
    {
        var vm = new WelcomeViewModel(_dialog, _recents);
        vm.Bind(p => { _openCalls.Add(p); return Task.CompletedTask; });
        return vm;
    }

    [Test]
    public async Task Initial_HasNoRecents_HasRecentsFalse()
    {
        var vm = NewBoundVm();

        await vm.RefreshAsync();

        vm.HasRecents.Should().BeFalse();
        vm.Recents.Should().BeEmpty();
    }

    [Test]
    public async Task RefreshAsync_PopulatesRecentsCollection()
    {
        await _recents.PushAsync(@"C:\a.bws");
        await _recents.PushAsync(@"C:\b.bws");
        var vm = NewBoundVm();

        await vm.RefreshAsync();

        vm.HasRecents.Should().BeTrue();
        vm.Recents.Should().HaveCount(2);
        vm.Recents[0].Path.Should().Be(Path.GetFullPath(@"C:\b.bws"));
    }

    [Test]
    public async Task OpenCommand_DialogReturnsPath_InvokesOpenHandler()
    {
        _dialog.EnqueueOpenFileResponse(@"C:\x.bws");
        var vm = NewBoundVm();

        await vm.OpenCommand.ExecuteAsync(null);

        _openCalls.Should().ContainSingle().Which.Should().Be(@"C:\x.bws");
    }

    [Test]
    public async Task OpenCommand_DialogReturnsNull_DoesNotInvokeOpenHandler()
    {
        _dialog.EnqueueOpenFileResponse(null);
        var vm = NewBoundVm();

        await vm.OpenCommand.ExecuteAsync(null);

        _openCalls.Should().BeEmpty();
    }

    [Test]
    public async Task OpenRecentCommand_PassesPathToHandler()
    {
        var vm = NewBoundVm();

        await vm.OpenRecentCommand.ExecuteAsync(@"C:\y.bws");

        _openCalls.Should().ContainSingle().Which.Should().Be(@"C:\y.bws");
    }

    [Test]
    public void IsNewWorkspaceEnabled_True_AfterDesktopConnectionManagement()
    {
        var vm = NewBoundVm();

        vm.IsNewWorkspaceEnabled.Should().BeTrue(
            "the New workspace button is live as of desktop-connection-management");
    }

    [Test]
    public async Task OpenCommand_NotBound_Throws()
    {
        _dialog.EnqueueOpenFileResponse(@"C:\x.bws");
        var unbound = new WelcomeViewModel(_dialog, _recents);

        Func<Task> act = () => unbound.OpenCommand.ExecuteAsync(null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Bind must be called*");
    }
}
