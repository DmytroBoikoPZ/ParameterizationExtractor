using System;
using System.Collections.Generic;
using NUnit.Framework;
using ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.CharacterisationTests
{
    public sealed class Scenario
    {
        public string Name { get; }
        public Func<ISourceForScript> Build { get; }

        public Scenario(string name, Func<ISourceForScript> build)
        {
            Name = name;
            Build = build;
        }

        public override string ToString() => Name;
    }

    public static class Scenarios
    {
        // Cross-schema scenario `only-parent-cross-schema-chain` was deferred — the engine emits
        // `SELECT * FROM {bareName}` which resolves via the SQL user's default schema. A seed in
        // a non-default schema (e.g. `hospital.PatientTransfers`) hits "Invalid object name". The
        // missing coverage will be added by the upcoming `engine-schema-aware-resolution` feature.
        public static IEnumerable<Scenario> All { get; } = new[]
        {
            new Scenario("only-one-table-employee",            BuildOnlyOneTableEmployee),
            new Scenario("only-parent-from-payment",           BuildOnlyParentFromPayment),
            new Scenario("only-children-from-therapy-program", BuildOnlyChildrenFromTherapyProgram),
            new Scenario("fk-dep-therapy-item",                BuildFkDepTherapyItem),
            new Scenario("where-filter-during-child-walk",     BuildWhereFilterDuringChildWalk),
        };

        public static IEnumerable<TestCaseData> AsTestCases()
        {
            foreach (var s in All)
                yield return new TestCaseData(s).SetName(s.Name);
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

    }
}
