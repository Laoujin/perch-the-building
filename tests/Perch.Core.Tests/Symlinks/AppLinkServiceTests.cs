using System.Collections.Immutable;

using Perch.Core.Backup;
using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Symlinks;

namespace Perch.Core.Tests.Symlinks;

[TestFixture]
public sealed class AppLinkServiceTests
{
    private ISymlinkProvider _symlinkProvider = null!;
    private IFileBackupProvider _backupProvider = null!;
    private IFileLockDetector _fileLockDetector = null!;
    private IPlatformDetector _platformDetector = null!;
    private ISettingsProvider _settingsProvider = null!;
    private AppLinkService _service = null!;
    private string _configRepoPath = null!;

    [SetUp]
    public void SetUp()
    {
        _symlinkProvider = Substitute.For<ISymlinkProvider>();
        _backupProvider = Substitute.For<IFileBackupProvider>();
        _fileLockDetector = Substitute.For<IFileLockDetector>();
        _platformDetector = Substitute.For<IPlatformDetector>();
        _settingsProvider = Substitute.For<ISettingsProvider>();

        _platformDetector.CurrentPlatform.Returns(Platform.Windows);
        _configRepoPath = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = _configRepoPath });

        var orchestrator = new SymlinkOrchestrator(_symlinkProvider, _backupProvider, _fileLockDetector);
        _service = new AppLinkService(orchestrator, _symlinkProvider, _platformDetector, _settingsProvider);
    }

    private static CatalogEntry MakeApp(params (string source, string target)[] links)
    {
        var configLinks = links.Select(l => new CatalogConfigLink(
            l.source,
            ImmutableDictionary<Platform, string>.Empty.Add(Platform.Windows, l.target)
        )).ToImmutableArray();

        return new CatalogEntry(
            "test-app", "test-app", "Test App", "Development/IDEs",
            ImmutableArray<string>.Empty, null, null, null, null,
            new CatalogConfigDefinition(configLinks), null);
    }

    [Test]
    public async Task LinkAppAsync_CreatesSymlinksForAllConfigLinks()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target1 = Path.Combine(tempDir, "settings.json");
        string target2 = Path.Combine(tempDir, "keybindings.json");
        var app = MakeApp(("vscode/settings.json", target1), ("vscode/keybindings.json", target2));

        try
        {
            var results = await _service.LinkAppAsync(app);

            Assert.Multiple(() =>
            {
                Assert.That(results, Has.Count.EqualTo(2));
                Assert.That(results.All(r => r.Level == ResultLevel.Ok), Is.True);
            });
            _symlinkProvider.Received(2).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task LinkAppAsync_SkipsLinksWithoutCurrentPlatformTarget()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");

        var linuxOnlyLink = new CatalogConfigLink(
            "bash/.bashrc",
            ImmutableDictionary<Platform, string>.Empty.Add(Platform.Linux, "~/.bashrc"));
        var windowsLink = new CatalogConfigLink(
            "vscode/settings.json",
            ImmutableDictionary<Platform, string>.Empty.Add(Platform.Windows, target));

        var app = new CatalogEntry(
            "test-app", "test-app", "Test App", "Development/IDEs",
            ImmutableArray<string>.Empty, null, null, null, null,
            new CatalogConfigDefinition(ImmutableArray.Create(linuxOnlyLink, windowsLink)), null);

        try
        {
            var results = await _service.LinkAppAsync(app);

            Assert.That(results, Has.Count.EqualTo(1));
            _symlinkProvider.Received(1).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task LinkAppAsync_NoConfig_ReturnsEmpty()
    {
        var app = new CatalogEntry(
            "test-app", "test-app", "Test App", "Development/IDEs",
            ImmutableArray<string>.Empty, null, null, null, null, null, null);

        var results = await _service.LinkAppAsync(app);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task UnlinkAppAsync_DeletesSymlinksForAllConfigLinks()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target1 = Path.Combine(tempDir, "settings.json");
        string target2 = Path.Combine(tempDir, "keybindings.json");
        var app = MakeApp(("vscode/settings.json", target1), ("vscode/keybindings.json", target2));

        _symlinkProvider.IsSymlink(target1).Returns(true);
        _symlinkProvider.IsSymlink(target2).Returns(true);

        // Create dummy files so File.Delete works
        File.WriteAllText(target1, "");
        File.WriteAllText(target2, "");

        try
        {
            var results = await _service.UnlinkAppAsync(app);

            Assert.Multiple(() =>
            {
                Assert.That(results, Has.Count.EqualTo(2));
                Assert.That(results.All(r => r.Level == ResultLevel.Ok), Is.True);
                Assert.That(results.All(r => r.Message.Contains("Unlinked")), Is.True);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task UnlinkAppAsync_SkipsNonSymlinks()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        File.WriteAllText(target, "real file");
        var app = MakeApp(("vscode/settings.json", target));

        _symlinkProvider.IsSymlink(target).Returns(false);

        try
        {
            var results = await _service.UnlinkAppAsync(app);

            Assert.Multiple(() =>
            {
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].Level, Is.EqualTo(ResultLevel.Ok));
                Assert.That(results[0].Message, Does.Contain("Not a symlink"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task FixAppLinksAsync_RelinksWhenTargetIsBroken()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        string expectedSource = Path.Combine(_configRepoPath, "vscode", "settings.json");
        var app = MakeApp(("vscode/settings.json", target));

        _symlinkProvider.IsSymlink(target).Returns(true);
        _symlinkProvider.GetSymlinkTarget(target).Returns("C:\\old\\gone\\settings.json");

        try
        {
            var results = await _service.FixAppLinksAsync(app);

            Assert.Multiple(() =>
            {
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].Level, Is.EqualTo(ResultLevel.Ok));
                Assert.That(results[0].Message, Does.Contain("Relinked"));
            });
            _symlinkProvider.Received(1).CreateSymlink(target, expectedSource);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task FixAppLinksAsync_SkipsCorrectlyLinked()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        string expectedSource = Path.Combine(_configRepoPath, "vscode", "settings.json");
        var app = MakeApp(("vscode/settings.json", target));

        _symlinkProvider.IsSymlink(target).Returns(true);
        _symlinkProvider.GetSymlinkTarget(target).Returns(expectedSource);

        try
        {
            var results = await _service.FixAppLinksAsync(app);

            Assert.Multiple(() =>
            {
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].Level, Is.EqualTo(ResultLevel.Ok));
                Assert.That(results[0].Message, Does.Contain("Already correct"));
            });
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task FixAppLinksAsync_SkipsNonSymlinks()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        var app = MakeApp(("vscode/settings.json", target));

        _symlinkProvider.IsSymlink(target).Returns(false);

        try
        {
            var results = await _service.FixAppLinksAsync(app);

            Assert.Multiple(() =>
            {
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].Level, Is.EqualTo(ResultLevel.Ok));
                Assert.That(results[0].Message, Does.Contain("Not a symlink"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
