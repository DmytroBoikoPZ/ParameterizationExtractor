namespace Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;

/// <summary>
/// Outcome of the New-workspace dialog. <see cref="Path"/> is the absolute path
/// of the freshly-written <c>.bws</c>; the caller passes it back to
/// <c>MainWindowViewModel.OpenWorkspaceAsync</c> to load.
/// </summary>
internal sealed record NewWorkspaceResult(string Path);
