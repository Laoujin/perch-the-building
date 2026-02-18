#if DESKTOP_TESTS
using System.Collections.Immutable;
using System.Runtime.Versioning;

using NSubstitute;

using Perch.Core;
using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Modules;
using Perch.Core.Scanner;
using Perch.Core.Symlinks;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class GalleryDetectionServiceDotfileTests
{
    private ICatalogService _catalog = null!;
    private IPlatformDetector _platformDetector = null!;
    private ISymlinkProvider _symlinkProvider = null!;
    private ISettingsProvider _settingsProvider = null!;
    private GalleryDetectionService _service = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _catalog = Substitute.For<ICatalogService>();
        _platformDetector = Substitute.For<IPlatformDetector>();
        _symlinkProvider = Substitute.For<ISymlinkProvider>();
        _settingsProvider = Substitute.For<ISettingsProvider>();

        _platformDetector.CurrentPlatform.Returns(Platform.Windows);
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = @"C:\config" });

        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _service = new GalleryDetectionService(
            _catalog,
            Substitute.For<IFontScanner>(),
            _platformDetector,
            _symlinkProvider,
            _settingsProvider,
            [],
            Substitute.For<ITweakService>());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task DetectDotfilesAsync_OnlyReturnsDotfileKindEntries()
    {
        var dotfileEntry = MakeEntry("git", "Git", CatalogKind.Dotfile,
            MakeLink(".gitconfig", @"C:\nonexistent\.gitconfig"));

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(dotfileEntry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("git"));
        await _catalog.DidNotReceive().GetAllAppsAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DetectDotfilesAsync_GroupsFilesByEntry()
    {
        var file1 = Path.Combine(_tempDir, ".gitconfig");
        var file2 = Path.Combine(_tempDir, ".gitignore_global");
        File.WriteAllText(file1, "");
        File.WriteAllText(file2, "");

        var entry = MakeEntry("git", "Git", CatalogKind.Dotfile,
            MakeLink(".gitconfig", file1),
            MakeLink(".gitignore_global", file2));

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(entry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task DetectDotfilesAsync_AllLinked_GroupStatusLinked()
    {
        var file1 = Path.Combine(_tempDir, ".gitconfig");
        var file2 = Path.Combine(_tempDir, ".gitignore_global");
        File.WriteAllText(file1, "");
        File.WriteAllText(file2, "");

        _symlinkProvider.IsSymlink(file1).Returns(true);
        _symlinkProvider.IsSymlink(file2).Returns(true);

        var entry = MakeEntry("git", "Git", CatalogKind.Dotfile,
            MakeLink(".gitconfig", file1),
            MakeLink(".gitignore_global", file2));

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(entry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result[0].Status, Is.EqualTo(CardStatus.Linked));
    }

    [Test]
    public async Task DetectDotfilesAsync_MixedStatus_GroupStatusDetected()
    {
        var file1 = Path.Combine(_tempDir, ".gitconfig");
        var file2 = Path.Combine(_tempDir, ".gitignore_global");
        File.WriteAllText(file1, "");
        File.WriteAllText(file2, "");

        _symlinkProvider.IsSymlink(file1).Returns(true);
        _symlinkProvider.IsSymlink(file2).Returns(false);

        var entry = MakeEntry("git", "Git", CatalogKind.Dotfile,
            MakeLink(".gitconfig", file1),
            MakeLink(".gitignore_global", file2));

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(entry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result[0].Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task DetectDotfilesAsync_NoConfigLinks_SkipsEntry()
    {
        var entry = new CatalogEntry(
            "git", "Git", null, "Development",
            [], null, null, null, null, null, null,
            CatalogKind.Dotfile);

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(entry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task DetectDotfilesAsync_WrongPlatform_SkipsLink()
    {
        _platformDetector.CurrentPlatform.Returns(Platform.Windows);

        var linuxOnlyLink = new CatalogConfigLink(
            ".bashrc",
            new Dictionary<Platform, string>
            {
                [Platform.Linux] = "~/.bashrc",
            }.ToImmutableDictionary(),
            LinkType.Symlink,
            ImmutableArray.Create(Platform.Linux));

        var config = new CatalogConfigDefinition(ImmutableArray.Create(linuxOnlyLink));
        var entry = new CatalogEntry(
            "bash", "Bash", null, "Development",
            [], null, null, null, null, config, null,
            CatalogKind.Dotfile);

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(entry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task DetectDotfilesAsync_SymlinkPointsOutsideConfigRepo_StatusDrift()
    {
        var configDir = Path.Combine(_tempDir, "config");
        var outsideDir = Path.Combine(_tempDir, "outside");
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(outsideDir);

        var sourceFile = Path.Combine(outsideDir, ".gitconfig");
        File.WriteAllText(sourceFile, "");

        var symlinkPath = Path.Combine(_tempDir, ".gitconfig");
        File.CreateSymbolicLink(symlinkPath, sourceFile);

        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = configDir });
        _symlinkProvider.IsSymlink(symlinkPath).Returns(true);

        var entry = MakeEntry("git", "Git", CatalogKind.Dotfile,
            MakeLink(".gitconfig", symlinkPath));

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(entry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result[0].Status, Is.EqualTo(CardStatus.Drift));
    }

    [Test]
    public async Task DetectDotfilesAsync_SymlinkPointsInsideConfigRepo_StatusLinked()
    {
        var configDir = Path.Combine(_tempDir, "config");
        Directory.CreateDirectory(configDir);

        var sourceFile = Path.Combine(configDir, "git", ".gitconfig");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(sourceFile, "");

        var symlinkPath = Path.Combine(_tempDir, ".gitconfig");
        File.CreateSymbolicLink(symlinkPath, sourceFile);

        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = configDir });
        _symlinkProvider.IsSymlink(symlinkPath).Returns(true);

        var entry = MakeEntry("git", "Git", CatalogKind.Dotfile,
            MakeLink(".gitconfig", symlinkPath));

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(entry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result[0].Status, Is.EqualTo(CardStatus.Linked));
    }

    [Test]
    public async Task DetectDotfilesAsync_NullConfigRepoPath_SkipsDriftCheck_StaysLinked()
    {
        var sourceFile = Path.Combine(_tempDir, "source", ".gitconfig");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(sourceFile, "");

        var symlinkPath = Path.Combine(_tempDir, ".gitconfig");
        File.CreateSymbolicLink(symlinkPath, sourceFile);

        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = null });
        _symlinkProvider.IsSymlink(symlinkPath).Returns(true);

        var entry = MakeEntry("git", "Git", CatalogKind.Dotfile,
            MakeLink(".gitconfig", symlinkPath));

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(entry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result[0].Status, Is.EqualTo(CardStatus.Linked));
    }

    [Test]
    public async Task DetectDotfilesAsync_OneDriftFile_GroupStatusDrift()
    {
        var configDir = Path.Combine(_tempDir, "config");
        var outsideDir = Path.Combine(_tempDir, "outside");
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(outsideDir);
        Directory.CreateDirectory(Path.Combine(configDir, "git"));

        var insideSource = Path.Combine(configDir, "git", ".gitconfig");
        File.WriteAllText(insideSource, "");
        var outsideSource = Path.Combine(outsideDir, ".gitignore_global");
        File.WriteAllText(outsideSource, "");

        var symlink1 = Path.Combine(_tempDir, ".gitconfig");
        var symlink2 = Path.Combine(_tempDir, ".gitignore_global");
        File.CreateSymbolicLink(symlink1, insideSource);
        File.CreateSymbolicLink(symlink2, outsideSource);

        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = configDir });
        _symlinkProvider.IsSymlink(symlink1).Returns(true);
        _symlinkProvider.IsSymlink(symlink2).Returns(true);

        var entry = MakeEntry("git", "Git", CatalogKind.Dotfile,
            MakeLink(".gitconfig", symlink1),
            MakeLink(".gitignore_global", symlink2));

        _catalog.GetAllDotfileAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(entry));

        var result = await _service.DetectDotfilesAsync();

        Assert.That(result[0].Status, Is.EqualTo(CardStatus.Drift));
    }

    private static CatalogConfigLink MakeLink(string source, string target) =>
        new(source,
            new Dictionary<Platform, string>
            {
                [Platform.Windows] = target,
            }.ToImmutableDictionary());

    private static CatalogEntry MakeEntry(string id, string name, CatalogKind kind,
        params CatalogConfigLink[] links)
    {
        var config = new CatalogConfigDefinition(links.ToImmutableArray());
        return new CatalogEntry(
            id, name, null, "Development",
            [], null, null, null, null, config, null,
            kind);
    }
}
#endif
