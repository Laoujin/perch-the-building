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
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class GalleryDetectionServiceTweakTests
{
    private ICatalogService _catalog = null!;
    private GalleryDetectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _catalog = Substitute.For<ICatalogService>();
        var settingsProvider = Substitute.For<ISettingsProvider>();
        settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = @"C:\config" });

        var platformDetector = Substitute.For<IPlatformDetector>();
        platformDetector.CurrentPlatform.Returns(Platform.Windows);

        _service = new GalleryDetectionService(
            _catalog,
            Substitute.For<IFontScanner>(),
            platformDetector,
            Substitute.For<ISymlinkProvider>(),
            settingsProvider,
            []);
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
            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo("dark-mode"));
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

        Assert.That(result, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task DetectTweaksAsync_EmptyCatalog_ReturnsEmpty()
    {
        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<TweakCatalogEntry>.Empty);

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.Developer });

        Assert.That(result, Is.Empty);
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

        Assert.That(result, Has.Length.EqualTo(1));
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

        Assert.That(result, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task DetectTweaksAsync_AllTweaksHaveNotInstalledStatus()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Tweak 1", []),
            MakeTweak("tweak2", "Tweak 2", []));

        _catalog.GetAllTweaksAsync(Arg.Any<CancellationToken>())
            .Returns(tweaks);

        var result = await _service.DetectTweaksAsync(
            new HashSet<UserProfile> { UserProfile.Developer });

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Status, Is.EqualTo(CardStatus.NotInstalled));
            Assert.That(result[1].Status, Is.EqualTo(CardStatus.NotInstalled));
        });
    }

    private static TweakCatalogEntry MakeTweak(string id, string name, string[] profiles) =>
        new(id, name, "System", [], null, true,
            profiles.ToImmutableArray(), []);
}
#endif
