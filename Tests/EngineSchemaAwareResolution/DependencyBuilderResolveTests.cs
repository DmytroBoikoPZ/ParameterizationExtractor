using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ParameterizationExtractor.Logic.Model;
using ParameterizationExtractor.Logic.MSSQL;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.MSSQL;
using Tests.CharacterisationTests.Harness;

namespace Tests.EngineSchemaAwareResolution;

[TestFixture]
public class DependencyBuilderResolveTests
{
    private TestUnitOfWorkFactory _uowFactory = null!;
    private MSSQLSourceSchema _schema = null!;
    private GlobalExtractConfiguration _config = null!;

    [OneTimeSetUp]
    public async Task Init()
    {
        var connStr = TestDbConfig.ResolveSourceConnectionString();
        _uowFactory = new TestUnitOfWorkFactory(connStr);
        _config = new GlobalExtractConfiguration
        {
            DefaultExtractStrategy = new OnlyChildrenExtractStrategy(),
            DefaultSqlBuildStrategy = new SqlBuildStrategy(),
            ResultingScriptOptions = new ResultingScriptOptions { TargetDatabase = "budzdorov_Core" },
        };
        var metaProvider = new ObjectMetaDataProvider(_uowFactory, NullLogger<ObjectMetaDataProvider>.Instance, new MetaDataInitializer());
        _schema = new MSSQLSourceSchema(_uowFactory, _config, NullLogger<MSSQLSourceSchema>.Instance, metaProvider);
        await _schema.Init(CancellationToken.None);
    }

    private DependencyBuilder NewBuilder() =>
        new DependencyBuilder(_uowFactory, _schema, NullLogger<DependencyBuilder>.Instance, _config);

    private static SourceForScript Source(string scriptName, string rootSchema, string rootTable, string where, string tableSchema, string tableName)
    {
        var s = new SourceForScript { ScriptName = scriptName };
        s.RootRecords.Add(new RecordsToExtract(rootTable, where) { Schema = string.IsNullOrEmpty(rootSchema) ? null : rootSchema });
        s.TablesToProcess.Add(new TableToExtract(tableName, new OnlyOneTableExtractStrategy())
        {
            Schema = string.IsNullOrEmpty(tableSchema) ? null : tableSchema,
        });
        return s;
    }

    [Test]
    public async Task Prepare_BareNameConfig_OneMatchInDiscovered_ResolvesAndWalks()
    {
        // Mirrors the existing only-one-table-employee scenario but bare-named.
        var template = Source("bare-name-test", string.Empty, "Employees", "Id = 1", string.Empty, "Employees");
        var builder = NewBuilder();

        var records = await builder.PrepareAsync(CancellationToken.None, template);

        records.Should().NotBeEmpty();
        records.First().TableName.Should().Be("Employees");
    }

    [Test]
    public async Task Prepare_QualifiedConfig_MatchesExactSchema()
    {
        // Same scenario with explicit Schema="dbo" — should resolve identically.
        var template = Source("qualified-dbo-test", "dbo", "Employees", "Id = 1", "dbo", "Employees");
        var builder = NewBuilder();

        var records = await builder.PrepareAsync(CancellationToken.None, template);

        records.Should().NotBeEmpty();
        records.First().TableName.Should().Be("Employees");
        records.First().Schema.Should().Be("dbo");
    }

    [Test]
    public void Prepare_QualifiedConfig_WrongSchema_Throws()
    {
        // Wrong schema — engine resolves Tables.ResolveTable("nonexistent", "Employees") → null
        // → PrepareTableMetaData throws InvalidOperationException with descriptive message.
        var template = Source("wrong-schema-test", "nonexistent", "Employees", "Id = 1", "nonexistent", "Employees");
        var builder = NewBuilder();

        var act = () => builder.PrepareAsync(CancellationToken.None, template);

        act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("Employees", StringComparison.OrdinalIgnoreCase)
                      && ex.Message.Contains("nonexistent", StringComparison.OrdinalIgnoreCase));
    }
}
