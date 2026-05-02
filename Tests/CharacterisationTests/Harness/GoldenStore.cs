using System;
using System.IO;
using NUnit.Framework;

namespace Tests.CharacterisationTests.Harness
{
    public static class GoldenStore
    {
        public const string ModeEnvVar     = "BULDOZER_GOLDEN_MODE";
        public const string ScenarioEnvVar = "BULDOZER_GOLDEN_SCENARIO";

        public static bool IsRecordingFor(string scenarioName)
        {
            var mode = Environment.GetEnvironmentVariable(ModeEnvVar);
            if (!string.Equals(mode, "record", StringComparison.OrdinalIgnoreCase))
                return false;

            var only = Environment.GetEnvironmentVariable(ScenarioEnvVar);
            return string.IsNullOrEmpty(only)
                   || string.Equals(only, scenarioName, StringComparison.Ordinal);
        }

        public static string Load(string scenarioName)
        {
            var path = ResolveGoldenPath(scenarioName);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Golden file missing for scenario '{scenarioName}'. " +
                    $"Run with {ModeEnvVar}=record (optionally {ScenarioEnvVar}={scenarioName}) to generate.",
                    path);

            return File.ReadAllText(path);
        }

        public static void Save(string scenarioName, string sql)
        {
            var path = ResolveGoldenPath(scenarioName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sql);
        }

        public static string ResolveGoldenPath(string scenarioName)
        {
            var projectRoot = ResolveTestsProjectRoot();
            return Path.Combine(projectRoot, "CharacterisationTests", "Goldens", scenarioName + ".sql");
        }

        private static string ResolveTestsProjectRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tests.csproj")))
                dir = dir.Parent;

            if (dir == null)
                throw new InvalidOperationException(
                    "Could not locate Tests.csproj walking up from " +
                    TestContext.CurrentContext.TestDirectory);

            return dir.FullName;
        }
    }
}
