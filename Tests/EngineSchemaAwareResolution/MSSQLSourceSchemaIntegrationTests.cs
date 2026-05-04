using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ParameterizationExtractor.Logic.Model;
using ParameterizationExtractor.Logic.MSSQL;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.MSSQL;
using Tests.CharacterisationTests.Harness;

namespace Tests.EngineSchemaAwareResolution;

[TestFixture]
public class MSSQLSourceSchemaIntegrationTests
{
    private MSSQLSourceSchema _schema = null!;

    [OneTimeSetUp]
    public async Task Init()
    {
        var connStr = TestDbConfig.ResolveSourceConnectionString();
        var uowFactory = new TestUnitOfWorkFactory(connStr);
        var config = new GlobalExtractConfiguration
        {
            DefaultExtractStrategy = new OnlyChildrenExtractStrategy(),
            DefaultSqlBuildStrategy = new SqlBuildStrategy(),
            ResultingScriptOptions = new ResultingScriptOptions { TargetDatabase = "budzdorov_Core" },
        };
        var metaInit = new MetaDataInitializer();
        var metaProvider = new ObjectMetaDataProvider(uowFactory, NullLogger<ObjectMetaDataProvider>.Instance, metaInit);
        _schema = new MSSQLSourceSchema(uowFactory, config, NullLogger<MSSQLSourceSchema>.Instance, metaProvider);
        await _schema.Init(CancellationToken.None);
    }

    [Test]
    public void GetMetaData_PopulatesSchemaForDiscoveredTables()
    {
        _schema.Tables.Should().NotBeEmpty();
        _schema.Tables.Should().OnlyContain(t => !string.IsNullOrEmpty(t.Schema),
            "every discovered PTableMetadata must carry the schema from sys.schemas (ADR-011)");
    }

    [Test]
    public void GetMetaData_DiscoveredTables_HaveDboSchema()
    {
        // The test DB's tables live in dbo (no other schemas seeded by default).
        // After step 05 lands the cross-schema scenario, "audit" tables will also exist.
        var dboTables = _schema.Tables.Where(t => string.Equals(t.Schema, "dbo", System.StringComparison.OrdinalIgnoreCase)).ToList();

        dboTables.Should().NotBeEmpty("test DB should have at least one table in dbo");
    }

    [Test]
    public void GetDependentTables_PopulatesParentSchemaAndReferencedSchema()
    {
        _schema.DependentTables.Should().NotBeEmpty();
        _schema.DependentTables.Should().OnlyContain(d => !string.IsNullOrEmpty(d.ParentSchema) && !string.IsNullOrEmpty(d.ReferencedSchema),
            "FK metadata must carry both Parent and Referenced schemas (ADR-011)");
    }

    [Test]
    public void ResolveTable_BareNameMatchingDiscoveredTable_ReturnsThatTable()
    {
        var firstTable = _schema.Tables.First();

        var resolved = _schema.ResolveTable(string.Empty, firstTable.TableName);

        resolved.Should().NotBeNull();
        resolved!.TableName.Should().Be(firstTable.TableName);
    }
}
