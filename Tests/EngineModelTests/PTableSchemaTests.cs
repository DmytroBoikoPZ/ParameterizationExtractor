using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.EngineModelTests;

[TestFixture]
public class PTableSchemaTests
{
    [Test]
    public void Default_Schema_IsEmptyString()
    {
        var sut = new PTableMetadata { TableName = "Patient" };

        sut.Schema.Should().Be(string.Empty);
    }

    [Test]
    public void Equality_TupleSemantics_SchemaAndNameMustMatch_CaseInsensitive()
    {
        var a = new PTableMetadata { Schema = "dbo", TableName = "Patient" };
        var b = new PTableMetadata { Schema = "DBO", TableName = "patient" };
        var c = new PTableMetadata { Schema = "audit", TableName = "Patient" };

        a.Equals(b).Should().BeTrue("(Schema, Name) tuple comparison is OrdinalIgnoreCase");
        a.Equals(c).Should().BeFalse("different Schema disambiguates same-name tables");
    }

    [Test]
    public void GetHashCode_TupleSemantics_MatchesEquality()
    {
        var a = new PTableMetadata { Schema = "dbo", TableName = "Patient" };
        var b = new PTableMetadata { Schema = "DBO", TableName = "patient" };

        a.GetHashCode().Should().Be(b.GetHashCode(),
            "equal-by-Equals values must produce equal hash codes");
    }
}
