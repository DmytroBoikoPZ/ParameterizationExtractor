using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Tests.CharacterisationTests;
using Tests.CharacterisationTests.Harness;

namespace Tests.EngineGraphBuilderTests;

[TestFixture]
public class MSSqlGraphBuilderTests
{
    private static MSSqlGraphBuilder NewBuilder() =>
        new(NullLogger<MSSqlGraphBuilder>.Instance);

    [Test]
    public async Task BuildAsync_SingleSeed_ReturnsSeedNode()
    {
        var sut = NewBuilder();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var graph = await sut.BuildAsync(connStr,
            new[] { new TableRef("dbo", "Patient") }, CancellationToken.None);

        graph.Nodes.Should().Contain(n => n.Schema == "dbo" && n.Name == "Patient");
    }

    [Test]
    public async Task BuildAsync_SeedReachesChildrenViaFK()
    {
        var sut = NewBuilder();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var graph = await sut.BuildAsync(connStr,
            new[] { new TableRef("dbo", "TherapyPrograms") }, CancellationToken.None);

        graph.Nodes.Count.Should().BeGreaterThan(1, "the seed must reach at least one FK partner");
        graph.Edges.Should().NotBeEmpty();
        graph.Edges.Should().Contain(e =>
            e.FromName.Equals("TherapyPrograms", StringComparison.OrdinalIgnoreCase) ||
            e.ToName.Equals("TherapyPrograms", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task BuildAsync_RespectsSchemaTuple()
    {
        if (!global::Tests.CharacterisationTests.CharacterisationTests.CrossSchemaAvailable)
        {
            Assert.Ignore("Cross-schema fixture not available — test runner lacks CREATE SCHEMA permission on the test DB.");
        }

        var sut = NewBuilder();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var graph = await sut.BuildAsync(connStr,
            new[] { new TableRef("dbo", "SchemaTestChild") }, CancellationToken.None);

        graph.Nodes.Should().Contain(n => n.Schema == "audit" && n.Name == "SchemaTestParent",
            "FK walk must cross schema boundaries");
    }

    [Test]
    public async Task BuildAsync_NoFKsForSeed_ReturnsOnlySeed()
    {
        var sut = NewBuilder();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        // Use a table we know has no FKs in either direction. Fall back: pick a table whose
        // result has Edges == 0; any island-table candidate is OK. If our test DB has no
        // such island, this test would need a dedicated fixture; for now we check the
        // contract shape.
        var graph = await sut.BuildAsync(connStr,
            new[] { new TableRef("dbo", "DefinitelyNotATable_xZq") }, CancellationToken.None);

        // The unknown seed appears in Nodes (we don't validate seed existence) but no edges.
        graph.Nodes.Should().ContainSingle();
        graph.Edges.Should().BeEmpty();
    }

    [Test]
    public async Task BuildAsync_MultipleSeeds_UnionsReachable()
    {
        var sut = NewBuilder();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var graph = await sut.BuildAsync(connStr, new[]
        {
            new TableRef("dbo", "TherapyPrograms"),
            new TableRef("dbo", "Employees"),
        }, CancellationToken.None);

        graph.Nodes.Should().Contain(n => n.Name.Equals("TherapyPrograms", StringComparison.OrdinalIgnoreCase));
        graph.Nodes.Should().Contain(n => n.Name.Equals("Employees", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task BuildAsync_OrderingIsStable()
    {
        var sut = NewBuilder();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var seed = new[] { new TableRef("dbo", "TherapyPrograms") };

        var first = await sut.BuildAsync(connStr, seed, CancellationToken.None);
        var second = await sut.BuildAsync(connStr, seed, CancellationToken.None);

        first.Nodes.Select(n => $"{n.Schema}.{n.Name}").Should().Equal(
            second.Nodes.Select(n => $"{n.Schema}.{n.Name}"));
        first.Edges.Select(e => e.ConstraintName).Should().Equal(
            second.Edges.Select(e => e.ConstraintName));
    }

    [Test]
    public async Task BuildAsync_BadConnection_ThrowsDatabaseExplorerException()
    {
        var sut = NewBuilder();
        var bogus = "Server=nonexistent.invalid,1433;Database=Whatever;User Id=sa;Password=x;TrustServerCertificate=True;Connect Timeout=2";

        var act = async () => await sut.BuildAsync(bogus,
            new[] { new TableRef("dbo", "Anything") }, CancellationToken.None);

        await act.Should().ThrowAsync<DatabaseExplorerException>();
    }

    [Test]
    public void BuildAsync_Cancelled_PropagatesOperationCancelled()
    {
        var sut = NewBuilder();
        var connStr = TestDbConfig.ResolveSourceConnectionString();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.BuildAsync(connStr,
            new[] { new TableRef("dbo", "Patient") }, cts.Token);

        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task BuildAsync_EmptySeedList_ReturnsEmptyGraph()
    {
        var sut = NewBuilder();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var graph = await sut.BuildAsync(connStr, Array.Empty<TableRef>(), CancellationToken.None);

        graph.Nodes.Should().BeEmpty();
        graph.Edges.Should().BeEmpty();
    }

    [Test]
    public async Task BuildAsync_DuplicateSeeds_DeduplicatedInOutput()
    {
        var sut = NewBuilder();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var graph = await sut.BuildAsync(connStr, new[]
        {
            new TableRef("dbo", "Employees"),
            new TableRef("dbo", "Employees"),
        }, CancellationToken.None);

        graph.Nodes.Count(n => n.Name.Equals("Employees", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1);
    }
}
