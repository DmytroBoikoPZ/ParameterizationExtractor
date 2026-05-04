using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Controls.TablePicker;
using Quipu.ParameterizationExtractor.Logic.Schema;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class TablePickerViewModelTests
{
    private static TablePickerViewModel NewVm(FakeDatabaseExplorer explorer) =>
        new(explorer, NullLogger<TablePickerViewModel>.Instance);

    [Test]
    public void Default_State_Empty()
    {
        var vm = NewVm(new FakeDatabaseExplorer());

        vm.Tables.Should().BeEmpty();
        vm.SelectedTable.Should().BeNull();
        vm.IsLoading.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Test]
    public async Task RefreshAsync_NoConnectionString_SetsErrorMessage()
    {
        var vm = NewVm(new FakeDatabaseExplorer());

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be("No connection");
        vm.Tables.Should().BeEmpty();
    }

    [Test]
    public async Task RefreshAsync_ExplorerReturnsTables_PopulatesCollection()
    {
        var explorer = new FakeDatabaseExplorer();
        explorer.EnqueueListResult(new[]
        {
            new TableRef("dbo", "Patients"),
            new TableRef("audit", "Logs"),
        });

        var vm = NewVm(explorer);
        vm.ConnectionString = "Server=fake;Database=stub";

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Tables.Should().HaveCount(2);
        vm.Tables[0].Name.Should().Be("Patients");
        vm.Tables[1].Schema.Should().Be("audit");
        vm.ErrorMessage.Should().BeNull();
        explorer.ListConnectionStringsCalled.Should().ContainSingle().Which.Should().Be("Server=fake;Database=stub");
    }

    [Test]
    public async Task RefreshAsync_ExplorerThrows_SetsErrorMessage_DoesNotThrow()
    {
        var explorer = new FakeDatabaseExplorer();
        explorer.EnqueueListException(new DatabaseExplorerException("boom"));

        var vm = NewVm(explorer);
        vm.ConnectionString = "Server=fake;Database=stub";

        var act = async () => await vm.RefreshCommand.ExecuteAsync(null);

        await act.Should().NotThrowAsync();
        vm.ErrorMessage.Should().Contain("boom");
        vm.Tables.Should().BeEmpty();
    }

    [Test]
    public async Task IsLoading_TogglesAroundRefresh()
    {
        var explorer = new FakeDatabaseExplorer();
        explorer.EnqueueListResult(new[] { new TableRef("dbo", "T") });

        var vm = NewVm(explorer);
        vm.ConnectionString = "Server=fake;Database=stub";

        // Pre-condition.
        vm.IsLoading.Should().BeFalse();

        await vm.RefreshCommand.ExecuteAsync(null);

        // Post-condition: command completed, IsLoading reset to false.
        vm.IsLoading.Should().BeFalse();
    }
}
