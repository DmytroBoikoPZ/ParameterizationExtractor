using System;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quipu.ParameterizationExtractor.Desktop.Controls.ConnectionEditor;
using Quipu.ParameterizationExtractor.Desktop.Controls.TablePicker;
using Quipu.ParameterizationExtractor.Desktop.Dialogs.AddStandaloneTable;
using Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;
using Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;
using Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles;
using Quipu.ParameterizationExtractor.Desktop.Services.Security;
using Quipu.ParameterizationExtractor.Desktop.Services.Threading;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Extras;
using Quipu.ParameterizationExtractor.Desktop.Views.Graph;
using Quipu.ParameterizationExtractor.Desktop.Views.Overview;
using Quipu.ParameterizationExtractor.Desktop.Views.Seed;
using Quipu.ParameterizationExtractor.Desktop.Views.Welcome;
using Quipu.ParameterizationExtractor.Logic.Connectivity;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Serilog;

namespace Quipu.ParameterizationExtractor.Desktop;

internal static class DesktopHost
{
    public static HostApplicationBuilder CreateApplicationBuilder(string[]? args = null)
    {
        var builder = Host.CreateApplicationBuilder(args ?? []);

        builder.Services.AddSerilog((sp, lc) => lc.ReadFrom.Configuration(builder.Configuration));

        builder.Services.AddSingleton<IWorkspaceStore, JsonWorkspaceStore>();

        builder.Services.AddSingleton<IDialogCoordinator>(_ => DialogCoordinator.Instance);
        builder.Services.AddSingleton<IDialogService, DialogService>();

        builder.Services.AddSingleton<IRecentFilesStore, JsonRecentFilesStore>();

        builder.Services.AddSingleton<IConnectionTester, MSSQLConnectionTester>();
        builder.Services.AddSingleton<IDatabaseExplorer, MSSqlDatabaseExplorer>();
        builder.Services.AddSingleton<IGraphBuilder, MSSqlGraphBuilder>();
        builder.Services.AddSingleton<IPasswordProtector, DpapiPasswordProtector>();
        builder.Services.AddSingleton<IUiDispatcher, UiDispatcher>();

        builder.Services.AddSingleton<ExtrasViewModel>();
        builder.Services.AddSingleton<GraphViewModel>();
        builder.Services.AddSingleton<OverviewViewModel>();
        builder.Services.AddSingleton<SeedViewModel>();
        builder.Services.AddSingleton<WelcomeViewModel>();
        builder.Services.AddTransient<ConnectionEditorViewModel>();
        builder.Services.AddTransient<TablePickerViewModel>();
        builder.Services.AddTransient<NewWorkspaceDialogViewModel>();
        builder.Services.AddTransient<NewWorkspaceDialog>();
        builder.Services.AddSingleton<Func<NewWorkspaceDialog>>(
            sp => () => sp.GetRequiredService<NewWorkspaceDialog>());

        builder.Services.AddTransient<AddStandaloneTableDialogViewModel>();
        builder.Services.AddTransient<AddStandaloneTableDialog>();
        builder.Services.AddSingleton<Func<AddStandaloneTableDialog>>(
            sp => () => sp.GetRequiredService<AddStandaloneTableDialog>());

        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<MainWindowViewModel>();

        return builder;
    }
}
