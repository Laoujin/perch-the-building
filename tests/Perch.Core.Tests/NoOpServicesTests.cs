using Perch.Core.Registry;
using Perch.Core.Startup;

namespace Perch.Core.Tests;

[TestFixture]
public sealed class NoOpRegistryProviderTests
{
    private NoOpRegistryProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _provider = new NoOpRegistryProvider();
    }

    [Test]
    public void GetValue_AnyKeyAndName_ReturnsNull()
    {
        var result = _provider.GetValue(@"HKCU\Software\Test", "SomeValue");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void SetValue_AnyArgs_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
            _provider.SetValue(@"HKCU\Software\Test", "SomeValue", "data", RegistryValueType.String));
    }

    [Test]
    public void EnumerateValues_AnyKeyPath_ReturnsEmpty()
    {
        var result = _provider.EnumerateValues(@"HKCU\Software\Test");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void DeleteValue_AnyKeyAndName_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
            _provider.DeleteValue(@"HKCU\Software\Test", "SomeValue"));
    }
}

[TestFixture]
public sealed class NoOpStartupServiceTests
{
    private NoOpStartupService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new NoOpStartupService();
    }

    [Test]
    public async Task GetAllAsync_ReturnsEmptyList()
    {
        var result = await _service.GetAllAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SetEnabledAsync_DoesNotThrow()
    {
        var entry = new StartupEntry("id", "name", "cmd", null, StartupSource.RegistryCurrentUser, true);

        await _service.SetEnabledAsync(entry, false);

        Assert.Pass();
    }

    [Test]
    public async Task RemoveAsync_DoesNotThrow()
    {
        var entry = new StartupEntry("id", "name", "cmd", null, StartupSource.StartupFolderUser, true);

        await _service.RemoveAsync(entry);

        Assert.Pass();
    }

    [Test]
    public async Task AddAsync_DoesNotThrow()
    {
        await _service.AddAsync("myapp", "myapp.exe --start", StartupSource.RegistryCurrentUser);

        Assert.Pass();
    }
}
