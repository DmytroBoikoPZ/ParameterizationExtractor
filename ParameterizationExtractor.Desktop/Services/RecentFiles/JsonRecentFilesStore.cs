using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quipu.ParameterizationExtractor.Logic.Configs.Json;

namespace Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles;

internal sealed class JsonRecentFilesStore : IRecentFilesStore
{
    private const string AppFolder = "SqlBuldozer";
    private const string FileName = "recent-files.json";

    private readonly string _filePath;
    private readonly ILogger<JsonRecentFilesStore> _log;

    public JsonRecentFilesStore(ILogger<JsonRecentFilesStore> log)
        : this(DefaultPath(), log) { }

    internal JsonRecentFilesStore(string filePath, ILogger<JsonRecentFilesStore> log)
    {
        _filePath = filePath;
        _log = log;
    }

    private static string DefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolder, FileName);

    public async Task<IReadOnlyList<RecentFile>> GetAsync(CancellationToken ct = default)
    {
        var state = await LoadStateAsync(ct).ConfigureAwait(false);
        return state.Items;
    }

    public async Task PushAsync(string path, CancellationToken ct = default)
    {
        var canonical = Path.GetFullPath(path);
        var state = await LoadStateAsync(ct).ConfigureAwait(false);

        var filtered = state.Items
            .Where(x => !string.Equals(x.Path, canonical, StringComparison.OrdinalIgnoreCase))
            .ToList();
        filtered.Insert(0, new RecentFile(canonical, DateTime.UtcNow));
        var trimmed = filtered.Take(IRecentFilesStore.MaxItems).ToList();

        await WriteStateAsync(new State(trimmed), ct).ConfigureAwait(false);
    }

    public Task ClearAsync(CancellationToken ct = default) =>
        WriteStateAsync(new State(Array.Empty<RecentFile>()), ct);

    private async Task<State> LoadStateAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return State.Empty;

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var state = await JsonSerializer.DeserializeAsync<State>(stream, JsonOptions.Default, ct)
                .ConfigureAwait(false);
            return state ?? State.Empty;
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "Recent files cache is invalid JSON at {Path}; treating as empty", _filePath);
            return State.Empty;
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Recent files cache could not be read at {Path}; treating as empty", _filePath);
            return State.Empty;
        }
    }

    private async Task WriteStateAsync(State state, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var tmp = _filePath + ".tmp";
            await using (var stream = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions.Default, ct).ConfigureAwait(false);
            }
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Recent files cache could not be written at {Path}", _filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogWarning(ex, "Recent files cache write was denied at {Path}", _filePath);
        }
    }

    private sealed record State(
        [property: JsonPropertyName("items")] IReadOnlyList<RecentFile> Items)
    {
        public static readonly State Empty = new(Array.Empty<RecentFile>());
    }
}
