using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.MSSQL;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.EngineSchemaAwareResolution;

[TestFixture]
public class TableResolverTests
{
    private static PTableMetadata Tab(string schema, string name) =>
        new PTableMetadata { Schema = schema, TableName = name };

    [Test]
    public void ResolveTable_QualifiedMatch_ReturnsMatchingTable()
    {
        var tables = new List<PTableMetadata>
        {
            Tab("dbo", "Patient"),
            Tab("audit", "Log"),
        };

        var result = TableResolver.Resolve(tables, "audit", "Log");

        result.Should().NotBeNull();
        result!.Schema.Should().Be("audit");
        result.TableName.Should().Be("Log");
    }

    [Test]
    public void ResolveTable_QualifiedNoMatch_ReturnsNull()
    {
        var tables = new List<PTableMetadata> { Tab("dbo", "Patient") };

        TableResolver.Resolve(tables, "audit", "Patient").Should().BeNull();
    }

    [Test]
    public void ResolveTable_BareName_OneMatch_ReturnsThatTable()
    {
        var tables = new List<PTableMetadata>
        {
            Tab("dbo", "Patient"),
            Tab("dbo", "Visit"),
        };

        var result = TableResolver.Resolve(tables, "", "Visit");

        result.Should().NotBeNull();
        result!.TableName.Should().Be("Visit");
    }

    [Test]
    public void ResolveTable_BareName_NoMatch_ReturnsNull()
    {
        var tables = new List<PTableMetadata> { Tab("dbo", "Patient") };

        TableResolver.Resolve(tables, "", "Missing").Should().BeNull();
    }

    [Test]
    public void ResolveTable_BareName_TwoMatches_ThrowsAmbiguousTableException()
    {
        var tables = new List<PTableMetadata>
        {
            Tab("dbo", "Log"),
            Tab("audit", "Log"),
        };

        var act = () => TableResolver.Resolve(tables, "", "Log");

        var ex = act.Should().Throw<AmbiguousTableException>().Which;
        ex.TableName.Should().Be("Log");
        ex.CandidateSchemas.Should().BeEquivalentTo(new[] { "audit", "dbo" });
        ex.Message.Should().Contain("Log").And.Contain("audit").And.Contain("dbo");
    }

    [Test]
    public void ResolveTable_CaseInsensitive_QualifiedMatch()
    {
        var tables = new List<PTableMetadata> { Tab("dbo", "Patient") };

        var result = TableResolver.Resolve(tables, "DBO", "patient");

        result.Should().NotBeNull();
        result!.TableName.Should().Be("Patient");
    }
}
