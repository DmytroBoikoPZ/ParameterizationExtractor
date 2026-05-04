using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.Desktop.Fakes;

[TestFixture]
public class FakeUiDispatcherTests
{
    [Test]
    public async Task InvokeAsync_Action_RunsInline()
    {
        var sut = new FakeUiDispatcher();
        var captured = 0;

        await sut.InvokeAsync(() => captured = 42);

        captured.Should().Be(42, "the fake must run actions synchronously / inline");
    }

    [Test]
    public async Task InvokeAsync_Func_ReturnsResult()
    {
        var sut = new FakeUiDispatcher();

        var result = await sut.InvokeAsync(() => 42);

        result.Should().Be(42);
    }

    [Test]
    public void CheckAccess_AlwaysReturnsTrue()
    {
        var sut = new FakeUiDispatcher();

        sut.CheckAccess().Should().BeTrue();
    }

    [Test]
    public async Task Records_CallCounts()
    {
        var sut = new FakeUiDispatcher();

        await sut.InvokeAsync(() => { });
        await sut.InvokeAsync(() => 1);

        sut.ActionInvocations.Should().Be(1);
        sut.FuncInvocations.Should().Be(1);
        sut.TotalInvocations.Should().Be(2);
    }
}
