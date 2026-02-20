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
public sealed class GalleryDetectionServiceFontTests
{
    private ICatalogService _catalog = null!;
    private IFontScanner _fontScanner = null!;
    private IPackageManagerProvider _packageProvider = null!;
    private GalleryDetectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _catalog = Substitute.For<ICatalogService>();
        _fontScanner = Substitute.For<IFontScanner>();
        _packageProvider = Substitute.For<IPackageManagerProvider>();
        var settingsProvider = Substitute.For<ISettingsProvider>();
        settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = @"C:\config" });

        var platformDetector = Substitute.For<IPlatformDetector>();
        platformDetector.CurrentPlatform.Returns(Platform.Windows);

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true, [], null));

        _fontScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<DetectedFont>.Empty);

        _catalog.GetAllFontsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<FontCatalogEntry>.Empty);

        _service = new GalleryDetectionService(
            _catalog,
            _fontScanner,
            platformDetector,
            Substitute.For<ISymlinkProvider>(),
            settingsProvider,
            [_packageProvider],
            Substitute.For<ITweakService>(),
            Substitute.For<ILogger<GalleryDetectionService>>());
    }

    [Test]
    public async Task DetectFontsAsync_SystemFontNotInGallery_InDetected()
    {
        _fontScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                new DetectedFont("MyCustomFont", "MyCustomFont", @"C:\Users\test\Fonts\MyCustomFont.ttf")));

        var result = await _service.DetectFontsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.InstalledFonts, Has.Length.EqualTo(1));
            Assert.That(result.InstalledFonts[0].Name, Is.EqualTo("MyCustomFont"));
            Assert.That(result.InstalledFonts[0].Source, Is.EqualTo(FontCardSource.Detected));
            Assert.That(result.InstalledFonts[0].Status, Is.EqualTo(CardStatus.Detected));
        });
    }

    [Test]
    public async Task DetectFontsAsync_GalleryFontNotInstalled_InNerdFontsAsNotInstalled()
    {
        _catalog.GetAllFontsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                MakeGalleryFont("fira-code-nerd", "FiraCode Nerd Font",
                    choco: "nerd-fonts-FiraCode")));

        var result = await _service.DetectFontsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.NerdFonts, Has.Length.EqualTo(1));
            Assert.That(result.NerdFonts[0].Status, Is.EqualTo(CardStatus.Unmanaged));
            Assert.That(result.NerdFonts[0].Source, Is.EqualTo(FontCardSource.Gallery));
        });
    }

    [Test]
    public async Task DetectFontsAsync_GalleryFontInstalledByPackage_MarkedDetected()
    {
        _catalog.GetAllFontsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                MakeGalleryFont("fira-code-nerd", "FiraCode Nerd Font",
                    choco: "nerd-fonts-FiraCode")));

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true,
                ImmutableArray.Create(new InstalledPackage("nerd-fonts-FiraCode", PackageManager.Chocolatey)),
                null));

        var result = await _service.DetectFontsAsync();

        Assert.That(result.NerdFonts[0].Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task DetectFontsAsync_GalleryFontMatchedBySystemScan_NerdFontMarkedDetected()
    {
        _catalog.GetAllFontsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                MakeGalleryFont("fira-code-nerd", "FiraCode Nerd Font")));

        _fontScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                new DetectedFont("FiraCode Nerd Font", "FiraCode Nerd Font",
                    @"C:\Users\test\AppData\Local\Microsoft\Windows\Fonts\FiraCodeNerdFont-Regular.ttf")));

        var result = await _service.DetectFontsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.NerdFonts[0].Status, Is.EqualTo(CardStatus.Detected));
            Assert.That(result.InstalledFonts, Has.Length.EqualTo(1),
                "Name-matched gallery font appears in installed list with gallery metadata");
        });
    }

    [Test]
    public async Task DetectFontsAsync_PackageInstalledFont_ExcludedFromDetectedList()
    {
        _catalog.GetAllFontsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                MakeGalleryFont("cascadia-code", "Cascadia Code",
                    winget: "Microsoft.CascadiaCode")));

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true,
                ImmutableArray.Create(new InstalledPackage("Microsoft.CascadiaCode", PackageManager.Winget)),
                null));

        _fontScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                new DetectedFont("Cascadia Code", "Cascadia Code",
                    @"C:\Windows\Fonts\CascadiaCode.ttf")));

        var result = await _service.DetectFontsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.InstalledFonts, Is.Empty,
                "Package-installed gallery font should not appear in detected list");
            Assert.That(result.NerdFonts[0].Status, Is.EqualTo(CardStatus.Detected));
        });
    }

    [Test]
    public async Task DetectFontsAsync_CatalogLoadFailure_ReturnsSystemFontsOnly()
    {
        _catalog.GetAllFontsAsync(Arg.Any<CancellationToken>())
            .Returns<ImmutableArray<FontCatalogEntry>>(x =>
                throw new InvalidOperationException("Network error"));

        _fontScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                new DetectedFont("MyCustomFont", "MyCustomFont", @"C:\Users\test\Fonts\MyCustomFont.ttf")));

        var result = await _service.DetectFontsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.InstalledFonts, Has.Length.EqualTo(1));
            Assert.That(result.NerdFonts, Is.Empty);
        });
    }

    [Test]
    public async Task DetectFontsAsync_NormalizesNamesForMatching()
    {
        _catalog.GetAllFontsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                MakeGalleryFont("jetbrains-mono", "JetBrains Mono")));

        _fontScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                new DetectedFont("JetBrainsMono", "JetBrains Mono",
                    @"C:\Windows\Fonts\JetBrainsMono-Regular.ttf")));

        var result = await _service.DetectFontsAsync();

        Assert.That(result.NerdFonts[0].Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task DetectFontsAsync_HyphenatedFileName_MatchesGalleryByFamily()
    {
        _catalog.GetAllFontsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                MakeGalleryFont("fira-code", "Fira Code", choco: "firacode")));

        _fontScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                new DetectedFont("FiraCode-Regular", "FiraCode",
                    @"C:\Users\test\AppData\Local\Microsoft\Windows\Fonts\FiraCode-Regular.ttf")));

        var result = await _service.DetectFontsAsync();

        Assert.That(result.NerdFonts[0].Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task DetectFontsAsync_EmptyEverything_ReturnsEmptyResults()
    {
        var result = await _service.DetectFontsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.InstalledFonts, Is.Empty);
            Assert.That(result.NerdFonts, Is.Empty);
        });
    }

    private static FontCatalogEntry MakeGalleryFont(
        string id,
        string name,
        string? winget = null,
        string? choco = null) =>
        new(id, name, "Fonts", [], null, null, null,
            winget is not null || choco is not null
                ? new InstallDefinition(winget, choco)
                : null);
}
