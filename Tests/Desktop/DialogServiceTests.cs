using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop;
using Quipu.ParameterizationExtractor.Desktop.Services.Dialogs;

namespace Tests.Desktop;

[TestFixture]
public class DialogServiceTests
{
    [Test]
    public void DesktopHost_ResolvesIDialogService()
    {
        var builder = DesktopHost.CreateApplicationBuilder();

        using var host = builder.Build();
        var dialog = host.Services.GetRequiredService<IDialogService>();

        dialog.Should().NotBeNull();
        dialog.Should().BeOfType<DialogService>();
    }
}
