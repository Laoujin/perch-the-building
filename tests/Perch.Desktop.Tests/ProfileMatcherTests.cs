using System.Collections.Immutable;
using System.Runtime.Versioning;

using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.Tests;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class ProfileMatcherTests
{
    [Test]
    public void Matches_MatchingProfile_ReturnsTrue()
    {
        var profiles = ImmutableArray.Create("developer");
        Assert.That(ProfileMatcher.Matches(profiles, [UserProfile.Developer]), Is.True);
    }

    [Test]
    public void Matches_NoMatchingProfile_ReturnsFalse()
    {
        var profiles = ImmutableArray.Create("gamer");
        Assert.That(ProfileMatcher.Matches(profiles, [UserProfile.Developer]), Is.False);
    }

    [Test]
    public void Matches_MultipleSelected_AnyMatches()
    {
        var profiles = ImmutableArray.Create("gamer");
        Assert.That(ProfileMatcher.Matches(profiles, [UserProfile.Developer, UserProfile.Gamer]), Is.True);
    }

    [Test]
    public void Matches_CaseInsensitive()
    {
        var profiles = ImmutableArray.Create("DEVELOPER");
        Assert.That(ProfileMatcher.Matches(profiles, [UserProfile.Developer]), Is.True);
    }

    [Test]
    public void Matches_EmptySelected_ReturnsFalse()
    {
        var profiles = ImmutableArray.Create("developer");
        Assert.That(ProfileMatcher.Matches(profiles, Array.Empty<UserProfile>()), Is.False);
    }

    [Test]
    public void ToGalleryString_Developer_ReturnsLowercase()
    {
        Assert.That(ProfileMatcher.ToGalleryString(UserProfile.Developer), Is.EqualTo("developer"));
    }

    [Test]
    public void ToGalleryString_PowerUser_ReturnsDashed()
    {
        Assert.That(ProfileMatcher.ToGalleryString(UserProfile.PowerUser), Is.EqualTo("power-user"));
    }

    [Test]
    public void ToGalleryString_Gamer_ReturnsLowercase()
    {
        Assert.That(ProfileMatcher.ToGalleryString(UserProfile.Gamer), Is.EqualTo("gamer"));
    }

    [Test]
    public void ToGalleryString_Casual_ReturnsLowercase()
    {
        Assert.That(ProfileMatcher.ToGalleryString(UserProfile.Casual), Is.EqualTo("casual"));
    }

    [Test]
    public void Matches_PowerUser_MatchesHyphenated()
    {
        var profiles = ImmutableArray.Create("power-user");
        Assert.That(ProfileMatcher.Matches(profiles, [UserProfile.PowerUser]), Is.True);
    }
}
