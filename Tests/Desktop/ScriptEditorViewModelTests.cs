#nullable enable
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Views.Seed;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class ScriptEditorViewModelTests
{
    private sealed class Harness
    {
        public SourceForScript Source { get; } = new();
        public FakeDatabaseExplorer Explorer { get; } = new();
        public FakeUiDispatcher Ui { get; } = new();
        public int SaveCount { get; private set; }

        public ScriptEditorViewModel Build(TimeSpan? debounce = null)
        {
            return new ScriptEditorViewModel(
                Source,
                Explorer,
                Ui,
                () => { SaveCount++; return Task.CompletedTask; },
                NullLogger<ScriptEditorViewModel>.Instance,
                debounce ?? TimeSpan.Zero);
        }
    }

    [Test]
    public void Default_State_PreviewStatusEmpty()
    {
        var h = new Harness();
        var vm = h.Build();

        vm.PreviewStatus.Should().BeEmpty();
        vm.PreviewResult.Should().BeNull();
    }

    [Test]
    public void HydrateFromSource_PopulatesFieldsWithoutTriggeringSave()
    {
        var h = new Harness();
        h.Source.ScriptName = "Pre-existing";
        h.Source.Query = "SELECT 1";
        h.Source.RootRecords.Add(new RecordsToExtract { Schema = "audit", TableName = "Logs" });

        var vm = h.Build();

        vm.ScriptName.Should().Be("Pre-existing");
        vm.SeedQuery.Should().Be("SELECT 1");
        vm.RootSchema.Should().Be("audit");
        vm.RootTable.Should().Be("Logs");
        h.SaveCount.Should().Be(0, "constructor hydration must not fire save");
    }

    [Test]
    public async Task RunPreview_NoConnectionString_SetsStatus()
    {
        var h = new Harness();
        var vm = h.Build();
        vm.Initialize(null);

        await vm.RunPreviewCommand.ExecuteAsync(null);

        vm.PreviewStatus.Should().Contain("No connection");
        h.Explorer.PreviewCalls.Should().BeEmpty();
    }

    [Test]
    public async Task RunPreview_Success_PopulatesResultAndStatus()
    {
        var h = new Harness();
        h.Explorer.EnqueuePreviewResult(new PreviewResult(
            new[] { "a", "b" },
            new string?[][] { new string?[] { "1", "2" } },
            Truncated: false));

        var vm = h.Build();
        vm.Initialize("Server=fake");
        vm.SeedQuery = "SELECT 1, 2";

        await vm.RunPreviewCommand.ExecuteAsync(null);

        vm.PreviewResult.Should().NotBeNull();
        vm.PreviewResult!.Rows.Should().HaveCount(1);
        vm.PreviewStatus.Should().Be("1 rows");
    }

    [Test]
    public async Task RunPreview_Truncated_StatusMentionsTruncated()
    {
        var h = new Harness();
        h.Explorer.EnqueuePreviewResult(new PreviewResult(
            new[] { "n" },
            new string?[][] { new string?[] { "1" }, new string?[] { "2" } },
            Truncated: true));

        var vm = h.Build();
        vm.Initialize("Server=fake");

        await vm.RunPreviewCommand.ExecuteAsync(null);

        vm.PreviewStatus.Should().Contain("truncated");
    }

    [Test]
    public async Task RunPreview_Failure_SetsStatusToFailedMessage()
    {
        var h = new Harness();
        h.Explorer.EnqueuePreviewException(new DatabaseExplorerException("login failed"));

        var vm = h.Build();
        vm.Initialize("Server=fake");

        await vm.RunPreviewCommand.ExecuteAsync(null);

        vm.PreviewStatus.Should().StartWith("Failed:");
        vm.PreviewStatus.Should().Contain("login failed");
        vm.PreviewResult.Should().BeNull();
    }

    [Test]
    public async Task RunPreview_Cancellation_SetsStatusToCancelled()
    {
        var h = new Harness();
        h.Explorer.EnqueuePreviewException(new OperationCanceledException());

        var vm = h.Build();
        vm.Initialize("Server=fake");

        await vm.RunPreviewCommand.ExecuteAsync(null);

        vm.PreviewStatus.Should().Be("Cancelled");
    }

    [Test]
    public async Task RunPreview_PassesConfiguredConnectionString()
    {
        var h = new Harness();
        h.Explorer.EnqueuePreviewResult(new PreviewResult(
            new[] { "x" },
            Array.Empty<string?[]>(),
            Truncated: false));

        var vm = h.Build();
        vm.Initialize("Server=h;Database=d");
        vm.SeedQuery = "SELECT 1";

        await vm.RunPreviewCommand.ExecuteAsync(null);

        h.Explorer.PreviewCalls.Should().HaveCount(1);
        h.Explorer.PreviewCalls[0].ConnectionString.Should().Be("Server=h;Database=d");
        h.Explorer.PreviewCalls[0].Sql.Should().Be("SELECT 1");
        h.Explorer.PreviewCalls[0].MaxRows.Should().Be(200);
    }

    [Test]
    public async Task OnScriptNameChanged_DebouncedSave_FiresOnce()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);

        vm.ScriptName = "Renamed";

        // Yield once so the synchronous-on-zero-debounce save callback completes.
        await Task.Yield();
        await Task.Yield();

        h.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task MultipleRapidEdits_DebouncedToSingleSave()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.FromMilliseconds(50));

        vm.ScriptName = "v1";
        vm.ScriptName = "v2";
        vm.ScriptName = "v3";

        await Task.Delay(150);

        h.SaveCount.Should().Be(1, "rapid edits must coalesce into a single save");
    }

    [Test]
    public async Task EditAfterSave_TriggersAnotherSave()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);

        vm.ScriptName = "first";
        await Task.Yield();
        await Task.Yield();

        vm.ScriptName = "second";
        await Task.Yield();
        await Task.Yield();

        h.SaveCount.Should().Be(2);
    }

    [Test]
    public async Task WriteThroughToSource_PersistsAllEditableFields()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);

        vm.ScriptName = "S";
        vm.RootSchema = "dbo";
        vm.RootTable = "Patient";
        vm.SeedQuery = "SELECT TOP 5 * FROM dbo.Patient";

        await Task.Delay(20);

        h.Source.ScriptName.Should().Be("S");
        h.Source.Query.Should().Be("SELECT TOP 5 * FROM dbo.Patient");
        h.Source.RootRecords.Should().HaveCount(1);
        h.Source.RootRecords[0].Schema.Should().Be("dbo");
        h.Source.RootRecords[0].TableName.Should().Be("Patient");
    }

    [Test]
    public void RootRef_GetterAndSetter_RoundTripThroughSchemaAndTable()
    {
        var h = new Harness();
        var vm = h.Build();

        vm.RootRef = new TableRef("dbo", "Patient");
        vm.RootSchema.Should().Be("dbo");
        vm.RootTable.Should().Be("Patient");

        vm.RootRef.Should().Be(new TableRef("dbo", "Patient"));

        vm.RootRef = null;
        vm.RootTable.Should().BeEmpty();
        vm.RootRef.Should().BeNull();
    }

    [Test]
    public async Task OnSeedQueryChanged_PickerEmpty_AutoPopulatesFromSql()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);

        vm.SeedQuery = "SELECT * FROM dbo.Patient";

        await Task.Yield();
        await Task.Yield();

        vm.RootSchema.Should().Be("dbo");
        vm.RootTable.Should().Be("Patient");
        vm.RootMismatchHint.Should().BeEmpty();
    }

    [Test]
    public async Task OnSeedQueryChanged_PickerMatchesParsed_NoHint()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);
        vm.RootSchema = "dbo";
        vm.RootTable = "Patient";

        vm.SeedQuery = "SELECT * FROM DBO.PATIENT";

        await Task.Yield();

        vm.RootMismatchHint.Should().BeEmpty();
        vm.RootSchema.Should().Be("dbo");
        vm.RootTable.Should().Be("Patient");
    }

    [Test]
    public async Task OnSeedQueryChanged_PickerDiffersFromParsed_SetsHint()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);
        vm.RootSchema = "dbo";
        vm.RootTable = "Visit";

        vm.SeedQuery = "SELECT * FROM dbo.Patient";

        await Task.Yield();

        vm.RootMismatchHint.Should().Contain("dbo.Patient");
        vm.RootMismatchHint.Should().Contain("dbo.Visit");
        vm.RootSchema.Should().Be("dbo");
        vm.RootTable.Should().Be("Visit");
    }

    [Test]
    public async Task OnSeedQueryChanged_NoFromInSql_HintCleared_WhenPickerEmpty()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);

        vm.SeedQuery = "SELECT 1";

        await Task.Yield();

        vm.RootMismatchHint.Should().BeEmpty();
        vm.RootSchema.Should().BeEmpty();
        vm.RootTable.Should().BeEmpty();
    }

    [Test]
    public async Task OnRootSchemaChanged_AfterMismatchHint_ClearsHint()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);
        vm.RootSchema = "dbo";
        vm.RootTable = "Visit";
        vm.SeedQuery = "SELECT * FROM dbo.Patient";
        await Task.Yield();
        vm.RootMismatchHint.Should().NotBeEmpty();

        vm.RootTable = "Patient";

        vm.RootMismatchHint.Should().BeEmpty();
    }

    [Test]
    public async Task AutoPopulate_DoesNotIncrementSaveCounter_BeyondSqlChange()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);

        vm.SeedQuery = "SELECT * FROM dbo.Patient";

        await Task.Yield();
        await Task.Yield();

        h.SaveCount.Should().Be(1, "auto-populate must not trigger extra saves on top of the SQL change");
    }

    [Test]
    public void DismissRootMismatchHint_ClearsHint()
    {
        var h = new Harness();
        var vm = h.Build(TimeSpan.Zero);
        vm.RootSchema = "dbo";
        vm.RootTable = "Visit";
        vm.SeedQuery = "SELECT * FROM dbo.Patient";
        vm.RootMismatchHint.Should().NotBeEmpty();

        vm.DismissRootMismatchHintCommand.Execute(null);

        vm.RootMismatchHint.Should().BeEmpty();
    }
}
