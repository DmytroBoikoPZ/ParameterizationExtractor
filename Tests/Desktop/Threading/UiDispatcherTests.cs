using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop;
using Quipu.ParameterizationExtractor.Desktop.Services.Threading;

namespace Tests.Desktop.Threading;

[TestFixture]
public class UiDispatcherTests
{
    [Test]
    public void DesktopHost_ResolvesIUiDispatcher()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var dispatcher = host.Services.GetRequiredService<IUiDispatcher>();

        dispatcher.Should().NotBeNull();
        dispatcher.Should().BeOfType<UiDispatcher>();
    }

    [Test]
    public void DesktopHost_IUiDispatcherIsSingleton()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var first = host.Services.GetRequiredService<IUiDispatcher>();
        var second = host.Services.GetRequiredService<IUiDispatcher>();

        first.Should().BeSameAs(second, "IUiDispatcher must be Singleton per the recipe");
    }
}
