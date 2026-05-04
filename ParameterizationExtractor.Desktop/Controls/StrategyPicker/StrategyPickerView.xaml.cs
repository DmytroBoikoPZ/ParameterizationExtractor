using System;
using System.Windows;
using System.Windows.Controls;

namespace Quipu.ParameterizationExtractor.Desktop.Controls.StrategyPicker;

internal partial class StrategyPickerView : UserControl
{
    public StrategyPickerView()
    {
        InitializeComponent();
        // Per-instance unique group name so multiple pickers in the same window don't share state.
        SetCurrentValue(GroupNameProperty, "StrategyPicker_" + Guid.NewGuid().ToString("N"));
    }

    public static readonly DependencyProperty StrategyChoiceProperty = DependencyProperty.Register(
        nameof(StrategyChoice),
        typeof(StrategyKind),
        typeof(StrategyPickerView),
        new FrameworkPropertyMetadata(
            StrategyKind.FKDependency,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public StrategyKind StrategyChoice
    {
        get => (StrategyKind)GetValue(StrategyChoiceProperty);
        set => SetValue(StrategyChoiceProperty, value);
    }

    public static readonly DependencyProperty GroupNameProperty = DependencyProperty.Register(
        nameof(GroupName),
        typeof(string),
        typeof(StrategyPickerView),
        new FrameworkPropertyMetadata(string.Empty));

    /// <summary>
    /// Per-instance unique group name (set in the constructor) so multiple <see cref="StrategyPickerView"/>
    /// instances rendered in the same window (e.g. one per row in <c>ExtrasView</c>) don't share radio state.
    /// Hosts can override if they need a specific name.
    /// </summary>
    public string GroupName
    {
        get => (string)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }
}
