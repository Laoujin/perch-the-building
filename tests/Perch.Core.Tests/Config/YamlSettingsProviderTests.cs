using Perch.Core.Config;

namespace Perch.Core.Tests.Config;

[TestFixture]
public sealed class YamlSettingsProviderTests
{
    private string _tempDir = null!;
    private string _settingsPath = null!;
    private YamlSettingsProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.yaml");
        _provider = new YamlSettingsProvider(_settingsPath);
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
    public async Task LoadAsync_FileMissing_ReturnsDefaults()
    {
        PerchSettings result = await _provider.LoadAsync();

        Assert.That(result.ConfigRepoPath, Is.Null);
    }

    [Test]
    public async Task SaveAsync_CreatesDirectoryAndFile()
    {
        string nestedDir = Path.Combine(_tempDir, "sub", "dir");
        string nestedPath = Path.Combine(nestedDir, "settings.yaml");
        var provider = new YamlSettingsProvider(nestedPath);
        var settings = new PerchSettings { ConfigRepoPath = "C:\\config" };

        await provider.SaveAsync(settings);

        Assert.That(File.Exists(nestedPath), Is.True);
    }

    [Test]
    public async Task Roundtrip_SaveAndLoad_PreservesValues()
    {
        var settings = new PerchSettings { ConfigRepoPath = "C:\\my\\config" };

        await _provider.SaveAsync(settings);
        PerchSettings loaded = await _provider.LoadAsync();

        Assert.That(loaded.ConfigRepoPath, Is.EqualTo("C:\\my\\config"));
    }

    [Test]
    public async Task LoadAsync_InvalidYaml_ReturnsDefaults()
    {
        await File.WriteAllTextAsync(_settingsPath, "{{invalid yaml::");

        PerchSettings result = await _provider.LoadAsync();

        Assert.That(result.ConfigRepoPath, Is.Null);
    }

