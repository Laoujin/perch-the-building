using Perch.Core.Catalog;

namespace Perch.Core.Tests.Catalog;

[TestFixture]
public sealed class FileCatalogCacheTests
{
    private string _tempDir = null!;
    private FileCatalogCache _cache = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-cache-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _cache = new FileCatalogCache(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public async Task GetAsync_KeyNotCached_ReturnsNull()
    {
        string? result = await _cache.GetAsync("nonexistent.yaml");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task SetAsync_ThenGetAsync_ReturnsContent()
    {
        await _cache.SetAsync("apps/vscode.yaml", "name: VS Code");

        string? result = await _cache.GetAsync("apps/vscode.yaml");

        Assert.That(result, Is.EqualTo("name: VS Code"));
    }

    [Test]
    public async Task SetAsync_CreatesSubdirectories()
    {
        await _cache.SetAsync("deep/nested/file.yaml", "content");

        string expectedPath = Path.Combine(_tempDir, "deep", "nested", "file.yaml");
        Assert.That(File.Exists(expectedPath), Is.True);
    }

    [Test]
    public async Task InvalidateAll_DeletesCacheDirectory()
    {
        await _cache.SetAsync("apps/vscode.yaml", "content");

        _cache.InvalidateAll();

        Assert.That(Directory.Exists(_tempDir), Is.False);
    }

    [Test]
    public void InvalidateAll_NoCacheDir_DoesNotThrow()
    {
        var cache = new FileCatalogCache(Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}"));

        Assert.DoesNotThrow(() => cache.InvalidateAll());
    }
}
