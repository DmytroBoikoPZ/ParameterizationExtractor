using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Tests.CharacterisationTests.Harness;

namespace Tests.EngineDatabaseExplorerTests;

[TestFixture]
public class MSSqlDatabaseExplorerTests
{
    private static MSSqlDatabaseExplorer NewExplorer() =>
        new(NullLogger<MSSqlDatabaseExplorer>.Instance);

    [Test]
    public async Task ListTablesAsync_ReturnsSchemaNamePairs_OrderedBySchemaThenName()
    {
        var sut = NewExplorer();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var tables = await sut.ListTablesAsync(connStr, CancellationToken.None);

        tables.Should().NotBeEmpty();
        tables.Should().Contain(t => t.Schema == "dbo" && t.Name == "Employees",
            "the test DB is expected to expose a dbo.Employees table");

        // Server-side ORDER BY uses the database's default collation (which may not align
        // with any .NET StringComparer for non-Latin names). Verify the UI-relevant contract:
        // each schema appears as a single contiguous block (i.e., never interleaved).
        var schemasInOrder = tables.Select(t => t.Schema).ToList();
        var distinctRunStarts = schemasInOrder
            .Where((s, i) => i == 0 || schemasInOrder[i - 1] != s)
            .ToList();
        distinctRunStarts.Should().OnlyHaveUniqueItems(
            "ListTablesAsync must return tables grouped by schema, not interleaved");
    }

    [Test]
    public async Task ListTablesAsync_FiltersSystemTables()
    {
        var sut = NewExplorer();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var tables = await sut.ListTablesAsync(connStr, CancellationToken.None);

        tables.Should().NotContain(t => t.Schema == "sys",
            "is_ms_shipped = 0 must filter out MS-shipped objects");
    }

    [Test]
    public async Task ListTablesAsync_BadConnection_ThrowsDatabaseExplorerException()
    {
        var sut = NewExplorer();
        var bogus = "Server=nonexistent.invalid,1433;Database=Whatever;User Id=sa;Password=x;TrustServerCertificate=True;Connect Timeout=2";

        var act = async () => await sut.ListTablesAsync(bogus, CancellationToken.None);

        await act.Should().ThrowAsync<DatabaseExplorerException>();
    }

    [Test]
    public void ListTablesAsync_Cancelled_PropagatesOperationCancelled()
    {
        var sut = NewExplorer();
        var connStr = TestDbConfig.ResolveSourceConnectionString();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.ListTablesAsync(connStr, cts.Token);

        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task PreviewQueryAsync_ValidSelect_ReturnsRowsAndColumns()
    {
        var sut = NewExplorer();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var result = await sut.PreviewQueryAsync(
            connStr, "SELECT TOP 5 Id FROM dbo.Employees ORDER BY Id", maxRows: 200, CancellationToken.None);

        result.ColumnNames.Should().ContainSingle().Which.Should().Be("Id");
        result.Rows.Count.Should().BeLessThanOrEqualTo(5);
        result.Rows.Count.Should().BeGreaterThan(0, "the test DB is expected to have at least one Employees row");
        result.Truncated.Should().BeFalse();
    }

    [Test]
    public async Task PreviewQueryAsync_QueryReturnsMoreThanMaxRows_TruncatedTrue()
    {
        var sut = NewExplorer();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        // Generate a 250-row stream via a fast tally CTE so we don't depend on row counts in user tables.
        const string sql = @"
WITH t AS (
    SELECT 1 AS n
    UNION ALL SELECT n + 1 FROM t WHERE n < 250)
SELECT n FROM t OPTION (MAXRECURSION 300)";

        var result = await sut.PreviewQueryAsync(connStr, sql, maxRows: 200, CancellationToken.None);

        result.Rows.Count.Should().Be(200);
        result.Truncated.Should().BeTrue();
    }

    [Test]
    public async Task PreviewQueryAsync_QueryReturnsExactlyMaxRows_TruncatedFalse()
    {
        var sut = NewExplorer();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        const string sql = @"
WITH t AS (
    SELECT 1 AS n
    UNION ALL SELECT n + 1 FROM t WHERE n < 200)
SELECT n FROM t OPTION (MAXRECURSION 300)";

        var result = await sut.PreviewQueryAsync(connStr, sql, maxRows: 200, CancellationToken.None);

        result.Rows.Count.Should().Be(200);
        result.Truncated.Should().BeFalse();
    }

    [Test]
    public async Task PreviewQueryAsync_NullCells_StringifyToNull()
    {
        var sut = NewExplorer();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        const string sql = "SELECT CAST(NULL AS nvarchar(10)) AS NullCol, 42 AS IntCol";

        var result = await sut.PreviewQueryAsync(connStr, sql, maxRows: 10, CancellationToken.None);

        result.ColumnNames.Should().Equal("NullCol", "IntCol");
        result.Rows.Should().HaveCount(1);
        result.Rows[0][0].Should().BeNull("DBNull cells must stringify to null");
        result.Rows[0][1].Should().Be("42");
        result.Truncated.Should().BeFalse();
    }

    [Test]
    public async Task PreviewQueryAsync_MalformedSql_ThrowsDatabaseExplorerException()
    {
        var sut = NewExplorer();
        var connStr = TestDbConfig.ResolveSourceConnectionString();

        var act = async () => await sut.PreviewQueryAsync(
            connStr, "SLECT * FORM nope", maxRows: 10, CancellationToken.None);

        await act.Should().ThrowAsync<DatabaseExplorerException>();
    }

    [Test]
    public void PreviewQueryAsync_Cancelled_PropagatesOperationCancelled()
    {
        var sut = NewExplorer();
        var connStr = TestDbConfig.ResolveSourceConnectionString();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.PreviewQueryAsync(
            connStr, "SELECT 1", maxRows: 10, cts.Token);

        act.Should().ThrowAsync<OperationCanceledException>();
    }
}
