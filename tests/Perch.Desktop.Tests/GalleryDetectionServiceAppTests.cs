using System.Collections.Immutable;
using System.Runtime.Versioning;

using Perch.Core;
using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Packages;
using Perch.Core.Scanner;
using Perch.Core.Symlinks;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.Tests;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class GalleryDetectionServiceAppTests
{
    private ICatalogService _catalog = null!;
    private IPlatformDetector _platformDetector = null!;
    private ISymlinkProvider _symlinkProvider = null!;
    private ISettingsProvider _settingsProvider = null!;
    private IPackageManagerProvider _packageProvider = null!;
    private GalleryDetectionService _service = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _catalog = Substitute.For<ICatalogService>();
        _platformDetector = Substitute.For<IPlatformDetector>();
        _symlinkProvider = Substitute.For<ISymlinkProvider>();
        _settingsProvider = Substitute.For<ISettingsProvider>();
        _packageProvider = Substitute.For<IPackageManagerProvider>();

        _platformDetector.CurrentPlatform.Returns(Platform.Windows);
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = @"C:\config" });

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true, [], null));

        _catalog.GetGitHubStarsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>() as IReadOnlyDictionary<string, int>);

        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _service = new GalleryDetectionService(
            _catalog,
            Substitute.For<IFontScanner>(),
            _platformDetector,
            _symlinkProvider,
            _settingsProvider,
            [_packageProvider],
            Substitute.For<ITweakService>(),
            Substitute.For<ILogger<GalleryDetectionService>>());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task DetectAppsAsync_AppDetectedByWingetId_InYourApps()
    {
        var app = MakeApp("vscode", "Visual Studio Code",
            install: new InstallDefinition("Microsoft.VisualStudioCode", null));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true,
                ImmutableArray.Create(new InstalledPackage("Microsoft.VisualStudioCode", PackageManager.Winget)),
                null));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.Multiple(() =>
        {
            Assert.That(result.YourApps, Has.Length.EqualTo(1));
            Assert.That(result.YourApps[0].Status, Is.EqualTo(CardStatus.Detected));
            Assert.That(result.Suggested, Is.Empty);
            Assert.That(result.OtherApps, Is.Empty);
        });
    }

    [Test]
    public async Task DetectAppsAsync_AppDetectedByChocoId_InYourApps()
    {
        var app = MakeApp("git", "Git",
            install: new InstallDefinition(null, "git"));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true,
                ImmutableArray.Create(new InstalledPackage("git", PackageManager.Chocolatey)),
                null));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.That(result.YourApps, Has.Length.EqualTo(1));
        Assert.That(result.YourApps[0].Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task DetectAppsAsync_AppDetectedByFileExists_InYourApps()
    {
        var targetFile = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(targetFile, "{}");

        var app = MakeApp("vscode", "Visual Studio Code",
            links: MakeLink("settings.json", targetFile));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.That(result.YourApps, Has.Length.EqualTo(1));
        Assert.That(result.YourApps[0].Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task DetectAppsAsync_AppDetectedAndSymlinked_StatusLinked()
    {
        var targetFile = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(targetFile, "{}");

        _symlinkProvider.IsSymlink(targetFile).Returns(true);

        var app = MakeApp("vscode", "Visual Studio Code",
            install: new InstallDefinition("Microsoft.VisualStudioCode", null),
            links: MakeLink("settings.json", targetFile));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true,
                ImmutableArray.Create(new InstalledPackage("Microsoft.VisualStudioCode", PackageManager.Winget)),
                null));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.That(result.YourApps[0].Status, Is.EqualTo(CardStatus.Linked));
    }

    [Test]
    public async Task DetectAppsAsync_AppNotDetected_NoConfigRepo_StatusNotInstalled()
    {
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = null });

        var app = MakeApp("vlc", "VLC", category: "Media/Players",
            install: new InstallDefinition("VideoLAN.VLC", null));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.Multiple(() =>
        {
            Assert.That(result.YourApps, Is.Empty);
            Assert.That(result.OtherApps, Has.Length.EqualTo(1));
            Assert.That(result.OtherApps[0].Status, Is.EqualTo(CardStatus.NotInstalled));
        });
    }

    [Test]
    public async Task DetectAppsAsync_NotDetectedButMatchesProfile_InSuggested()
    {
        var app = MakeApp("rider", "JetBrains Rider", category: "Development/IDEs",
            install: new InstallDefinition("JetBrains.Rider", null),
            profiles: ImmutableArray.Create("developer"));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.Multiple(() =>
        {
            Assert.That(result.Suggested, Has.Length.EqualTo(1));
            Assert.That(result.Suggested[0].Tier, Is.EqualTo(CardTier.Suggested));
            Assert.That(result.YourApps, Is.Empty);
        });
    }

    [Test]
    public async Task DetectAppsAsync_NotDetectedNoProfileMatch_InOther()
    {
        var app = MakeApp("vlc", "VLC Media Player", category: "Media/Players",
            install: new InstallDefinition("VideoLAN.VLC", null));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.Multiple(() =>
        {
            Assert.That(result.OtherApps, Has.Length.EqualTo(1));
            Assert.That(result.Suggested, Is.Empty);
        });
    }

    [Test]
    public async Task DetectAppsAsync_ProfileMatchesMultipleCategories()
    {
        var ide = MakeApp("rider", "Rider", category: "Development/IDEs",
            install: new InstallDefinition("JetBrains.Rider", null),
            profiles: ImmutableArray.Create("developer"));
        var terminal = MakeApp("wt", "Windows Terminal", category: "Development/Terminals",
            install: new InstallDefinition("Microsoft.WindowsTerminal", null),
            profiles: ImmutableArray.Create("developer"));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(ide, terminal));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.That(result.Suggested, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task DetectAppsAsync_DetectedAppNotInSuggested_EvenWhenProfileMatches()
    {
        var app = MakeApp("rider", "JetBrains Rider", category: "Development/IDEs",
            install: new InstallDefinition("JetBrains.Rider", null),
            profiles: ImmutableArray.Create("developer"));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true,
                ImmutableArray.Create(new InstalledPackage("JetBrains.Rider", PackageManager.Winget)),
                null));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.Multiple(() =>
        {
            Assert.That(result.YourApps, Has.Length.EqualTo(1));
            Assert.That(result.Suggested, Is.Empty);
        });
    }

    [Test]
    public async Task DetectAppsAsync_PackageProviderUnavailable_GracefullyHandled()
    {
        var app = MakeApp("vlc", "VLC", category: "Media/Players",
            install: new InstallDefinition("VideoLAN.VLC", null));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PackageManagerScanResult>(new InvalidOperationException("winget not found")));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        Assert.That(result.OtherApps, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task DetectAllAppsAsync_ReturnsAllAppsWithStatus()
    {
        var detected = MakeApp("vscode", "VS Code",
            install: new InstallDefinition("Microsoft.VisualStudioCode", null));
        var notInstalled = MakeApp("rider", "Rider",
            install: new InstallDefinition("JetBrains.Rider", null));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(detected, notInstalled));

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true,
                ImmutableArray.Create(new InstalledPackage("Microsoft.VisualStudioCode", PackageManager.Winget)),
                null));

        var result = await _service.DetectAllAppsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Length.EqualTo(2));
            Assert.That(result[0].Status, Is.EqualTo(CardStatus.Detected));
            Assert.That(result[1].Status, Is.EqualTo(CardStatus.NotInstalled));
        });
    }

    [Test]
    public async Task DetectAppsAsync_LinkedRequiresConfigRepoPath()
    {
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = null });

        var targetFile = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(targetFile, "{}");
        _symlinkProvider.IsSymlink(targetFile).Returns(true);

        var app = MakeApp("vscode", "VS Code",
            install: new InstallDefinition("Microsoft.VisualStudioCode", null),
            links: MakeLink("settings.json", targetFile));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true,
                ImmutableArray.Create(new InstalledPackage("Microsoft.VisualStudioCode", PackageManager.Winget)),
                null));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        // Detected but not Linked because configRepoPath is null
        Assert.That(result.YourApps[0].Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task DetectAppsAsync_HidesPureConfigDotfiles()
    {
        var dotfileWithInstall = new CatalogEntry(
            "powershell", "PowerShell", null, "Development/Languages",
            [], null, null, null,
            new InstallDefinition("Microsoft.PowerShell", null),
            null, null, CatalogKind.Dotfile);

        var dotfileWithoutInstall = new CatalogEntry(
            "nuget-config", "NuGet Config", null, "Development/.NET",
            [], null, null, null,
            null, null, null, CatalogKind.Dotfile);

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(dotfileWithInstall, dotfileWithoutInstall));

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        var allCards = result.YourApps.AsEnumerable().Concat(result.Suggested).Concat(result.OtherApps).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(allCards, Has.Count.EqualTo(1));
            Assert.That(allCards[0].Id, Is.EqualTo("powershell"));
        });
    }

    [Test]
    public async Task DetectAppsAsync_SetsGitHubStarsOnCards()
    {
        var app = MakeApp("vscode", "Visual Studio Code",
            install: new InstallDefinition("Microsoft.VisualStudioCode", null));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        _catalog.GetGitHubStarsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int> { ["vscode"] = 181800 } as IReadOnlyDictionary<string, int>);

        var result = await _service.DetectAppsAsync(new HashSet<UserProfile> { UserProfile.Developer });

        var allCards = result.YourApps.AsEnumerable().Concat(result.Suggested).Concat(result.OtherApps);
        var card = allCards.First(c => c.Id == "vscode");
        Assert.That(card.GitHubStars, Is.EqualTo(181800));
    }

    [Test]
    public async Task DetectAllAppsAsync_SetsGitHubStarsOnCards()
    {
        var app = MakeApp("vscode", "VS Code",
            install: new InstallDefinition("Microsoft.VisualStudioCode", null));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(app));

        _catalog.GetGitHubStarsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int> { ["vscode"] = 181800 } as IReadOnlyDictionary<string, int>);

        var result = await _service.DetectAllAppsAsync();

        Assert.That(result[0].GitHubStars, Is.EqualTo(181800));
    }

    private static CatalogConfigLink MakeLink(string source, string target) =>
        new(source,
            new Dictionary<Platform, string>
            {
                [Platform.Windows] = target,
            }.ToImmutableDictionary());

    private static CatalogEntry MakeApp(
        string id,
        string name,
        string category = "Development/Tools",
        InstallDefinition? install = null,
        ImmutableArray<string> profiles = default,
        params CatalogConfigLink[] links)
    {
        var config = links.Length > 0
            ? new CatalogConfigDefinition(links.ToImmutableArray())
            : null;
        return new CatalogEntry(
            id, name, null, category,
            [], null, null, null, install, config, null,
            CatalogKind.App, Profiles: profiles);
    }
}
