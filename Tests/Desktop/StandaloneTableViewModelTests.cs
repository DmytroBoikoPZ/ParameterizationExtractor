#nullable enable
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Controls.StrategyPicker;
using Quipu.ParameterizationExtractor.Desktop.Views.Extras;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.Desktop;

[TestFixture]
public class StandaloneTableViewModelTests
{
    private sealed class Harness
    {
        public int SaveCount;
        public int RemoveCount;

        public StandaloneTableViewModel Build(TableToExtract source, TimeSpan? debounce = null)
        {
            return new StandaloneTableViewModel(
                source,
                () => { SaveCount++; return Task.CompletedTask; },
                _ => { RemoveCount++; return Task.CompletedTask; },
                NullLogger<StandaloneTableViewModel>.Instance,
                debounce ?? TimeSpan.Zero);
        }
    }

    private static TableToExtract NewEntry(string name = "Lookup1", string schema = "dbo",
        ExtractStrategy? strategy = null, bool excluded = false) =>
        new(name, strategy ?? new OnlyOneTableExtractStrategy()) { Schema = schema, Excluded = excluded };

    [Test]
    public void Construction_HydratesFromSource_NoSave()
    {
        var h = new Harness();
        var entry = NewEntry(strategy: new OnlyChildrenExtractStrategy { Where = "Active = 1" }, excluded: true);

        var vm = h.Build(entry);

        vm.Schema.Should().Be("dbo");
        vm.Name.Should().Be("Lookup1");
        vm.Display.Should().Be("dbo.Lookup1");
        vm.StrategyChoice.Should().Be(StrategyKind.OnlyChildren);
        vm.Where.Should().Be("Active = 1");
        vm.Excluded.Should().BeTrue();
        vm.IsExpanded.Should().BeFalse();
        h.SaveCount.Should().Be(0, "construction must not fire save");
    }

    [Test]
    public void Display_BareSchema_OmitsDot()
    {
        var h = new Harness();
        var vm = h.Build(NewEntry(schema: ""));

        vm.Display.Should().Be("Lookup1");
    }

    [Test]
    public void OnStrategyChoiceChanged_ReplacesEngineStrategy_PreservesWhere()
    {
        var h = new Harness();
        var entry = NewEntry(strategy: new FKDependencyExtractStrategy { Where = "Active = 1" });
        var vm = h.Build(entry);

        vm.StrategyChoice = StrategyKind.OnlyParent;

        entry.ExtractStrategy.Should().BeOfType<OnlyParentExtractStrategy>();
        entry.ExtractStrategy.Where.Should().Be("Active = 1");
        h.SaveCount.Should().Be(1);
    }

    [Test]
    public void OnWhereChanged_MutatesEngineWhere_FiresSave()
    {
        var h = new Harness();
        var entry = NewEntry();
        var vm = h.Build(entry);

        vm.Where = "Id < 100";

        entry.ExtractStrategy.Where.Should().Be("Id < 100");
        h.SaveCount.Should().Be(1);
    }

    [Test]
    public void OnExcludedChanged_MutatesEngineExcluded_FiresSave()
    {
        var h = new Harness();
        var entry = NewEntry();
        var vm = h.Build(entry);

        vm.Excluded = true;

        entry.Excluded.Should().BeTrue();
        h.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task RemoveAsync_DelegatesToParentCallback()
    {
        var h = new Harness();
        var vm = h.Build(NewEntry());

        await vm.RemoveCommand.ExecuteAsync(null);

        h.RemoveCount.Should().Be(1);
    }

    [Test]
    public async Task MultipleRapidEdits_DebouncedToSingleSave()
    {
        var h = new Harness();
        var vm = h.Build(NewEntry(), TimeSpan.FromMilliseconds(50));

        vm.Where = "v1";
        vm.Where = "v2";
        vm.Where = "v3";

        await Task.Delay(150);

        h.SaveCount.Should().Be(1, "rapid edits must coalesce into a single save");
    }

    [Test]
    public void ToggleExpand_FlipsIsExpanded()
    {
        var h = new Harness();
        var vm = h.Build(NewEntry());

        vm.ToggleExpandCommand.Execute(null);
        vm.IsExpanded.Should().BeTrue();

        vm.ToggleExpandCommand.Execute(null);
        vm.IsExpanded.Should().BeFalse();
    }

    [Test]
    public void StrategyChip_ReflectsStrategyChoice()
    {
        var h = new Harness();
        var vm = h.Build(NewEntry(strategy: new OnlyOneTableExtractStrategy()));

        vm.StrategyChip.Should().Be("OnlyOneTable");

        vm.StrategyChoice = StrategyKind.OnlyChildren;

        vm.StrategyChip.Should().Be("OnlyChildren");
    }
}
