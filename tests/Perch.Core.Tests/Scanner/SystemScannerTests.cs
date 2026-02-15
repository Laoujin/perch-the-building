using System.Collections.Immutable;

using NSubstitute;

using Perch.Core.Packages;
using Perch.Core.Scanner;

namespace Perch.Core.Tests.Scanner;

[TestFixture]
public sealed class SystemScannerTests
{
    private IDotfileScanner _dotfileScanner = null!;
    private IFontScanner _fontScanner = null!;
    private IVsCodeService _vsCodeService = null!;
    private IPackageManagerProvider _packageProvider = null!;
    private SystemScanner _scanner = null!;

    [SetUp]
    public void SetUp()
    {
        _dotfileScanner = Substitute.For<IDotfileScanner>();
        _fontScanner = Substitute.For<IFontScanner>();
        _vsCodeService = Substitute.For<IVsCodeService>();
        _packageProvider = Substitute.For<IPackageManagerProvider>();
        _scanner = new SystemScanner(_dotfileScanner, _fontScanner, _vsCodeService, [_packageProvider]);
    }

    [Test]
    public async Task ScanAsync_AggregatesAllScanners()
    {
        var dotfiles = ImmutableArray.Create(
            new DetectedDotfile(".gitconfig", "/home/user/.gitconfig", "Git", 100, DateTime.UtcNow, false));
        _dotfileScanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(dotfiles);

        var fonts = ImmutableArray.Create(
            new DetectedFont("JetBrainsMono", null, "/usr/share/fonts/JetBrainsMono.ttf"));
        _fontScanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(fonts);

        _vsCodeService.IsInstalled.Returns(true);
        var extensions = ImmutableArray.Create(
            new DetectedVsCodeExtension("ms-python.python", null, "2024.1.0"));
        _vsCodeService.GetInstalledExtensionsAsync(Arg.Any<CancellationToken>()).Returns(extensions);

        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(true, ImmutableArray.Create(new InstalledPackage("git", PackageManager.Winget)), null));

        var result = await _scanner.ScanAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Dotfiles, Has.Length.EqualTo(1));
            Assert.That(result.InstalledFonts, Has.Length.EqualTo(1));
            Assert.That(result.VsCodeExtensions, Has.Length.EqualTo(1));
            Assert.That(result.InstalledPackages, Has.Length.EqualTo(1));
            Assert.That(result.VsCodeDetected, Is.True);
        });
    }

    [Test]
    public async Task ScanAsync_VsCodeNotInstalled_SkipsExtensions()
    {
        _dotfileScanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(ImmutableArray<DetectedDotfile>.Empty);
        _fontScanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(ImmutableArray<DetectedFont>.Empty);
        _vsCodeService.IsInstalled.Returns(false);
        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(false, ImmutableArray<InstalledPackage>.Empty, null));

        var result = await _scanner.ScanAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.VsCodeDetected, Is.False);
            Assert.That(result.VsCodeExtensions, Is.Empty);
        });
        await _vsCodeService.DidNotReceive().GetInstalledExtensionsAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ScanAsync_ReportsProgress()
    {
        _dotfileScanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(ImmutableArray<DetectedDotfile>.Empty);
        _fontScanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(ImmutableArray<DetectedFont>.Empty);
        _vsCodeService.IsInstalled.Returns(false);
        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(false, ImmutableArray<InstalledPackage>.Empty, null));

        var messages = new List<string>();
        var progress = Substitute.For<IProgress<string>>();
        progress.When(p => p.Report(Arg.Any<string>())).Do(ci => messages.Add(ci.Arg<string>()));

        await _scanner.ScanAsync(progress);

        Assert.That(messages, Is.Not.Empty);
    }

    [Test]
    public async Task ScanAsync_PackageProviderError_CollectsWarning()
    {
        _dotfileScanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(ImmutableArray<DetectedDotfile>.Empty);
        _fontScanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(ImmutableArray<DetectedFont>.Empty);
        _vsCodeService.IsInstalled.Returns(false);
        _packageProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(new PackageManagerScanResult(false, ImmutableArray<InstalledPackage>.Empty, "choco not found"));

        var result = await _scanner.ScanAsync();

        Assert.That(result.Warnings, Has.Length.EqualTo(1));
        Assert.That(result.Warnings[0], Is.EqualTo("choco not found"));
    }
}
