using System.IO;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.Configs.Json;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.EngineJsonTests;

[TestFixture]
public class JsonGlobalConfigReaderTests
{
    [Test]
    public void Read_CompleteConfig_DeserialisesAllFields()
    {
        const string json = """
        {
          "fieldsToExclude": [ "RowVersion", "ModifiedAt" ],
          "uniqueColums": [
            {
              "tableName": "Patient",
              "uniqueColumns": [ "LastName", "FirstName" ]
            }
          ],
          "defaultExtractStrategy": {
            "$kind": "OnlyOneTable",
            "processChildren": false,
            "processParents": false
          },
          "defaultSqlBuildStrategy": {
            "throwExecptionIfNotExists": true,
            "noInserts": false,
            "asIsInserts": false,
            "identityInsert": true
          },
          "resultingScriptOptions": {
            "targetDatabase": "TargetDb",
            "rollback": false
          }
        }
        """;

        var config = JsonGlobalConfigReader.Read(ToStream(json));

        config.Should().NotBeNull();
        config.FieldsToExclude.Should().BeEquivalentTo(new[] { "RowVersion", "ModifiedAt" });

        config.UniqueColums.Should().HaveCount(1);
        config.UniqueColums[0].TableName.Should().Be("Patient");
        config.UniqueColums[0].UniqueColumns.Should().BeEquivalentTo(new[] { "LastName", "FirstName" });

        config.DefaultExtractStrategy.Should().BeOfType<OnlyOneTableExtractStrategy>();
        config.DefaultExtractStrategy.ProcessChildren.Should().BeFalse();
        config.DefaultExtractStrategy.ProcessParents.Should().BeFalse();

        config.DefaultSqlBuildStrategy.ThrowExecptionIfNotExists.Should().BeTrue();
        config.DefaultSqlBuildStrategy.IdentityInsert.Should().BeTrue();

        config.ResultingScriptOptions.TargetDatabase.Should().Be("TargetDb");
        config.ResultingScriptOptions.Rollback.Should().BeFalse();
    }

    [Test]
    public void Read_DefaultStrategyMissingKind_DeserialisesToBaseExtractStrategy()
    {
        // Documented behaviour (workspace-format.md § 1, ADR-009): with [JsonPolymorphic] on a
        // concrete base, a missing $kind falls back to the base class. Closed-set safety is enforced
        // only for explicitly *unknown* discriminators (covered by JsonPackageReaderTests).
        const string json = """
        {
          "fieldsToExclude": [],
          "uniqueColums": [],
          "defaultExtractStrategy": {
            "processChildren": true,
            "processParents": false,
            "where": null
          },
          "defaultSqlBuildStrategy": {},
          "resultingScriptOptions": { "targetDatabase": "", "rollback": true }
        }
        """;

        var config = JsonGlobalConfigReader.Read(ToStream(json));

        config.DefaultExtractStrategy.Should().NotBeNull();
        config.DefaultExtractStrategy.GetType().Should().Be<ExtractStrategy>(); // exactly the base type, not a subclass
        config.DefaultExtractStrategy.ProcessChildren.Should().BeTrue();
        config.DefaultExtractStrategy.ProcessParents.Should().BeFalse();
    }

    private static Stream ToStream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));
}
