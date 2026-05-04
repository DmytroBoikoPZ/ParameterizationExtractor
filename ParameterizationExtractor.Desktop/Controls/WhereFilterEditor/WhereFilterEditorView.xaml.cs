using System.Windows;
using System.Windows.Controls;

namespace Quipu.ParameterizationExtractor.Desktop.Controls.WhereFilterEditor;

internal partial class WhereFilterEditorView : UserControl
{
    public WhereFilterEditorView()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(WhereFilterEditorView),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
