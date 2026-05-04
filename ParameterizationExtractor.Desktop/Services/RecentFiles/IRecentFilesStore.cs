using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles;

/// <summary>
/// Per-user recent-workspaces store. The default impl persists to
/// <c>%APPDATA%\SqlBuldozer\recent-files.json</c>. The store is intentionally tolerant
/// of disk errors — recents is a UX cache, not authoritative state. First impl landed
/// by the <c>desktop-startup-and-open-workspace</c> feature.
/// </summary>
internal interface IRecentFilesStore
{
    public const int MaxItems = 5;

    /// <summary>Returns the recent files, most-recent first, capped at <see cref="MaxItems"/>.</summary>
    Task<IReadOnlyList<RecentFile>> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds (or moves to top) the given path. Path is canonicalised internally via
    /// <c>Path.GetFullPath</c>; case-insensitive de-duplication on Windows.
    /// On disk-write failure, logs a warning and silently swallows.
    /// </summary>
    Task PushAsync(string path, CancellationToken ct = default);

    /// <summary>Empties the list and persists.</summary>
    Task ClearAsync(CancellationToken ct = default);
}
