using System.Collections.Immutable;

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

    private readonly SemaphoreSlim _packageScanLock = new(1, 1);
    private HashSet<string>? _cachedInstalledIds;

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
        IFontScanner fontScanner,
        IPlatformDetector platformDetector,
        ISymlinkProvider symlinkProvider,
        ISettingsProvider settingsProvider,
        IEnumerable<IPackageManagerProvider> packageProviders,
        ITweakService tweakService)
    {
        _catalog = catalog;
        _fontScanner = fontScanner;
        _platformDetector = platformDetector;
        _symlinkProvider = symlinkProvider;
        _settingsProvider = settingsProvider;
        _packageProviders = packageProviders;
        _tweakService = tweakService;
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _catalog.GetAllAppsAsync(cancellationToken),
            _catalog.GetAllTweaksAsync(cancellationToken),
            _catalog.GetAllFontsAsync(cancellationToken),
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

        var yourApps = ImmutableArray.CreateBuilder<AppCardModel>();
        var suggested = ImmutableArray.CreateBuilder<AppCardModel>();
        var other = ImmutableArray.CreateBuilder<AppCardModel>();

        foreach (var app in allApps)
        {
            var detected = IsAppDetected(app, platform, installedIds);
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
        var installedIds = await ScanInstalledPackageIdsAsync(cancellationToken);
        var builder = ImmutableArray.CreateBuilder<AppCardModel>();

        foreach (var app in allApps)
        {
            var status = ResolveStatus(app, platform, settings.ConfigRepoPath, installedIds);
            builder.Add(new AppCardModel(app, CardTier.Other, status));
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

    public void InvalidatePackageCache() => _cachedInstalledIds = null;

    public async Task<ImmutableArray<TweakCardModel>> DetectTweaksAsync(
        IReadOnlySet<UserProfile> selectedProfiles,
        CancellationToken cancellationToken = default)
    {
        var allTweaks = await _catalog.GetAllTweaksAsync(cancellationToken);
        var builder = ImmutableArray.CreateBuilder<TweakCardModel>();

        foreach (var tweak in allTweaks)
        {
            var detection = _tweakService.Detect(tweak);
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

        return builder.ToImmutable();
    }

    public async Task<ImmutableArray<DotfileGroupCardModel>> DetectDotfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var dotfileApps = await _catalog.GetAllDotfileAppsAsync(cancellationToken);
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        var platform = _platformDetector.CurrentPlatform;
        var configRepoPath = settings.ConfigRepoPath;

        var builder = ImmutableArray.CreateBuilder<DotfileGroupCardModel>();

        foreach (var app in dotfileApps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (app.Config is null || app.Config.Links.IsDefaultOrEmpty)
                continue;

            var files = ImmutableArray.CreateBuilder<DotfileFileStatus>();
            foreach (var link in app.Config.Links)
            {
                if (!link.Platforms.IsDefaultOrEmpty && !link.Platforms.Contains(platform))
                    continue;

                if (!link.Targets.TryGetValue(platform, out var targetPath))
                    continue;

                var resolved = Environment.ExpandEnvironmentVariables(targetPath.Replace('/', '\\'));
                var exists = File.Exists(resolved) || Directory.Exists(resolved);
                var isSymlink = exists && _symlinkProvider.IsSymlink(resolved);

                var fileStatus = isSymlink ? CardStatus.Linked
                    : exists ? CardStatus.Detected
                    : CardStatus.NotInstalled;

                if (isSymlink && !string.IsNullOrEmpty(configRepoPath))
                {
                    if (IsDrift(resolved, configRepoPath))
                        fileStatus = CardStatus.Drift;
                }

                files.Add(new DotfileFileStatus(
                    Path.GetFileName(resolved),
                    resolved,
                    exists,
                    isSymlink,
                    fileStatus));
            }

            if (files.Count == 0)
                continue;

            var groupStatus = ResolveGroupStatus(files);
            builder.Add(new DotfileGroupCardModel(app, files.ToImmutable(), groupStatus));
        }

        return builder.ToImmutable();
    }

    private static CardStatus ResolveGroupStatus(ImmutableArray<DotfileFileStatus>.Builder files)
    {
        bool allLinked = true;
        bool anyDrift = false;
        bool anyDetected = false;

        foreach (var file in files)
        {
            if (file.Status == CardStatus.Drift) anyDrift = true;
            if (file.Status == CardStatus.Detected) anyDetected = true;
            if (file.Status != CardStatus.Linked) allLinked = false;
        }

        if (anyDrift) return CardStatus.Drift;
        if (allLinked) return CardStatus.Linked;
        if (anyDetected) return CardStatus.Detected;
        return CardStatus.NotInstalled;
    }

    private static bool IsDrift(string resolvedPath, string configRepoPath)
    {
        try
        {
            var linkTarget = new FileInfo(resolvedPath).LinkTarget;
            if (linkTarget is null) return false;

            var resolvedTarget = Path.GetFullPath(linkTarget, Path.GetDirectoryName(resolvedPath)!);
            var resolvedConfig = Path.GetFullPath(configRepoPath);
            return !resolvedTarget.StartsWith(resolvedConfig, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
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
