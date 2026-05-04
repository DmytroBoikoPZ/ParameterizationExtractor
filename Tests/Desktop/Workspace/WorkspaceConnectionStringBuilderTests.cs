#nullable enable
using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;

namespace Tests.Desktop.Workspace;

[TestFixture]
public class WorkspaceConnectionStringBuilderTests
{
    [Test]
    public void Build_WindowsAuth_IncludesIntegratedSecurity_ExcludesPasswordToken()
    {
        var source = new WorkspaceSource { Server = "h", Database = "d", Auth = "windows" };

        var s = WorkspaceConnectionStringBuilder.Build(source, plaintextPassword: null);

        s.Should().Contain("Integrated Security=True");
        s.Should().NotContain("Password=");
        s.Should().NotContain("User ID=");
    }

    [Test]
    public void Build_SqlAuth_IncludesUserAndPasswordTokens()
    {
        var source = new WorkspaceSource { Server = "h", Database = "d", Auth = "sql", User = "alice" };

        var s = WorkspaceConnectionStringBuilder.Build(source, plaintextPassword: "secret");

        s.Should().Contain("User ID=alice");
        s.Should().Contain("Password=secret");
        s.Should().NotContain("Integrated Security=True");
    }

    [Test]
    public void Build_NullPassword_TreatedAsEmpty()
    {
        var source = new WorkspaceSource { Server = "h", Database = "d", Auth = "sql", User = "alice" };

        var act = () => WorkspaceConnectionStringBuilder.Build(source, plaintextPassword: null);

        act.Should().NotThrow();
    }
}
