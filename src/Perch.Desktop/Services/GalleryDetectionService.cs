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
    private readonly IFontScanner _fontScanner;
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
        IFontScanner fontScanner,
        IPlatformDetector platformDetector,
        ISymlinkProvider symlinkProvider,
        ISettingsProvider settingsProvider)
    {
        _catalog = catalog;
        _dotfileScanner = dotfileScanner;
        _fontScanner = fontScanner;
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

    public async Task<ImmutableArray<AppCardModel>> DetectAllAppsAsync(
        CancellationToken cancellationToken = default)
    {
        var allApps = await _catalog.GetAllAppsAsync(cancellationToken);
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        var platform = _platformDetector.CurrentPlatform;
        var builder = ImmutableArray.CreateBuilder<AppCardModel>();

        foreach (var app in allApps)
        {
            var status = ResolveStatus(app, platform, settings.ConfigRepoPath);
            builder.Add(new AppCardModel(app, CardTier.Other, status));
        }

        return builder.ToImmutable();
    }

    private CardStatus ResolveStatus(CatalogEntry app, Platform platform, string? configRepoPath)
    {
        var detected = IsAppDetectedOnFilesystem(app, platform);
        var linked = detected && IsAppLinked(app, platform, configRepoPath);
        if (linked) return CardStatus.Linked;
        if (detected) return CardStatus.Detected;
        return CardStatus.NotInstalled;
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
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        var configRepoPath = settings.ConfigRepoPath;

        return dotfiles
            .Select(d =>
            {
                var model = new DotfileCardModel(d);
                if (d.IsSymlink && !string.IsNullOrEmpty(configRepoPath))
                {
                    try
                    {
                        var target = new FileInfo(d.FullPath).LinkTarget;
                        if (target != null)
                        {
                            var resolvedTarget = Path.GetFullPath(target, Path.GetDirectoryName(d.FullPath)!);
                            var resolvedConfig = Path.GetFullPath(configRepoPath);
                            if (!resolvedTarget.StartsWith(resolvedConfig, StringComparison.OrdinalIgnoreCase))
                                model.Status = CardStatus.Drift;
                        }
                    }
                    catch { }
                }
                return model;
            })
            .ToImmutableArray();
    }

    public async Task<FontDetectionResult> DetectFontsAsync(CancellationToken cancellationToken = default)
    {
        var systemFonts = await _fontScanner.ScanAsync(cancellationToken);

        ImmutableArray<FontCatalogEntry> galleryFonts;
        try
        {
            galleryFonts = await _catalog.GetAllFontsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            galleryFonts = [];
        }

        var galleryByNormalized = new Dictionary<string, FontCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var gf in galleryFonts)
            galleryByNormalized[NormalizeFontName(gf.Name)] = gf;

        var matchedGalleryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var detected = ImmutableArray.CreateBuilder<FontCardModel>();

        foreach (var font in systemFonts)
        {
            if (DefaultFontFamilies.IsDefault(Path.GetFileNameWithoutExtension(font.FullPath)))
                continue;

            var normalizedName = NormalizeFontName(font.Name);
            FontCatalogEntry? matchedGallery = null;
            if (galleryByNormalized.TryGetValue(normalizedName, out var entry))
            {
                matchedGallery = entry;
                matchedGalleryIds.Add(entry.Id);
            }

            detected.Add(new FontCardModel(
                matchedGallery?.Id ?? font.Name,
                matchedGallery?.Name ?? font.Name,
                matchedGallery?.Description,
                matchedGallery?.PreviewText,
                font.FullPath,
                FontCardSource.Detected,
                matchedGallery?.Tags ?? [],
                CardStatus.Detected));
        }

        var gallery = ImmutableArray.CreateBuilder<FontCardModel>();
        foreach (var gf in galleryFonts)
        {
            if (matchedGalleryIds.Contains(gf.Id))
                continue;

            gallery.Add(new FontCardModel(
                gf.Id,
                gf.Name,
                gf.Description,
                gf.PreviewText,
                fullPath: null,
                FontCardSource.Gallery,
                gf.Tags,
                CardStatus.NotInstalled));
        }

        return new FontDetectionResult(detected.ToImmutable(), gallery.ToImmutable());
    }

    private static string NormalizeFontName(string name)
        => name.Replace(" ", "", StringComparison.Ordinal)
               .Replace("-", "", StringComparison.Ordinal)
               .Replace("_", "", StringComparison.Ordinal);

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
