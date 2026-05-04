using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Model;
using Tests.CharacterisationTests.Harness;

namespace Tests.CharacterisationTests
{
    [TestFixture]
    public class CharacterisationTests
    {
        private CharacterisationRunner _runner;
        public static bool CrossSchemaAvailable { get; private set; }

        [OneTimeSetUp]
        public async Task BuildEngine()
        {
            var connectionString = TestDbConfig.ResolveSourceConnectionString();

            // Idempotent cross-schema fixture for the engine-schema-aware-resolution scenario.
            // Skipped (with a TestContext message) if the test runner's account lacks CREATE SCHEMA.
            CrossSchemaAvailable = await EnsureCrossSchemaFixtureAsync(connectionString);

            var config = new GlobalExtractConfiguration
            {
                DefaultExtractStrategy = new OnlyChildrenExtractStrategy(),
                DefaultSqlBuildStrategy = new SqlBuildStrategy(),
                ResultingScriptOptions = new ResultingScriptOptions { TargetDatabase = "budzdorov_Core" }
            };
            _runner = await CharacterisationRunner.CreateAsync(connectionString, config);
        }

        private static async Task<bool> EnsureCrossSchemaFixtureAsync(string connectionString)
        {
            const string setupSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'audit')
    EXEC('CREATE SCHEMA audit');
IF OBJECT_ID('audit.SchemaTestParent') IS NULL
    EXEC('CREATE TABLE audit.SchemaTestParent (
        Id int NOT NULL PRIMARY KEY,
        Note nvarchar(100) NULL)');
IF OBJECT_ID('dbo.SchemaTestChild') IS NULL
    EXEC('CREATE TABLE dbo.SchemaTestChild (
        Id int NOT NULL PRIMARY KEY,
        ParentId int NOT NULL CONSTRAINT FK_SchemaTestChild_Parent
            REFERENCES audit.SchemaTestParent(Id))');
IF NOT EXISTS (SELECT 1 FROM audit.SchemaTestParent WHERE Id = 1)
    INSERT audit.SchemaTestParent(Id, Note) VALUES (1, 'cross-schema test root');
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaTestChild WHERE Id = 100)
    INSERT dbo.SchemaTestChild(Id, ParentId) VALUES (100, 1);
";
            try
            {
                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(setupSql, conn) { CommandTimeout = 30 };
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (SqlException ex)
            {
                TestContext.Out.WriteLine($"Cross-schema fixture skipped — CREATE SCHEMA / CREATE TABLE rejected: {ex.Message}");
                return false;
            }
        }

        [TestCaseSource(typeof(Scenarios), nameof(Scenarios.AsTestCases))]
        public async Task Scenario_Matches_Golden(Scenario scenario, PackageLoader loader)
        {
            if (scenario.Name == "cross-schema" && !CrossSchemaAvailable)
            {
                Assert.Ignore("Cross-schema fixture not available — test runner lacks CREATE SCHEMA permission on the test DB.");
            }

            var script = scenario.Load(loader);
            var actual = await _runner.RunScenarioAsync(script);

            Assert.That(actual, Is.Not.Empty,
                $"Engine produced empty SQL for scenario '{scenario.Name}' via {loader} loader.");

            // Goldens are shared across loaders. Recording is keyed on the scenario name only —
            // both loaders write/read the same golden so divergence between them surfaces as a
            // test failure, not a silent overwrite.
            if (GoldenStore.IsRecordingFor(scenario.Name) && loader == PackageLoader.Programmatic)
            {
                GoldenStore.Save(scenario.Name, actual);
                Assert.Pass($"Recorded golden for '{scenario.Name}' ({actual.Length} chars) via Programmatic loader.");
                return;
            }

            var golden = GoldenStore.Load(scenario.Name);
            Assert.That(actual, Is.EqualTo(golden),
                $"Output drifted from golden for scenario '{scenario.Name}' via {loader} loader. " +
                $"Set {GoldenStore.ModeEnvVar}=record (optionally {GoldenStore.ScenarioEnvVar}={scenario.Name}) " +
                "to regenerate after reviewing the diff.");
        }
    }
}
