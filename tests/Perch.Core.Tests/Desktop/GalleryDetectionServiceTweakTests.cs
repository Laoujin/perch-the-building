#if DESKTOP_TESTS
using System.Collections.Immutable;
using System.Runtime.Versioning;

using NSubstitute;

using Perch.Core;
using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Scanner;
using Perch.Core.Symlinks;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class GalleryDetectionServiceTweakTests
{
    private ICatalogService _catalog = null!;
    private ITweakService _tweakService = null!;
    private GalleryDetectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _catalog = Substitute.For<ICatalogService>();
        _tweakService = Substitute.For<ITweakService>();
        var settingsProvider = Substitute.For<ISettingsProvider>();
        settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = @"C:\config" });

        var platformDetector = Substitute.For<IPlatformDetector>();
        platformDetector.CurrentPlatform.Returns(Platform.Windows);

        _tweakService.Detect(Arg.Any<TweakCatalogEntry>())
            .Returns(new TweakDetectionResult(TweakStatus.NotApplied, ImmutableArray<RegistryEntryStatus>.Empty));

        _service = new GalleryDetectionService(
            _catalog,
            Substitute.For<IFontScanner>(),
            platformDetector,
            Substitute.For<ISymlinkProvider>(),
            settingsProvider,
            [],
            _tweakService);
    }

    [Test]
    public async Task DetectTweaksAsync_ReturnsTweaksMatchingDeveloperProfile()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("dark-mode", "Dark Mode", ["developer", "power-user"]),
            MakeTweak("game-bar", "Disable Game Bar", ["gamer"]));

        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(tweaks);

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.Developer });

        Assert.Multiple(() =>
        {
            Assert.That(result.Tweaks, Has.Length.EqualTo(1));
            Assert.That(result.Tweaks[0].Id, Is.EqualTo("dark-mode"));
        });
    }

    [Test]
    public async Task DetectTweaksAsync_NoProfilesOnTweak_IncludedForAll()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("universal", "Universal Tweak", []));

        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(tweaks);

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.Gamer });

        Assert.That(result.Tweaks, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task DetectTweaksAsync_EmptyCatalog_ReturnsEmpty()
    {
        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<TweakCatalogEntry>.Empty);

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.Developer });

        Assert.That(result.Tweaks, Is.Empty);
    }

    [Test]
    public async Task DetectTweaksAsync_PowerUserProfile_MatchesPowerUserTweaks()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("perf", "Performance Tweak", ["power-user"]));

        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(tweaks);

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.PowerUser });

        Assert.That(result.Tweaks, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task DetectTweaksAsync_MultipleProfiles_UnionOfMatches()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("dev-tweak", "Dev Tweak", ["developer"]),
            MakeTweak("game-tweak", "Game Tweak", ["gamer"]),
            MakeTweak("casual-tweak", "Casual Tweak", ["casual"]));

        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(tweaks);

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.Developer, UserProfile.Gamer });

        Assert.That(result.Tweaks, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task DetectTweaksAsync_NotApplied_MapsToNotInstalled()
    {
        var tweak = MakeTweak("tweak1", "Tweak 1", []);
        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(tweak));

        _tweakService.Detect(tweak)
            .Returns(new TweakDetectionResult(TweakStatus.NotApplied, ImmutableArray<RegistryEntryStatus>.Empty));

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.Developer });

        Assert.That(result.Tweaks[0].Status, Is.EqualTo(CardStatus.NotInstalled));
    }

    [Test]
    public async Task DetectTweaksAsync_Applied_MapsToDetected()
    {
        var tweak = MakeTweakWithRegistry("tweak1", "Tweak 1");
        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(tweak));

        var entryStatus = new RegistryEntryStatus(tweak.Registry[0], 1, true);
        _tweakService.Detect(tweak)
            .Returns(new TweakDetectionResult(TweakStatus.Applied, [entryStatus]));

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.Developer });

        Assert.Multiple(() =>
        {
            Assert.That(result.Tweaks[0].Status, Is.EqualTo(CardStatus.Detected));
            Assert.That(result.Tweaks[0].AppliedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task DetectTweaksAsync_Partial_MapsToDrift()
    {
        var tweak = MakeTweakWithTwoEntries("tweak1", "Tweak 1");
        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(tweak));

        var entries = ImmutableArray.Create(
            new RegistryEntryStatus(tweak.Registry[0], 1, true),
            new RegistryEntryStatus(tweak.Registry[1], 99, false));
        _tweakService.Detect(tweak)
            .Returns(new TweakDetectionResult(TweakStatus.Partial, entries));

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.Developer });

        Assert.Multiple(() =>
        {
            Assert.That(result.Tweaks[0].Status, Is.EqualTo(CardStatus.Drift));
            Assert.That(result.Tweaks[0].AppliedCount, Is.EqualTo(1));
        });
    }

    private static TweakCatalogEntry MakeTweak(string id, string name, string[] profiles) =>
        new(id, name, "System", [], null, true,
            profiles.ToImmutableArray(), []);

    private static TweakCatalogEntry MakeTweakWithRegistry(string id, string name) =>
        new(id, name, "System", [], null, true, [],
            [new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord)]);

    private static TweakCatalogEntry MakeTweakWithTwoEntries(string id, string name) =>
        new(id, name, "System", [], null, true, [],
            [
                new RegistryEntryDefinition(@"HKCU\Software\Test", "A", 1, RegistryValueType.DWord),
                new RegistryEntryDefinition(@"HKCU\Software\Test", "B", 2, RegistryValueType.DWord),
            ]);
}
#endif
