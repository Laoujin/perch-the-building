using System.Collections.Immutable;

using Perch.Desktop.Models;

namespace Perch.Desktop.Services;

public interface IGalleryDetectionService
{
    Task<GalleryDetectionResult> DetectAppsAsync(IReadOnlySet<UserProfile> selectedProfiles, CancellationToken cancellationToken = default);
    Task<ImmutableArray<TweakCardModel>> DetectTweaksAsync(IReadOnlySet<UserProfile> selectedProfiles, CancellationToken cancellationToken = default);
    Task<ImmutableArray<DotfileCardModel>> DetectDotfilesAsync(CancellationToken cancellationToken = default);
}

public sealed record GalleryDetectionResult(
    ImmutableArray<AppCardModel> YourApps,
    ImmutableArray<AppCardModel> Suggested,
    ImmutableArray<AppCardModel> OtherApps);
