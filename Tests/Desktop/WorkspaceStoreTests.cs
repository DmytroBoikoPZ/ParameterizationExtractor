using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.Desktop;

[TestFixture]
public class WorkspaceStoreTests
{
    private string _tempDir = null!;

    [SetUp]
    public void CreateTempDir()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "buldozer-tests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void DeleteTempDir()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task RoundTrip_PreservesAllFields()
    {
        var sut = new JsonWorkspaceStore();

        var original = new WorkspaceModel
        {
            Version = 1,
            Name = "round-trip-test",
            Source = new WorkspaceSource
            {
                Server = "127.0.0.1,1433",
                Database = "TestDb",
                Auth = "sql",
                User = "sa",
                PasswordEncrypted = null
            },
            Global = new GlobalExtractConfiguration
            {
                DefaultExtractStrategy = new OnlyOneTableExtractStrategy(),
                DefaultSqlBuildStrategy = new SqlBuildStrategy()
            },
            Package = new Package()
        };

        var path = Path.Combine(_tempDir, "round-trip.bws");
        await sut.SaveAsync(original, path);

        File.Exists(path).Should().BeTrue();
        var raw = await File.ReadAllTextAsync(path);
        raw.Should().Contain("\"$version\"", "the version field must be wire-named '$version'");

        var loaded = await sut.LoadAsync(path);

        loaded.Should().BeEquivalentTo(original, opts => opts
            .RespectingRuntimeTypes()
            .Excluding(p => p.Path.EndsWith("FieldsToExclude"))
            .Excluding(p => p.Path.EndsWith("DependencyToExclude"))
            .Excluding(p => p.Path.EndsWith("UniqueColumns"))
            .Excluding(p => p.Path.EndsWith("UniqueColums")));
    }

    [Test]
    public async Task LoadsSampleBws_ProducesEngineReadablePackageAndGlobal()
    {
        var sut = new JsonWorkspaceStore();
        var path = Path.Combine(AppContext.BaseDirectory, "Examples", "sample.bws");

        File.Exists(path).Should().BeTrue($"sample workspace must be copied to test output at {path}");

        var workspace = await sut.LoadAsync(path);

        workspace.Version.Should().Be(1);
        workspace.Name.Should().Be("sample-clearing");

        workspace.Source.Auth.Should().Be("sql");
        workspace.Source.PasswordEncrypted.Should().BeNull();

        workspace.Global.Should().NotBeNull();
        workspace.Global.DefaultExtractStrategy.Should().BeOfType<FKDependencyExtractStrategy>();

        workspace.Package.Scripts.Should().HaveCount(1);
        workspace.Package.Scripts[0].ScriptName.Should().Be("PatientClearing");
        workspace.Package.Scripts[0].TablesToProcess.Should().HaveCount(2);
    }

    [Test]
    public void Load_RejectsUnknownVersion()
    {
        var path = Path.Combine(_tempDir, "v2.bws");
        File.WriteAllText(path, """
        {
          "$version": 2,
          "name": "from-future",
          "source": { "server": "", "database": "", "auth": "windows" },
          "global": {},
          "package": { "scripts": [] }
        }
        """);

        var sut = new JsonWorkspaceStore();

        var act = () => sut.LoadAsync(path);

        act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*$version=2*");
    }

    [Test]
    public void Load_RejectsInvalidAuthValue()
    {
        var path = Path.Combine(_tempDir, "bad-auth.bws");
        File.WriteAllText(path, """
        {
          "$version": 1,
          "name": "bad-auth",
          "source": { "server": "", "database": "", "auth": "kerberos" },
          "global": {},
          "package": { "scripts": [] }
        }
        """);

        var sut = new JsonWorkspaceStore();

        var act = () => sut.LoadAsync(path);

        act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*kerberos*");
    }

    [Test]
    public async Task PasswordEncryptedField_RoundTripsAsNull_PlaceholderSemantics()
    {
        // Step 04 deliberately ships the password slot as a placeholder — desktop-connection-management
        // owns the DPAPI flow. This test pins the placeholder behaviour so a future change that
        // accidentally requires/encrypts the password trips a regression.
        var sut = new JsonWorkspaceStore();

        var original = new WorkspaceModel
        {
            Name = "no-password",
            Source = new WorkspaceSource
            {
                Server = "127.0.0.1",
                Database = "Db",
                Auth = "sql",
                User = "sa",
                PasswordEncrypted = null
            }
        };

        var path = Path.Combine(_tempDir, "no-password.bws");
        await sut.SaveAsync(original, path);

        var loaded = await sut.LoadAsync(path);

        loaded.Source.PasswordEncrypted.Should().BeNull();
    }
}
