using System.Collections.Immutable;

using Microsoft.Extensions.Logging;

using Perch.Core;
using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Packages;
using Perch.Core.Scanner;
using Perch.Core.Symlinks;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;

namespace Perch.Desktop.Services;

public sealed class GalleryDetectionService : IGalleryDetectionService
{
    private readonly ICatalogService _catalog;
    private readonly IFontScanner _fontScanner;
    private readonly IPlatformDetector _platformDetector;
    private readonly ISymlinkProvider _symlinkProvider;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IEnumerable<IPackageManagerProvider> _packageProviders;
    private readonly ITweakService _tweakService;
    private readonly ILogger<GalleryDetectionService> _logger;

    private readonly SemaphoreSlim _packageScanLock = new(1, 1);
    private HashSet<string>? _cachedInstalledIds;

    public GalleryDetectionService(
        ICatalogService catalog,
        IFontScanner fontScanner,
        IPlatformDetector platformDetector,
        ISymlinkProvider symlinkProvider,
        ISettingsProvider settingsProvider,
        IEnumerable<IPackageManagerProvider> packageProviders,
        ITweakService tweakService,
        ILogger<GalleryDetectionService> logger)
    {
        _catalog = catalog;
        _fontScanner = fontScanner;
        _platformDetector = platformDetector;
        _symlinkProvider = symlinkProvider;
        _settingsProvider = settingsProvider;
        _packageProviders = packageProviders;
        _tweakService = tweakService;
        _logger = logger;
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _catalog.GetAllAppsAsync(cancellationToken),
            _catalog.GetAllTweaksAsync(cancellationToken),
            _catalog.GetAllFontsAsync(cancellationToken),
            _catalog.GetGitHubStarsAsync(cancellationToken),
            ScanInstalledPackageIdsAsync(cancellationToken));
    }

    public async Task<GalleryDetectionResult> DetectAppsAsync(
        IReadOnlySet<UserProfile> selectedProfiles,
        CancellationToken cancellationToken = default)
    {
        var allApps = await _catalog.GetAllAppsAsync(cancellationToken);
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        var platform = _platformDetector.CurrentPlatform;
        var installedIds = await ScanInstalledPackageIdsAsync(cancellationToken);
        var stars = await _catalog.GetGitHubStarsAsync(cancellationToken);
        var logoBaseUrl = GetLogoBaseUrl(settings);

        var yourApps = ImmutableArray.CreateBuilder<AppCardModel>();
        var suggested = ImmutableArray.CreateBuilder<AppCardModel>();
        var other = ImmutableArray.CreateBuilder<AppCardModel>();

        foreach (var app in allApps)
        {
            if (IsPureConfigDotfile(app))
                continue;

            var detected = IsAppDetected(app, platform, installedIds);
            var linked = detected && IsAppLinked(app, platform, settings.ConfigRepoPath);

            CardStatus status;
            if (linked) status = CardStatus.Linked;
            else if (detected) status = CardStatus.Detected;
            else status = CardStatus.NotInstalled;

            var logoUrl = $"{logoBaseUrl}{app.Id}.png";
            int? appStars = stars.TryGetValue(app.Id, out var starCount) ? starCount : null;

            if (detected)
            {
                yourApps.Add(new AppCardModel(app, CardTier.YourApps, status, logoUrl) { GitHubStars = appStars });
            }
            else if (IsSuggestedForProfiles(app, selectedProfiles))
            {
                suggested.Add(new AppCardModel(app, CardTier.Suggested, status, logoUrl) { GitHubStars = appStars });
            }
            else
            {
                other.Add(new AppCardModel(app, CardTier.Other, status, logoUrl) { GitHubStars = appStars });
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
        var installedIds = await ScanInstalledPackageIdsAsync(cancellationToken);
        var stars = await _catalog.GetGitHubStarsAsync(cancellationToken);
        var logoBaseUrl = GetLogoBaseUrl(settings);
        var builder = ImmutableArray.CreateBuilder<AppCardModel>();

        foreach (var app in allApps)
        {
            if (IsPureConfigDotfile(app))
                continue;

            var status = ResolveStatus(app, platform, settings.ConfigRepoPath, installedIds);
            int? appStars = stars.TryGetValue(app.Id, out var starCount) ? starCount : null;
            builder.Add(new AppCardModel(app, CardTier.Other, status, $"{logoBaseUrl}{app.Id}.png") { GitHubStars = appStars });
        }

        return builder.ToImmutable();
    }

    private CardStatus ResolveStatus(CatalogEntry app, Platform platform, string? configRepoPath, HashSet<string> installedIds)
    {
        var detected = IsAppDetected(app, platform, installedIds);
        var linked = detected && IsAppLinked(app, platform, configRepoPath);
        if (linked) return CardStatus.Linked;
        if (detected) return CardStatus.Detected;
        return CardStatus.NotInstalled;
    }

    private async Task<HashSet<string>> ScanInstalledPackageIdsAsync(CancellationToken cancellationToken)
    {
        if (_cachedInstalledIds is not null)
            return _cachedInstalledIds;

        await _packageScanLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedInstalledIds is not null)
                return _cachedInstalledIds;

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tasks = _packageProviders.Select(async provider =>
            {
                try
                {
                    return await provider.ScanInstalledAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return PackageManagerScanResult.Unavailable(ex.Message);
                }
            });

            var results = await Task.WhenAll(tasks);
            foreach (var result in results)
            {
                if (result.IsAvailable)
                {
                    foreach (var pkg in result.Packages)
                        ids.Add(pkg.Name);
                }
            }

            _cachedInstalledIds = ids;
            return ids;
        }
        finally
        {
            _packageScanLock.Release();
        }
    }

    public void InvalidatePackageCache()
    {
        _packageScanLock.Wait();
        try
        {
            _cachedInstalledIds = null;
        }
        finally
        {
            _packageScanLock.Release();
        }
    }

    public void InvalidateCache()
    {
        _catalog.InvalidateAll();
        _cachedInstalledIds = null;
    }

    public async Task<TweakDetectionPageResult> DetectTweaksAsync(
        IReadOnlySet<UserProfile> selectedProfiles,
        CancellationToken cancellationToken = default)
    {
        var allTweaks = await _catalog.GetAllTweaksAsync(cancellationToken);
        var builder = ImmutableArray.CreateBuilder<TweakCardModel>();
        var errors = ImmutableArray.CreateBuilder<TweakDetectionError>();

        foreach (var tweak in allTweaks)
        {
            try
            {
                var detection = await _tweakService.DetectWithCaptureAsync(tweak, cancellationToken);
                CardStatus status = detection.Status switch
                {
                    TweakStatus.Applied => CardStatus.Detected,
                    TweakStatus.Partial => CardStatus.Drift,
                    _ => CardStatus.NotInstalled,
                };

                var model = new TweakCardModel(tweak, status);
                model.AppliedCount = detection.Entries.Count(e => e.IsApplied);
                model.DetectedEntries = detection.Entries;

                if (model.MatchesProfile(selectedProfiles))
                {
                    builder.Add(model);
                }
            }
            catch (Exception ex)
            {
                string? firstKey = tweak.Registry.IsDefaultOrEmpty ? null : tweak.Registry[0].Key;
                errors.Add(new TweakDetectionError(tweak.Name, tweak.Id, firstKey, tweak.Source, ex.Message));
            }
        }

        return new TweakDetectionPageResult(builder.ToImmutable(), errors.ToImmutable());
    }

    public async Task<ImmutableArray<AppCardModel>> DetectDotfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var dotfileApps = await _catalog.GetAllDotfileAppsAsync(cancellationToken);
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        var platform = _platformDetector.CurrentPlatform;
        var configRepoPath = settings.ConfigRepoPath;

        var builder = ImmutableArray.CreateBuilder<AppCardModel>();

        foreach (var app in dotfileApps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (app.Config is null || app.Config.Links.IsDefaultOrEmpty)
                continue;

            var status = DetectDotfileStatus(app, platform, configRepoPath);
            if (status is null)
                continue;

            builder.Add(new AppCardModel(app, CardTier.Other, status.Value));
        }

        return builder.ToImmutable();
    }

    private CardStatus? DetectDotfileStatus(CatalogEntry app, Platform platform, string? configRepoPath)
    {
        bool allLinked = true;
        bool anyDrift = false;
        bool anyDetected = false;
        int count = 0;

        foreach (var link in app.Config!.Links)
        {
            if (!link.Platforms.IsDefaultOrEmpty && !link.Platforms.Contains(platform))
                continue;

            if (!link.Targets.TryGetValue(platform, out var targetPath))
                continue;

            count++;
            var resolved = Environment.ExpandEnvironmentVariables(targetPath.Replace('/', '\\'));
            var exists = File.Exists(resolved) || Directory.Exists(resolved);
            var isSymlink = exists && _symlinkProvider.IsSymlink(resolved);

            if (isSymlink)
            {
                if (!string.IsNullOrEmpty(configRepoPath))
                {
                    var driftCheck = DriftDetector.Check(resolved, configRepoPath, _logger);
                    if (driftCheck.IsDrift || driftCheck.Error is not null)
                        anyDrift = true;
                }
            }
            else
            {
                allLinked = false;
                if (exists) anyDetected = true;
            }
        }

        if (count == 0) return null;
        if (anyDrift) return CardStatus.Drift;
        if (allLinked) return CardStatus.Linked;
        if (anyDetected) return CardStatus.Detected;
        return CardStatus.NotInstalled;
    }

    public async Task<FontDetectionResult> DetectFontsAsync(CancellationToken cancellationToken = default)
    {
        var systemFontsTask = _fontScanner.ScanAsync(cancellationToken);
        var installedIdsTask = ScanInstalledPackageIdsAsync(cancellationToken);

        ImmutableArray<FontCatalogEntry> galleryFonts;
        try
        {
            galleryFonts = await _catalog.GetAllFontsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            galleryFonts = [];
        }

        var systemFonts = await systemFontsTask;
        var installedIds = await installedIdsTask;

        var galleryByNormalized = new Dictionary<string, FontCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var gf in galleryFonts)
            galleryByNormalized[NormalizeFontName(gf.Name)] = gf;

        // Determine which gallery fonts are installed (by name match or package ID)
        var matchedGalleryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packageInstalledGalleryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: check package IDs for gallery fonts
        foreach (var gf in galleryFonts)
        {
            if (IsGalleryFontInstalledByPackage(gf, installedIds))
                packageInstalledGalleryNames.Add(NormalizeFontName(gf.Name));
        }

        // Build detected (installed) fonts, excluding nerd fonts matched by package
        var detected = ImmutableArray.CreateBuilder<FontCardModel>();
        foreach (var font in systemFonts)
        {
            if (DefaultFontFamilies.IsDefault(Path.GetFileNameWithoutExtension(font.FullPath)))
                continue;

            var normalizedName = NormalizeFontName(font.Name);

            // If this system font matches a gallery font installed via package manager, skip it
            if (packageInstalledGalleryNames.Contains(normalizedName))
            {
                if (galleryByNormalized.TryGetValue(normalizedName, out var pkgEntry))
                    matchedGalleryIds.Add(pkgEntry.Id);
                continue;
            }

            FontCatalogEntry? matchedGallery = null;
            if (galleryByNormalized.TryGetValue(normalizedName, out var entry))
            {
                matchedGallery = entry;
                matchedGalleryIds.Add(entry.Id);
            }
            else if (font.FamilyName is not null)
            {
                var normalizedFamily = NormalizeFontName(font.FamilyName);
                if (galleryByNormalized.TryGetValue(normalizedFamily, out var familyEntry))
                {
                    matchedGallery = familyEntry;
                    matchedGalleryIds.Add(familyEntry.Id);
                }
            }

            detected.Add(new FontCardModel(
                matchedGallery?.Id ?? font.Name,
                matchedGallery?.Name ?? font.Name,
                font.FamilyName,
                matchedGallery?.Description,
                matchedGallery?.PreviewText,
                font.FullPath,
                FontCardSource.Detected,
                matchedGallery?.Tags ?? [],
                CardStatus.Detected));
        }

        // Build nerd fonts list: all gallery fonts, with installed ones marked Detected
        var nerdFonts = ImmutableArray.CreateBuilder<FontCardModel>();
        foreach (var gf in galleryFonts)
        {
            var isInstalledByPackage = IsGalleryFontInstalledByPackage(gf, installedIds);
            var isNameMatched = matchedGalleryIds.Contains(gf.Id);

            var status = (isInstalledByPackage || isNameMatched)
                ? CardStatus.Detected
                : CardStatus.NotInstalled;

            nerdFonts.Add(new FontCardModel(
                gf.Id,
                gf.Name,
                familyName: null,
                gf.Description,
                gf.PreviewText,
                fullPath: null,
                FontCardSource.Gallery,
                gf.Tags,
                status));
        }

        return new FontDetectionResult(detected.ToImmutable(), nerdFonts.ToImmutable());
    }

    private static bool IsGalleryFontInstalledByPackage(FontCatalogEntry font, HashSet<string> installedIds)
    {
        if (font.Install is null)
            return false;

        return (font.Install.Choco is not null && installedIds.Contains(font.Install.Choco))
            || (font.Install.Winget is not null && installedIds.Contains(font.Install.Winget));
    }

    private static string NormalizeFontName(string name)
        => name.Replace(" ", "", StringComparison.Ordinal)
               .Replace("-", "", StringComparison.Ordinal)
               .Replace("_", "", StringComparison.Ordinal);

    private static string GetLogoBaseUrl(PerchSettings settings)
    {
        var baseUrl = settings.GalleryUrl.TrimEnd('/');
        return $"{baseUrl}/catalog/logos/";
    }

    private bool IsAppDetected(CatalogEntry app, Platform platform, HashSet<string> installedIds)
    {
        if (app.Install is not null)
        {
            if (app.Install.Winget is not null && installedIds.Contains(app.Install.Winget))
                return true;
            if (app.Install.Choco is not null && installedIds.Contains(app.Install.Choco))
                return true;
        }

        if (app.Config is not null && !app.Config.Links.IsDefaultOrEmpty)
        {
            foreach (var link in app.Config.Links)
            {
                if (!link.Targets.TryGetValue(platform, out var targetPath))
                    continue;

                var resolved = Environment.ExpandEnvironmentVariables(targetPath.Replace('/', '\\'));
                if (File.Exists(resolved) || Directory.Exists(resolved))
                    return true;
            }
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

    private static bool IsPureConfigDotfile(CatalogEntry app) =>
        app.Kind == CatalogKind.Dotfile && app.Install is null;

    private static bool IsSuggestedForProfiles(CatalogEntry app, IReadOnlySet<UserProfile> profiles)
    {
        return !app.Profiles.IsDefaultOrEmpty && ProfileMatcher.Matches(app.Profiles, profiles);
    }
}
