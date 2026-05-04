using System;

namespace Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles;

/// <summary>
/// One entry in the recent-workspaces list. <see cref="Path"/> is the canonicalised
/// full path (output of <c>Path.GetFullPath</c>); callers don't need to canonicalise themselves.
/// </summary>
internal sealed record RecentFile(string Path, DateTime LastOpenedUtc);
