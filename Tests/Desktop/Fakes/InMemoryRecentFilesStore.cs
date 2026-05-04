#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles;

namespace Tests.Desktop.Fakes;

/// <summary>
/// In-memory <see cref="IRecentFilesStore"/> that mirrors <c>JsonRecentFilesStore</c>
/// semantics — canonicalisation, case-insensitive de-dup, cap at <c>MaxItems</c> — without
/// touching the file system. Used by VM tests in step 03.
/// </summary>
internal sealed class InMemoryRecentFilesStore : IRecentFilesStore
{
    private readonly List<RecentFile> _items = new();

    public Task<IReadOnlyList<RecentFile>> GetAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RecentFile>>(_items.ToList());

    public Task PushAsync(string path, CancellationToken ct = default)
    {
        var canonical = Path.GetFullPath(path);
        _items.RemoveAll(x => string.Equals(x.Path, canonical, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, new RecentFile(canonical, DateTime.UtcNow));
        while (_items.Count > IRecentFilesStore.MaxItems)
        {
            _items.RemoveAt(_items.Count - 1);
        }
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _items.Clear();
        return Task.CompletedTask;
    }
}
