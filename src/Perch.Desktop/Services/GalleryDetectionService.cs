using System.Collections.Immutable;

using Perch.Core;
using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Scanner;
using Perch.Core.Symlinks;
using Perch.Desktop.Models;

namespace Perch.Desktop.Services;

public sealed class GalleryDetectionService : IGalleryDetectionService
{
    private readonly ICatalogService _catalog;
    private readonly IDotfileScanner _dotfileScanner;
    private readonly IPlatformDetector _platformDetector;
    private readonly ISymlinkProvider _symlinkProvider;
    private readonly ISettingsProvider _settingsProvider;

    // Category-to-profile mapping for "Suggested" tier
    private static readonly Dictionary<string, UserProfile[]> _profileCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Development/IDEs"] = [UserProfile.Developer],
        ["Development/Version Control"] = [UserProfile.Developer],
        ["Development/Terminals"] = [UserProfile.Developer, UserProfile.PowerUser],
        ["Development/Tools"] = [UserProfile.Developer],
        ["Development/Languages"] = [UserProfile.Developer],
        ["System/Utilities"] = [UserProfile.PowerUser],
        ["System/Productivity"] = [UserProfile.PowerUser],
        ["Media/Players"] = [UserProfile.Gamer, UserProfile.Casual],
        ["Gaming"] = [UserProfile.Gamer],
        ["Communication"] = [UserProfile.Casual],
    };

    public GalleryDetectionService(
        ICatalogService catalog,
        IDotfileScanner dotfileScanner,
        IPlatformDetector platformDetector,
        ISymlinkProvider symlinkProvider,
        ISettingsProvider settingsProvider)
    {
        _catalog = catalog;
        _dotfileScanner = dotfileScanner;
        _platformDetector = platformDetector;
        _symlinkProvider = symlinkProvider;
        _settingsProvider = settingsProvider;
    }

    public async Task<GalleryDetectionResult> DetectAppsAsync(
        IReadOnlySet<UserProfile> selectedProfiles,
        CancellationToken cancellationToken = default)
    {
        var allApps = await _catalog.GetAllAppsAsync(cancellationToken);
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        var platform = _platformDetector.CurrentPlatform;

        var yourApps = ImmutableArray.CreateBuilder<AppCardModel>();
        var suggested = ImmutableArray.CreateBuilder<AppCardModel>();
        var other = ImmutableArray.CreateBuilder<AppCardModel>();

        foreach (var app in allApps)
        {
            var detected = IsAppDetectedOnFilesystem(app, platform);
            var linked = detected && IsAppLinked(app, platform, settings.ConfigRepoPath);

            CardStatus status;
            if (linked) status = CardStatus.Linked;
            else if (detected) status = CardStatus.Detected;
            else status = CardStatus.NotInstalled;

            if (detected)
            {
                yourApps.Add(new AppCardModel(app, CardTier.YourApps, status));
            }
            else if (IsSuggestedForProfiles(app, selectedProfiles))
            {
                suggested.Add(new AppCardModel(app, CardTier.Suggested, status));
            }
            else
            {
                other.Add(new AppCardModel(app, CardTier.Other, status));
            }
        }

        return new GalleryDetectionResult(
            yourApps.ToImmutable(),
            suggested.ToImmutable(),
            other.ToImmutable());
    }

    public async Task<ImmutableArray<TweakCardModel>> DetectTweaksAsync(
        IReadOnlySet<UserProfile> selectedProfiles,
        CancellationToken cancellationToken = default)
    {
        var allTweaks = await _catalog.GetAllTweaksAsync(cancellationToken);
        var builder = ImmutableArray.CreateBuilder<TweakCardModel>();

        foreach (var tweak in allTweaks)
        {
            var model = new TweakCardModel(tweak, CardStatus.NotInstalled);
            if (model.MatchesProfile(selectedProfiles))
            {
                builder.Add(model);
            }
        }

        return builder.ToImmutable();
    }

    public async Task<ImmutableArray<DotfileCardModel>> DetectDotfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var dotfiles = await _dotfileScanner.ScanAsync(cancellationToken);
        return dotfiles.Select(d => new DotfileCardModel(d)).ToImmutableArray();
    }

    private bool IsAppDetectedOnFilesystem(CatalogEntry app, Platform platform)
    {
        if (app.Config is null || app.Config.Links.IsDefaultOrEmpty)
            return false;

        foreach (var link in app.Config.Links)
        {
            if (!link.Targets.TryGetValue(platform, out var targetPath))
                continue;

            var resolved = Environment.ExpandEnvironmentVariables(targetPath.Replace('/', '\\'));
            if (File.Exists(resolved) || Directory.Exists(resolved))
                return true;
        }

        return false;
    }

    private bool IsAppLinked(CatalogEntry app, Platform platform, string? configRepoPath)
    {
        if (app.Config is null || string.IsNullOrEmpty(configRepoPath))
            return false;

        foreach (var link in app.Config.Links)
        {
            if (!link.Targets.TryGetValue(platform, out var targetPath))
                continue;

            var resolved = Environment.ExpandEnvironmentVariables(targetPath.Replace('/', '\\'));
            if (_symlinkProvider.IsSymlink(resolved))
                return true;
        }

        return false;
    }

    private static bool IsSuggestedForProfiles(CatalogEntry app, IReadOnlySet<UserProfile> profiles)
    {
        foreach (var (categoryPrefix, matchingProfiles) in _profileCategoryMap)
        {
            if (app.Category.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (matchingProfiles.Any(profiles.Contains))
                    return true;
            }
        }

        return false;
    }
}
