using Perch.Core.Modules;

namespace Perch.Core.Tests.Modules;

[TestFixture]
public sealed class ModuleDiscoveryServiceTests
{
    private string _tempDir = null!;
    private ModuleDiscoveryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new ModuleDiscoveryService(new ManifestParser());
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
    public async Task DiscoverAsync_MultipleModules_ReturnsAll()
    {
        CreateModule("git", """
            links:
              - source: .gitconfig
                target: "C:\\Users\\test\\.gitconfig"
            """);
        CreateModule("vscode", """
            display-name: Visual Studio Code
            links:
              - source: settings.json
                target: "%APPDATA%\\Code\\User\\settings.json"
            """);

        DiscoveryResult result = await _service.DiscoverAsync(_tempDir);

        Assert.Multiple(() =>
        {
            Assert.That(result.Modules, Has.Length.EqualTo(2));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Modules[0].Name, Is.EqualTo("git"));
            Assert.That(result.Modules[1].Name, Is.EqualTo("vscode"));
            Assert.That(result.Modules[1].DisplayName, Is.EqualTo("Visual Studio Code"));
        });
    }

    [Test]
    public async Task DiscoverAsync_FolderWithoutManifest_IsIgnored()
    {
        CreateModule("git", """
            links:
              - source: .gitconfig
                target: "C:\\Users\\test\\.gitconfig"
            """);
        Directory.CreateDirectory(Path.Combine(_tempDir, "no-manifest"));

        DiscoveryResult result = await _service.DiscoverAsync(_tempDir);

        Assert.That(result.Modules, Has.Length.EqualTo(1));
        Assert.That(result.Modules[0].Name, Is.EqualTo("git"));
    }

    [Test]
    public async Task DiscoverAsync_InvalidManifest_ReportsErrorAndContinues()
    {
        CreateModule("good", """
            links:
              - source: config
                target: "C:\\test\\config"
            """);
        CreateModule("bad", "{{invalid yaml");

        DiscoveryResult result = await _service.DiscoverAsync(_tempDir);

        Assert.Multiple(() =>
        {
            Assert.That(result.Modules, Has.Length.EqualTo(1));
            Assert.That(result.Modules[0].Name, Is.EqualTo("good"));
            Assert.That(result.Errors, Has.Length.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("bad"));
        });
    }

    [Test]
    public async Task DiscoverAsync_EmptyDirectory_ReturnsEmpty()
    {
        DiscoveryResult result = await _service.DiscoverAsync(_tempDir);

        Assert.Multiple(() =>
        {
            Assert.That(result.Modules, Is.Empty);
            Assert.That(result.Errors, Is.Empty);
        });
    }

    [Test]
    public void DiscoverAsync_Cancellation_Throws()
    {
        CreateModule("git", """
            links:
              - source: .gitconfig
                target: "C:\\Users\\test\\.gitconfig"
            """);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.DiscoverAsync(_tempDir, cts.Token));
    }

    [Test]
    public async Task DiscoverAsync_UnreadableManifest_ReportsErrorAndContinues()
    {
        CreateModule("good", """
            links:
              - source: config
                target: "C:\\test\\config"
            """);
        CreateModule("locked", """
            links:
              - source: data
                target: "C:\\test\\data"
            """);

        var lockedPath = Path.Combine(_tempDir, "locked", "manifest.yaml");
        using var lockHandle = File.Open(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);

        DiscoveryResult result = await _service.DiscoverAsync(_tempDir);

        Assert.Multiple(() =>
        {
            Assert.That(result.Modules, Has.Length.EqualTo(1));
            Assert.That(result.Modules[0].Name, Is.EqualTo("good"));
            Assert.That(result.Errors, Has.Length.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("locked"));
            Assert.That(result.Errors[0], Does.Contain("Failed to read manifest"));
        });
    }

    private void CreateModule(string name, string manifestYaml)
    {
        string moduleDir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, "manifest.yaml"), manifestYaml);
    }
}
