using FluentAssertions;
using NUnit.Framework;
using ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Desktop.Views.Overview;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.Desktop;

[TestFixture]
public class OverviewViewModelTests
{
    [Test]
    public void Show_WithNullWorkspace_PopulatesEmptyStatePlaceholders()
    {
        var sut = new OverviewViewModel();

        sut.Show(null);

        sut.WorkspaceName.Should().Be("<no workspace>");
        sut.SourceSummary.Should().Be("<no workspace>");
        sut.PathSummary.Should().Be("<no workspace>");
        sut.LastDryRunSummary.Should().Be("never");
        sut.PendingWarning.Should().BeNull();
        sut.IsEmpty.Should().BeTrue();
        sut.EmptyStateMessage.Should().NotBeEmpty();
    }

    [Test]
    public void Show_WithWorkspace_PopulatesNameAndSourceSummary()
    {
        var workspace = new WorkspaceModel
        {
            Name = "patient-clearing",
            Source = new WorkspaceSource
            {
                Server = "127.0.0.1,1433",
                Database = "TestDb",
                Auth = "sql"
            },
            Package = new Package()
        };

        var sut = new OverviewViewModel();
        sut.Show(workspace);

        sut.WorkspaceName.Should().Be("patient-clearing");
        sut.SourceSummary.Should().Be("TestDb @ 127.0.0.1,1433");
        sut.IsEmpty.Should().BeFalse();
        sut.EmptyStateMessage.Should().BeEmpty();
    }

    [Test]
    public void Show_WithMultiTableWorkspace_PathSummaryReportsTotalTableCount()
    {
        var workspace = new WorkspaceModel
        {
            Name = "multi",
            Source = new WorkspaceSource { Server = "s", Database = "d", Auth = "windows" },
            Package = new Package
            {
                Scripts =
                {
                    new SourceForScript
                    {
                        ScriptName = "first",
                        TablesToProcess =
                        {
                            new TableToExtract("A"),
                            new TableToExtract("B"),
                            new TableToExtract("C"),
                        }
                    },
                    new SourceForScript
                    {
                        ScriptName = "second",
                        TablesToProcess =
                        {
                            new TableToExtract("D"),
                            new TableToExtract("E"),
                            new TableToExtract("F"),
                        }
                    }
                }
            }
        };

        var sut = new OverviewViewModel();
        sut.Show(workspace);

        sut.PathSummary.Should().Be("6 tables in package");
    }

    [Test]
    public void Show_TransitionsBetweenStates_PropertiesRefresh()
    {
        var sut = new OverviewViewModel();
        sut.IsEmpty.Should().BeTrue("default state");

        var workspace = new WorkspaceModel
        {
            Name = "loaded",
            Source = new WorkspaceSource { Server = "s", Database = "d", Auth = "sql" },
            Package = new Package()
        };
        sut.Show(workspace);
        sut.IsEmpty.Should().BeFalse();
        sut.WorkspaceName.Should().Be("loaded");

        sut.Show(null);
        sut.IsEmpty.Should().BeTrue();
        sut.WorkspaceName.Should().Be("<no workspace>");
    }
}
