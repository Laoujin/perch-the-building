using System.Collections.Immutable;

using Perch.Desktop.Models;

namespace Perch.Desktop.Services;

public interface IGalleryDetectionService
{
    Task WarmUpAsync(CancellationToken cancellationToken = default);
    Task<GalleryDetectionResult> DetectAppsAsync(IReadOnlySet<UserProfile> selectedProfiles, CancellationToken cancellationToken = default);
    Task<ImmutableArray<AppCardModel>> DetectAllAppsAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<TweakCardModel>> DetectTweaksAsync(IReadOnlySet<UserProfile> selectedProfiles, CancellationToken cancellationToken = default);
    Task<ImmutableArray<DotfileGroupCardModel>> DetectDotfilesAsync(CancellationToken cancellationToken = default);
    Task<FontDetectionResult> DetectFontsAsync(CancellationToken cancellationToken = default);
}

public sealed record GalleryDetectionResult(
    ImmutableArray<AppCardModel> YourApps,
    ImmutableArray<AppCardModel> Suggested,
    ImmutableArray<AppCardModel> OtherApps);

public sealed record FontDetectionResult(
    ImmutableArray<FontCardModel> InstalledFonts,
    ImmutableArray<FontCardModel> NerdFonts);
