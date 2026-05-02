using System.Threading.Tasks;
using NUnit.Framework;
using ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;
using Tests.CharacterisationTests.Harness;

namespace Tests.CharacterisationTests
{
    [TestFixture]
    public class CharacterisationRunnerSmokeTests
    {
        private CharacterisationRunner _runner;

        [OneTimeSetUp]
        public async Task BuildEngine()
        {
            var connectionString = TestDbConfig.ResolveSourceConnectionString();

            var config = new GlobalExtractConfiguration
            {
                DefaultExtractStrategy = new OnlyChildrenExtractStrategy(),
                DefaultSqlBuildStrategy = new SqlBuildStrategy(),
                ResultingScriptOptions = new ResultingScriptOptions { TargetDatabase = "budzdorov_Core" }
            };

            _runner = await CharacterisationRunner.CreateAsync(connectionString, config);
        }

        [Test]
        public async Task Scenario_OnlyOneTable_Employee_Returns_NonEmpty_Sql()
        {
            var scenario = new SourceForScript { ScriptName = "smoke-only-one-table-employee" };
            scenario.RootRecords.Add(new RecordsToExtract("Employees", "Id = 1"));
            scenario.TablesToProcess.Add(new TableToExtract(
                "Employees",
                new OnlyOneTableExtractStrategy(),
                new SqlBuildStrategy()));

            var sql = await _runner.RunScenarioAsync(scenario);

            Assert.That(sql, Is.Not.Empty);
            Assert.That(sql, Does.Contain("Employees"),
                "Generated SQL should reference the seed table by name.");
        }
    }
}
