using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.Desktop.Fakes;

[TestFixture]
public class FakeDialogServiceTests
{
    [Test]
    public async Task Confirm_ReturnsEnqueuedResponse()
    {
        var fake = new FakeDialogService();
        fake.EnqueueConfirmResponse(true);

        var result = await fake.ConfirmAsync("t", "m");

        result.Should().BeTrue();
    }

    [Test]
    public async Task Confirm_RecordsCall()
    {
        var fake = new FakeDialogService();
        fake.EnqueueConfirmResponse(false);

        await fake.ConfirmAsync("Discard?", "You'll lose changes.");

        fake.Calls.Should().HaveCount(1);
        fake.Calls[0].Method.Should().Be("Confirm");
        fake.Calls[0].Title.Should().Be("Discard?");
        fake.Calls[0].Message.Should().Be("You'll lose changes.");
    }

    [Test]
    public async Task OpenFile_ReturnsEnqueuedPath()
    {
        var fake = new FakeDialogService();
        fake.EnqueueOpenFileResponse(@"C:\x.bws");

        var result = await fake.OpenFileAsync("Open", "*.bws|*.bws");

        result.Should().Be(@"C:\x.bws");
    }

    [Test]
    public async Task OpenFile_RecordsCallWithFilter()
    {
        var fake = new FakeDialogService();
        fake.EnqueueOpenFileResponse(null);

        await fake.OpenFileAsync("Open workspace", "Workspace (*.bws)|*.bws");

        fake.Calls.Should().HaveCount(1);
        fake.Calls[0].Method.Should().Be("OpenFile");
        fake.Calls[0].Title.Should().Be("Open workspace");
        fake.Calls[0].Filter.Should().Be("Workspace (*.bws)|*.bws");
    }

    [Test]
    public void Confirm_NoResponseEnqueued_Throws()
    {
        var fake = new FakeDialogService();

        var act = async () => await fake.ConfirmAsync("t", "m");

        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no enqueued confirm response*");
    }
}
