using System;

namespace Quipu.ParameterizationExtractor.Desktop.Controls.GraphHost;

/// <summary>
/// XAML-friendly enumeration of <see cref="GraphLayoutKind"/> values for binding to a ComboBox.
/// Lives next to the enum so the Layout picker can be data-driven without a converter.
/// </summary>
internal static class GraphLayoutKindValues
{
    public static GraphLayoutKind[] All { get; } =
        (GraphLayoutKind[])Enum.GetValues(typeof(GraphLayoutKind));
}
