using Perch.Core.Catalog;

namespace Perch.Core.Tests.Catalog;

[TestFixture]
public sealed class NoOpCatalogCacheTests
{
    private NoOpCatalogCache _cache = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = new NoOpCatalogCache();
    }

    [Test]
    public async Task GetAsync_AlwaysReturnsNull()
    {
        await _cache.SetAsync("key", "value");

        string? result = await _cache.GetAsync("key");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task SetAsync_DoesNotThrow()
    {
        Assert.DoesNotThrowAsync(() => _cache.SetAsync("any-key", "any-content"));
    }

    [Test]
    public void InvalidateAll_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _cache.InvalidateAll());
    }
}