    [Test]
    public async Task LoadAsync_NoGallerySettings_ReturnsDefaults()
    {
        PerchSettings result = await _provider.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.GalleryUrl, Is.EqualTo("https://laoujin.github.io/perch-gallery/"));
            Assert.That(result.GalleryLocalPath, Is.Null);
        });
    }

    [Test]
    public async Task Roundtrip_GallerySettings_PreservesValues()
    {
        var settings = new PerchSettings
        {
            ConfigRepoPath = "C:\\config",
            GalleryUrl = "https://custom.gallery/",
            GalleryLocalPath = "C:\\gallery-local"
        };

        await _provider.SaveAsync(settings);
        PerchSettings loaded = await _provider.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.GalleryUrl, Is.EqualTo("https://custom.gallery/"));
            Assert.That(loaded.GalleryLocalPath, Is.EqualTo("C:\\gallery-local"));
        });
    }

    [Test]
    public async Task LoadAsync_GalleryLocalPathOnly_UrlKeepsDefault()
    {
        await File.WriteAllTextAsync(_settingsPath, "gallery-local-path: C:\\local-gallery\n");

        PerchSettings result = await _provider.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.GalleryUrl, Is.EqualTo("https://laoujin.github.io/perch-gallery/"));
            Assert.That(result.GalleryLocalPath, Is.EqualTo("C:\\local-gallery"));
        });
    }

    [Test]
    public async Task LoadAsync_LocalOverridesBase()
    {
        await File.WriteAllTextAsync(_settingsPath,
            "config-repo-path: C:\\base-config\ngallery-url: https://base.gallery/\n");
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "settings.local.yaml"),
            "gallery-local-path: C:\\local-gallery\ndisable-gallery-cache: true\n");

        PerchSettings result = await _provider.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.ConfigRepoPath, Is.EqualTo("C:\\base-config"));
            Assert.That(result.GalleryUrl, Is.EqualTo("https://base.gallery/"));
            Assert.That(result.GalleryLocalPath, Is.EqualTo("C:\\local-gallery"));
            Assert.That(result.DisableGalleryCache, Is.True);
        });
    }

    [Test]
    public async Task LoadAsync_LocalOverridesBaseProperty()
    {
        await File.WriteAllTextAsync(_settingsPath,
            "config-repo-path: C:\\base-config\ngallery-url: https://base.gallery/\n");
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "settings.local.yaml"),
            "config-repo-path: C:\\local-config\n");

        PerchSettings result = await _provider.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.ConfigRepoPath, Is.EqualTo("C:\\local-config"));
            Assert.That(result.GalleryUrl, Is.EqualTo("https://base.gallery/"));
        });
    }

    [Test]
    public async Task LoadAsync_NoLocalFile_UsesBaseOnly()
    {
        await File.WriteAllTextAsync(_settingsPath,
            "config-repo-path: C:\\config\ndev: true\n");

        PerchSettings result = await _provider.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.ConfigRepoPath, Is.EqualTo("C:\\config"));
            Assert.That(result.Dev, Is.True);
        });
    }

    [Test]
    public async Task LoadAsync_InvalidLocalYaml_UsesBaseOnly()
    {
        await File.WriteAllTextAsync(_settingsPath, "config-repo-path: C:\\config\n");
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "settings.local.yaml"),
            "{{broken yaml::");

        PerchSettings result = await _provider.LoadAsync();

        Assert.That(result.ConfigRepoPath, Is.EqualTo("C:\\config"));
    }

    [Test]
    public void DiscoverConfigRepoFromSymlink_NotSymlink_ReturnsNull()
    {
        File.WriteAllText(_settingsPath, "config-repo-path: C:\\config\n");

        Assert.That(_provider.DiscoverConfigRepoFromSymlink(), Is.Null);
    }

    [Test]
    public void DiscoverConfigRepoFromSymlink_Symlink_ReturnsConfigRepoRoot()
    {
        var configRepo = Path.Combine(_tempDir, "fake-config-repo");
        var moduleDir = Path.Combine(configRepo, "perch");
        Directory.CreateDirectory(moduleDir);
        var sourceFile = Path.Combine(moduleDir, "settings.yaml");
        File.WriteAllText(sourceFile, "profiles:\n  - developer\n");

        try
        {
            File.CreateSymbolicLink(_settingsPath, sourceFile);
        }
        catch (UnauthorizedAccessException)
        {
            Assert.Ignore("Symlink creation requires elevated privileges or Developer Mode");
            return;
        }
        catch (IOException)
        {
            Assert.Ignore("Symlink creation not supported in this environment");
            return;
        }

        var result = _provider.DiscoverConfigRepoFromSymlink();

        Assert.That(result, Is.EqualTo(configRepo));
    }

    [Test]
    public async Task LoadAsync_SymlinkedSettings_AutoDiscoversConfigRepo()
    {
        var configRepo = Path.Combine(_tempDir, "fake-config-repo");
        var moduleDir = Path.Combine(configRepo, "perch");
        Directory.CreateDirectory(moduleDir);
        var sourceFile = Path.Combine(moduleDir, "settings.yaml");
        File.WriteAllText(sourceFile, "profiles:\n  - developer\n");

        try
        {
            File.CreateSymbolicLink(_settingsPath, sourceFile);
        }
        catch (UnauthorizedAccessException)
        {
            Assert.Ignore("Symlink creation requires elevated privileges or Developer Mode");
            return;
        }
        catch (IOException)
        {
            Assert.Ignore("Symlink creation not supported in this environment");
            return;
        }

        PerchSettings result = await _provider.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.ConfigRepoPath, Is.EqualTo(configRepo));
            Assert.That(result.Profiles, Is.EqualTo(new List<string> { "developer" }));
        });
    }

    [Test]
    public async Task LoadAsync_SymlinkedSettings_ExplicitConfigRepoTakesPrecedence()
    {
        var configRepo = Path.Combine(_tempDir, "fake-config-repo");
        var moduleDir = Path.Combine(configRepo, "perch");
        Directory.CreateDirectory(moduleDir);
        var sourceFile = Path.Combine(moduleDir, "settings.yaml");
        File.WriteAllText(sourceFile, "config-repo-path: C:\\explicit-config\n");

        try
        {
            File.CreateSymbolicLink(_settingsPath, sourceFile);
        }
        catch (UnauthorizedAccessException)
        {
            Assert.Ignore("Symlink creation requires elevated privileges or Developer Mode");
            return;
        }
        catch (IOException)
        {
            Assert.Ignore("Symlink creation not supported in this environment");
            return;
        }

        PerchSettings result = await _provider.LoadAsync();

        Assert.That(result.ConfigRepoPath, Is.EqualTo("C:\\explicit-config"));
    }
}
