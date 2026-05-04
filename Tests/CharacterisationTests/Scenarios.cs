using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Configs.Json;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.CharacterisationTests
{
    /// <summary>
    /// How a characterisation scenario's <see cref="ISourceForScript"/> is constructed.
    /// Locked in <c>desktop-workspace-format/feature-architecture.md § 5.5</c>.
    /// </summary>
    public enum PackageLoader
    {
        /// <summary>Build the script in C# using the engine's POCO constructors.</summary>
        Programmatic,

        /// <summary>Deserialise the script's owning <see cref="Package"/> from a JSON fixture
        /// at <c>Tests/CharacterisationTests/Json/{scenario}.json</c> via <see cref="JsonPackageReader"/>,
        /// then return its first <c>SourceForScript</c>.</summary>
        Json
    }

    public sealed class Scenario
    {
        public string Name { get; }
        public Func<ISourceForScript> Build { get; }

        public Scenario(string name, Func<ISourceForScript> build)
        {
            Name = name;
            Build = build;
        }

        public ISourceForScript Load(PackageLoader loader) => loader switch
        {
            PackageLoader.Programmatic => Build(),
            PackageLoader.Json => LoadFromJsonFixture(Name),
            _ => throw new ArgumentOutOfRangeException(nameof(loader), loader, null)
        };

        private static ISourceForScript LoadFromJsonFixture(string name)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "CharacterisationTests", "Json", $"{name}.json");
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"JSON fixture for scenario '{name}' not found at {path}. " +
                    "Ensure the file exists under Tests/CharacterisationTests/Json/ and is copied to output.",
                    path);

            var package = JsonPackageReader.Read(path);
            if (package.Scripts.Count == 0)
                throw new InvalidOperationException(
                    $"JSON fixture for scenario '{name}' contains no scripts.");

            return package.Scripts[0];
        }

        public override string ToString() => Name;
    }

    public static class Scenarios
    {
        // Cross-schema scenario lives below as `BuildCrossSchema`. Landed by
        // `engine-schema-aware-resolution` (closes the deferred follow-up from `characterisation-tests`).
        public static IEnumerable<Scenario> All { get; } = new[]
        {
            new Scenario("only-one-table-employee",            BuildOnlyOneTableEmployee),
            new Scenario("only-parent-from-payment",           BuildOnlyParentFromPayment),
            new Scenario("only-children-from-therapy-program", BuildOnlyChildrenFromTherapyProgram),
            new Scenario("fk-dep-therapy-item",                BuildFkDepTherapyItem),
            new Scenario("where-filter-during-child-walk",     BuildWhereFilterDuringChildWalk),
            new Scenario("cross-schema",                       BuildCrossSchema),
            new Scenario("excluded-table",                     BuildExcludedTable),
        };

        /// <summary>
        /// Yields the Cartesian product of <see cref="All"/> × <see cref="PackageLoader"/>.
        /// Each scenario is exercised through both loaders against the same golden — proving
        /// that the JSON path produces byte-equal output to the programmatic / XML path
        /// (ADR-009, L10).
        /// </summary>
        public static IEnumerable<TestCaseData> AsTestCases()
        {
            foreach (var scenario in All)
            {
                foreach (PackageLoader loader in Enum.GetValues<PackageLoader>())
                {
                    yield return new TestCaseData(scenario, loader)
                        .SetName($"{scenario.Name}({loader})");
                }
            }
        }

        private static ISourceForScript BuildOnlyOneTableEmployee()
        {
            var s = new SourceForScript { ScriptName = "only-one-table-employee" };
            s.RootRecords.Add(new RecordsToExtract("Employees", "Id = 1"));
            s.TablesToProcess.Add(new TableToExtract(
                "Employees", new OnlyOneTableExtractStrategy(), new SqlBuildStrategy()));
            return s;
        }

        private static ISourceForScript BuildOnlyParentFromPayment()
        {
            var s = new SourceForScript { ScriptName = "only-parent-from-payment" };
            s.RootRecords.Add(new RecordsToExtract("Payments", "Id = 2488889"));
            s.TablesToProcess.Add(new TableToExtract(
                "Payments", new OnlyParentExtractStrategy(), new SqlBuildStrategy()));
            return s;
        }

        private static ISourceForScript BuildOnlyChildrenFromTherapyProgram()
        {
            var s = new SourceForScript { ScriptName = "only-children-from-therapy-program" };
            s.RootRecords.Add(new RecordsToExtract("TherapyPrograms", "Id = 904"));
            s.TablesToProcess.Add(new TableToExtract(
                "TherapyPrograms", new OnlyChildrenExtractStrategy(), new SqlBuildStrategy()));
            return s;
        }

        private static ISourceForScript BuildFkDepTherapyItem()
        {
            var s = new SourceForScript { ScriptName = "fk-dep-therapy-item" };
            s.RootRecords.Add(new RecordsToExtract("TherapyProgramItems", "Id = 1039"));
            s.TablesToProcess.Add(new TableToExtract(
                "TherapyProgramItems", new FKDependencyExtractStrategy(), new SqlBuildStrategy()));
            return s;
        }

        private static ISourceForScript BuildWhereFilterDuringChildWalk()
        {
            var s = new SourceForScript { ScriptName = "where-filter-during-child-walk" };
            s.RootRecords.Add(new RecordsToExtract("TherapyPrograms", "Id = 904"));
            s.TablesToProcess.Add(new TableToExtract(
                "TherapyPrograms", new OnlyChildrenExtractStrategy(), new SqlBuildStrategy()));
            s.TablesToProcess.Add(new TableToExtract(
                "TherapyProgramItems",
                new OnlyChildrenExtractStrategy { Where = "IsCanceled = 0" },
                new SqlBuildStrategy()));
            return s;
        }

        /// <summary>
        /// Cross-schema seed: child in <c>dbo</c>, parent in <c>audit</c>; engine walks the FK across
        /// the schema boundary and emits qualified <c>[audit].[SchemaTestParent]</c> identifiers.
        /// Test DB fixture is created in <see cref="CharacterisationTests"/>'s <c>OneTimeSetUp</c>.
        /// Scenario name `cross-schema` matches both the JSON fixture path and golden path.
        /// </summary>
        /// <summary>
        /// Excluded scenario: walker enters TherapyPrograms (root, OnlyChildren), reaches
        /// TherapyProgramItems via FK, but the operator marked TherapyProgramItems Excluded.
        /// The recorded golden must NOT contain any rows / Deleter calls for TherapyProgramItems.
        /// Pins ADR-012's engine wiring.
        /// </summary>
        private static ISourceForScript BuildExcludedTable()
        {
            var s = new SourceForScript { ScriptName = "excluded-table" };
            s.RootRecords.Add(new RecordsToExtract("TherapyPrograms", "Id = 904"));
            s.TablesToProcess.Add(new TableToExtract(
                "TherapyPrograms", new OnlyChildrenExtractStrategy(), new SqlBuildStrategy()));
            s.TablesToProcess.Add(new TableToExtract(
                "TherapyProgramItems",
                new OnlyChildrenExtractStrategy(),
                new SqlBuildStrategy()) { Excluded = true });
            return s;
        }

        private static ISourceForScript BuildCrossSchema()
        {
            var s = new SourceForScript { ScriptName = "cross-schema" };
            s.RootRecords.Add(new RecordsToExtract("SchemaTestChild", "Id = 100") { Schema = "dbo" });
            s.TablesToProcess.Add(new TableToExtract(
                "SchemaTestChild",
                new OnlyParentExtractStrategy { ProcessParents = true },
                new SqlBuildStrategy()) { Schema = "dbo" });
            s.TablesToProcess.Add(new TableToExtract(
                "SchemaTestParent",
                new OnlyOneTableExtractStrategy(),
                new SqlBuildStrategy()) { Schema = "audit" });
            return s;
        }

    }
}
