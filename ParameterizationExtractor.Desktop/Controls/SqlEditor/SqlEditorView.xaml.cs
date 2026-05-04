using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace Quipu.ParameterizationExtractor.Desktop.Controls.SqlEditor;

/// <summary>
/// AvalonEdit wrapper. AvalonEdit types live ONLY inside this control's code-behind;
/// ViewModels see the <see cref="Text"/> / <see cref="IsReadOnly"/> / <see cref="IsSingleLine"/>
/// dependency properties (per the wpf-desktop recipe + tripwire).
/// </summary>
internal partial class SqlEditorView : UserControl
{
    private bool _isUpdating;

    public SqlEditorView()
    {
        InitializeComponent();
        Editor.TextChanged += OnEditorTextChanged;
        Editor.PreviewKeyDown += OnEditorPreviewKeyDown;
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(SqlEditorView),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTextDpChanged));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly),
        typeof(bool),
        typeof(SqlEditorView),
        new FrameworkPropertyMetadata(false, OnIsReadOnlyChanged));

    public static readonly DependencyProperty IsSingleLineProperty = DependencyProperty.Register(
        nameof(IsSingleLine),
        typeof(bool),
        typeof(SqlEditorView),
        new FrameworkPropertyMetadata(false, OnIsSingleLineChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool IsSingleLine
    {
        get => (bool)GetValue(IsSingleLineProperty);
        set => SetValue(IsSingleLineProperty, value);
    }

    private static void OnTextDpChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SqlEditorView view || view._isUpdating) return;
        view._isUpdating = true;
        view.Editor.Text = (string?)e.NewValue ?? string.Empty;
        view._isUpdating = false;
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SqlEditorView view) return;
        view.Editor.IsReadOnly = (bool)e.NewValue;
    }

    private static void OnIsSingleLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SqlEditorView view) return;
        var single = (bool)e.NewValue;
        view.Editor.ShowLineNumbers = !single;
    }

    private void OnEditorTextChanged(object? sender, System.EventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        SetValue(TextProperty, Editor.Text);
        _isUpdating = false;
    }

    private void OnEditorPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Single-line mode: swallow Enter so the editor stays one row.
        if (IsSingleLine && e.Key == Key.Enter)
        {
            e.Handled = true;
        }
    }
}
