using System.Collections.Immutable;

using Perch.Desktop.Models;

namespace Perch.Desktop.Services;

internal static class ProfileMatcher
{
    public static bool Matches(ImmutableArray<string> entryProfiles, IEnumerable<UserProfile> selectedProfiles)
    {
        foreach (var selected in selectedProfiles)
        {
            var profileString = ToGalleryString(selected);
            if (entryProfiles.Contains(profileString, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string ToGalleryString(UserProfile profile) => profile switch
    {
        UserProfile.PowerUser => "power-user",
        _ => profile.ToString().ToLowerInvariant(),
    };
}
