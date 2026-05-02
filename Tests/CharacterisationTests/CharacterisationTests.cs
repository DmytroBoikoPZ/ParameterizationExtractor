using System.Threading.Tasks;
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

        [TestCaseSource(typeof(Scenarios), nameof(Scenarios.AsTestCases))]
        public async Task Scenario_Matches_Golden(Scenario scenario)
        {
            var actual = await _runner.RunScenarioAsync(scenario.Build());

            Assert.That(actual, Is.Not.Empty, "Engine produced empty SQL for scenario " + scenario.Name);

            if (GoldenStore.IsRecordingFor(scenario.Name))
            {
                GoldenStore.Save(scenario.Name, actual);
                Assert.Pass($"Recorded golden for '{scenario.Name}' ({actual.Length} chars).");
                return;
            }

            var golden = GoldenStore.Load(scenario.Name);
            Assert.That(actual, Is.EqualTo(golden),
                $"Output drifted from golden for scenario '{scenario.Name}'. " +
                $"Set {GoldenStore.ModeEnvVar}=record (optionally {GoldenStore.ScenarioEnvVar}={scenario.Name}) " +
                "to regenerate after reviewing the diff.");
        }
    }
}
