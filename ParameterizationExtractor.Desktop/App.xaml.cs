using System.Linq;
using System.Windows;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Quipu.ParameterizationExtractor.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // AvalonEdit ships a built-in T-SQL highlighting definition registered as "TSQL".
        // Touching HighlightingManager.Instance ensures the static initialiser ran before any
        // SqlEditorView resolves SyntaxHighlighting="TSQL" at parse time.
        _ = HighlightingManager.Instance.GetDefinition("TSQL");

        _host = DesktopHost.CreateApplicationBuilder(e.Args).Build();
        await _host.StartAsync();

        var vm = _host.Services.GetRequiredService<MainWindowViewModel>();
        var workspacePath = e.Args.FirstOrDefault();
        await vm.InitializeAsync(workspacePath);

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.DataContext = vm;
        window.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }
        base.OnExit(e);
    }
}
