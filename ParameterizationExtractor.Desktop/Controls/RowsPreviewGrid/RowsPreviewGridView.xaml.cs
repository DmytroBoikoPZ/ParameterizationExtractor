using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Quipu.ParameterizationExtractor.Logic.Schema;

namespace Quipu.ParameterizationExtractor.Desktop.Controls.RowsPreviewGrid;

internal partial class RowsPreviewGridView : UserControl
{
    public RowsPreviewGridView()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(
        nameof(Result),
        typeof(PreviewResult),
        typeof(RowsPreviewGridView),
        new FrameworkPropertyMetadata(null, OnResultChanged));

    public PreviewResult? Result
    {
        get => (PreviewResult?)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    private static void OnResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RowsPreviewGridView view) return;

        view.Grid.Columns.Clear();
        view.Grid.ItemsSource = null;

        var result = (PreviewResult?)e.NewValue;
        if (result is null)
        {
            view.TruncationBanner.Visibility = Visibility.Collapsed;
            return;
        }

        for (var i = 0; i < result.ColumnNames.Count; i++)
        {
            var binding = new Binding($"[{i}]") { Mode = BindingMode.OneWay };
            view.Grid.Columns.Add(new DataGridTextColumn
            {
                Header = result.ColumnNames[i],
                Binding = binding,
            });
        }

        view.Grid.ItemsSource = result.Rows;
        view.TruncationBanner.Visibility = result.Truncated ? Visibility.Visible : Visibility.Collapsed;
    }
}
