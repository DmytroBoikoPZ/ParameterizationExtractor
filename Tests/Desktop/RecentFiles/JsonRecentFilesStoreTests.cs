#nullable enable
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Services.RecentFiles;
using Tests.Desktop.Fakes;

namespace Tests.Desktop.RecentFiles;

[TestFixture]
public class JsonRecentFilesStoreTests
{
    private string _tempDir = null!;
    private string _filePath = null!;

    [SetUp]
    public void CreateTempDir()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "buldozer-recents-" + Path.GetRandomFileName());
        _filePath = Path.Combine(_tempDir, "recent-files.json");
    }

    [TearDown]
    public void DeleteTempDir()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private JsonRecentFilesStore NewStore() =>
        new(_filePath, NullLogger<JsonRecentFilesStore>.Instance);

    [Test]
    public async Task GetAsync_FileMissing_ReturnsEmpty()
    {
        var sut = NewStore();

        var items = await sut.GetAsync();

        items.Should().BeEmpty();
    }

    [Test]
    public async Task PushAsync_FirstCall_CreatesFileWithSingleEntry()
    {
        var sut = NewStore();
        var workspace = Path.Combine(_tempDir, "ws.bws");

        await sut.PushAsync(workspace);

        File.Exists(_filePath).Should().BeTrue("PushAsync must create the parent directory and write the file");
        var items = await sut.GetAsync();
        items.Should().HaveCount(1);
        items[0].Path.Should().Be(Path.GetFullPath(workspace));
    }

    [Test]
    public async Task PushAsync_TwoDistinctPaths_BothPresentMostRecentFirst()
    {
        var sut = NewStore();
        var a = Path.Combine(_tempDir, "a.bws");
        var b = Path.Combine(_tempDir, "b.bws");

        await sut.PushAsync(a);
        await sut.PushAsync(b);

        var items = await sut.GetAsync();
        items.Should().HaveCount(2);
        items[0].Path.Should().Be(Path.GetFullPath(b));
        items[1].Path.Should().Be(Path.GetFullPath(a));
    }

    [Test]
    public async Task PushAsync_DuplicatePath_MovesToTop_DoesNotGrow()
    {
        var sut = NewStore();
        var a = Path.Combine(_tempDir, "a.bws");
        var b = Path.Combine(_tempDir, "b.bws");

        await sut.PushAsync(a);
        await sut.PushAsync(b);
        var firstA = (await sut.GetAsync()).Single(x => x.Path == Path.GetFullPath(a));

        await Task.Delay(10);
        await sut.PushAsync(a);

        var items = await sut.GetAsync();
        items.Should().HaveCount(2);
        items[0].Path.Should().Be(Path.GetFullPath(a));
        items[0].LastOpenedUtc.Should().BeAfter(firstA.LastOpenedUtc);
    }

    [Test]
    public async Task PushAsync_DuplicatePath_CaseInsensitive()
    {
        var sut = NewStore();
        var lower = Path.Combine(_tempDir, "case.bws");
        var upper = Path.Combine(_tempDir, "CASE.BWS");

        await sut.PushAsync(lower);
        await sut.PushAsync(upper);

        var items = await sut.GetAsync();
        items.Should().HaveCount(1);
    }

    [Test]
    public async Task PushAsync_BeyondMax_TrimsToFive()
    {
        var sut = NewStore();
        for (int i = 0; i < 6; i++)
        {
            await sut.PushAsync(Path.Combine(_tempDir, $"w{i}.bws"));
        }

        var items = await sut.GetAsync();
        items.Should().HaveCount(IRecentFilesStore.MaxItems);
        items.Select(x => x.Path).Should().NotContain(Path.GetFullPath(Path.Combine(_tempDir, "w0.bws")),
            "the oldest entry must be evicted when capacity is exceeded");
    }

    [Test]
    public async Task PushAsync_NonCanonicalPath_StoresCanonicalForm()
    {
        var sut = NewStore();
        var noisy = Path.Combine(_tempDir, "sub", "..", "x.bws");

        await sut.PushAsync(noisy);

        var items = await sut.GetAsync();
        items[0].Path.Should().NotContain("..").And.Be(Path.GetFullPath(noisy));
    }

    [Test]
    public async Task ClearAsync_RemovesAllEntries()
    {
        var sut = NewStore();
        await sut.PushAsync(Path.Combine(_tempDir, "a.bws"));
        await sut.PushAsync(Path.Combine(_tempDir, "b.bws"));

        await sut.ClearAsync();

        (await sut.GetAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task RoundTrip_SurvivesNewStoreInstance()
    {
        var first = NewStore();
        await first.PushAsync(Path.Combine(_tempDir, "persist.bws"));

        var second = NewStore();
        var items = await second.GetAsync();

        items.Should().HaveCount(1);
        items[0].Path.Should().Be(Path.GetFullPath(Path.Combine(_tempDir, "persist.bws")));
    }

    [Test]
    public async Task GetAsync_FileIsInvalidJson_ReturnsEmpty_LogsWarning()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(_filePath, "not json");
        var log = new CapturingLogger<JsonRecentFilesStore>();
        var sut = new JsonRecentFilesStore(_filePath, log);

        var items = await sut.GetAsync();

        items.Should().BeEmpty();
        log.Entries.Should().Contain(e => e.Level == LogLevel.Warning,
            "an invalid recents file is observable as a warning, not an exception");
    }
}
