using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.Helpers;

namespace Tests.EngineSchemaAwareResolution;

[TestFixture]
public class SqlEmissionQualifyTests
{
    [Test]
    public void Qualify_EmptyEmissionSchema_ProducesBareName()
    {
        SqlHelper.Qualify(string.Empty, "Patient").Should().Be("Patient",
            "empty emissionSchema preserves the legacy bare-name emission");
    }

    [Test]
    public void Qualify_NullEmissionSchema_ProducesBareName()
    {
        SqlHelper.Qualify(null, "Patient").Should().Be("Patient");
    }

    [Test]
    public void Qualify_NonEmptyEmissionSchema_ProducesBracketedQualifiedName()
    {
        SqlHelper.Qualify("audit", "Log").Should().Be("[audit].[Log]");
    }

    [Test]
    public void QualifyForDeleter_EmptyEmissionSchema_ProducesBareName()
    {
        SqlHelper.QualifyForDeleter(string.Empty, "Patient").Should().Be("Patient");
    }

    [Test]
    public void QualifyForDeleter_NonEmptyEmissionSchema_ProducesUnbracketedDottedName()
    {
        SqlHelper.QualifyForDeleter("audit", "Log").Should().Be("audit.Log",
            "Deleter SP's @TableName parameter expects schema.table without brackets");
    }
}
