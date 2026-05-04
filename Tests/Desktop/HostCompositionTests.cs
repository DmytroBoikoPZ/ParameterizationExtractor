using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop;
using Quipu.ParameterizationExtractor.Desktop.Controls.ConnectionEditor;
using Quipu.ParameterizationExtractor.Desktop.Controls.TablePicker;
using Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;
using Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles;
using Quipu.ParameterizationExtractor.Desktop.Services.Security;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Extras;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Desktop.Views.Overview;
using Quipu.ParameterizationExtractor.Desktop.Views.Seed;
using Quipu.ParameterizationExtractor.Desktop.Views.Welcome;

namespace Tests.Desktop;

[TestFixture]
public class HostCompositionTests
{
    [Test]
    public void DesktopHost_BuildsAndResolvesMainWindowViewModel()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var viewModel = host.Services.GetRequiredService<MainWindowViewModel>();

        viewModel.Should().NotBeNull();
        viewModel.WindowTitle.Should().Be("SQL Buldozer");
    }

    [Test]
    public void DesktopHost_ResolvesIWorkspaceStore()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var store = host.Services.GetRequiredService<IWorkspaceStore>();

        store.Should().NotBeNull();
        store.Should().BeOfType<JsonWorkspaceStore>();
    }

    [Test]
    public void DesktopHost_ResolvesIRecentFilesStore()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var store = host.Services.GetRequiredService<IRecentFilesStore>();

        store.Should().NotBeNull();
        store.Should().BeOfType<JsonRecentFilesStore>();
    }

    [Test]
    public void DesktopHost_ResolvesIPasswordProtector()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var protector = host.Services.GetRequiredService<IPasswordProtector>();

        protector.Should().NotBeNull();
        protector.Should().BeOfType<DpapiPasswordProtector>();
    }

    [Test]
    public void DesktopHost_ResolvesConnectionEditorViewModel_AsTransient()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var first = host.Services.GetRequiredService<ConnectionEditorViewModel>();
        var second = host.Services.GetRequiredService<ConnectionEditorViewModel>();

        first.Should().NotBeNull();
        first.Should().NotBeSameAs(second, "ConnectionEditorViewModel must be transient — fresh instance per dialog");
    }

    [Test]
    public void DesktopHost_ResolvesTablePickerViewModel_AsTransient()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var first = host.Services.GetRequiredService<TablePickerViewModel>();
        var second = host.Services.GetRequiredService<TablePickerViewModel>();

        first.Should().NotBeNull();
        first.Should().NotBeSameAs(second, "TablePickerViewModel must be transient — fresh instance per consumer");
    }

    [Test]
    public void DesktopHost_ResolvesNewWorkspaceDialogViewModel_AsTransient()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var first = host.Services.GetRequiredService<NewWorkspaceDialogViewModel>();
        var second = host.Services.GetRequiredService<NewWorkspaceDialogViewModel>();

        first.Should().NotBeNull();
        first.Should().NotBeSameAs(second);
    }

    [Test]
    public void DesktopHost_ResolvesNewWorkspaceDialogFactory()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var factory = host.Services.GetRequiredService<System.Func<NewWorkspaceDialog>>();

        factory.Should().NotBeNull();
    }

    [Test]
    public void DesktopHost_ResolvesOverviewViewModel()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var overview = host.Services.GetRequiredService<OverviewViewModel>();

        overview.Should().NotBeNull();
        overview.IsEmpty.Should().BeTrue("default state — no workspace assigned yet");
    }

    [Test]
    public void DesktopHost_ResolvesGraphViewModel()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var graph = host.Services.GetRequiredService<GraphViewModel>();

        graph.Should().NotBeNull();
        graph.AllNodes.Should().BeEmpty();
        graph.LayoutChoice.Should().Be(Quipu.ParameterizationExtractor.Desktop.Controls.GraphHost.GraphLayoutKind.Hierarchical);
    }

    [Test]
    public void DesktopHost_ResolvesExtrasViewModel()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var extras = host.Services.GetRequiredService<ExtrasViewModel>();

        extras.Should().NotBeNull();
        extras.StandaloneTables.Should().BeEmpty();
    }

    [Test]
    public void DesktopHost_ResolvesSeedViewModel()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var seed = host.Services.GetRequiredService<SeedViewModel>();

        seed.Should().NotBeNull();
        seed.Scripts.Should().BeEmpty();
        seed.HasScripts.Should().BeFalse();
    }

    [Test]
    public void DesktopHost_ResolvesWelcomeViewModel()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var welcome = host.Services.GetRequiredService<WelcomeViewModel>();

        welcome.Should().NotBeNull();
        welcome.IsNewWorkspaceEnabled.Should().BeTrue(
            "the New workspace button is live as of desktop-connection-management");
    }

    [Test]
    public void MainWindowViewModel_NoWorkspaceLoaded_TitleIsBranded()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var vm = host.Services.GetRequiredService<MainWindowViewModel>();

        vm.Workspace.Should().BeNull();
        vm.WindowTitle.Should().Be("SQL Buldozer");
        vm.ConnectionDisplay.Should().BeEmpty();
        vm.Overview.IsEmpty.Should().BeTrue();
        vm.IsWorkspaceLoaded.Should().BeFalse();
    }
}
