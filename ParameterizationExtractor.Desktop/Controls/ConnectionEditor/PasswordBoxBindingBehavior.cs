using System.Windows;
using System.Windows.Controls;

namespace Quipu.ParameterizationExtractor.Desktop.Controls.ConnectionEditor;

/// <summary>
/// Attached behaviour that two-way-binds <see cref="PasswordBox.Password"/> (which is not a
/// dependency property) to a string. Use as: <c>local:PasswordBoxBindingBehavior.Password="{Binding Password, Mode=TwoWay}"</c>.
/// Plaintext stays in the binding source for the lifetime of the dialog only — see ADR-010.
/// </summary>
internal static class PasswordBoxBindingBehavior
{
    // defaultValue: null (NOT string.Empty) so that when the binding pushes "" from the VM,
    // WPF observes a change (null → "") and fires OnPasswordChanged exactly once — that's
    // where we subscribe to PasswordBox.PasswordChanged. With string.Empty as default, the
    // initial bind is a no-op (""→""), the callback never fires, the subscription never happens,
    // and typing in the box is silently dropped.
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
        "Password",
        typeof(string),
        typeof(PasswordBoxBindingBehavior),
        new FrameworkPropertyMetadata(
            defaultValue: null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
        "IsUpdating",
        typeof(bool),
        typeof(PasswordBoxBindingBehavior));

    public static string GetPassword(DependencyObject obj) => (string)obj.GetValue(PasswordProperty);
    public static void SetPassword(DependencyObject obj, string value) => obj.SetValue(PasswordProperty, value);

    private static bool GetIsUpdating(DependencyObject obj) => (bool)obj.GetValue(IsUpdatingProperty);
    private static void SetIsUpdating(DependencyObject obj, bool value) => obj.SetValue(IsUpdatingProperty, value);

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;

        box.PasswordChanged -= OnPasswordBoxChanged;
        if (!GetIsUpdating(box))
        {
            box.Password = (string)(e.NewValue ?? string.Empty);
        }
        box.PasswordChanged += OnPasswordBoxChanged;
    }

    private static void OnPasswordBoxChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox box) return;
        SetIsUpdating(box, true);
        SetPassword(box, box.Password);
        SetIsUpdating(box, false);
    }
}
