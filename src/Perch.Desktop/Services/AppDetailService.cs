using System.Collections.Immutable;

using Perch.Core;
using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Modules;
using Perch.Core.Symlinks;
using Perch.Desktop.Models;

namespace Perch.Desktop.Services;

public sealed class AppDetailService : IAppDetailService
{
    private readonly IModuleDiscoveryService _moduleDiscovery;
    private readonly ICatalogService _catalog;
    private readonly ISettingsProvider _settings;
    private readonly IPlatformDetector _platformDetector;
    private readonly ISymlinkProvider _symlinkProvider;

    public AppDetailService(
        IModuleDiscoveryService moduleDiscovery,
        ICatalogService catalog,
        ISettingsProvider settings,
        IPlatformDetector platformDetector,
        ISymlinkProvider symlinkProvider)
    {
        _moduleDiscovery = moduleDiscovery;
        _catalog = catalog;
        _settings = settings;
        _platformDetector = platformDetector;
        _symlinkProvider = symlinkProvider;
    }

    public async Task<AppDetail> LoadDetailAsync(AppCardModel card, CancellationToken cancellationToken = default)
    {
        var perchSettings = await _settings.LoadAsync(cancellationToken);
        var configRepoPath = perchSettings.ConfigRepoPath;

        if (string.IsNullOrWhiteSpace(configRepoPath))
        {
            return new AppDetail(card, null, null, null, null, []);
        }

        var discovery = await _moduleDiscovery.DiscoverAsync(configRepoPath, cancellationToken);

        var (owningModule, manifest, manifestYaml, manifestPath) = await FindModuleByGalleryIdAsync(
            discovery.Modules, card.Id, cancellationToken);

        var alternatives = await FindAlternativesAsync(card.Category, card.Id, cancellationToken);
        var fileStatuses = DetectFileStatuses(card.CatalogEntry, configRepoPath);

        return new AppDetail(card, owningModule, manifest, manifestYaml, manifestPath, alternatives, fileStatuses);
    }

    private ImmutableArray<DotfileFileStatus> DetectFileStatuses(CatalogEntry entry, string? configRepoPath)
    {
        if (entry.Config is null || entry.Config.Links.IsDefaultOrEmpty)
            return [];

        var platform = _platformDetector.CurrentPlatform;
        var builder = ImmutableArray.CreateBuilder<DotfileFileStatus>();

        foreach (var link in entry.Config.Links)
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

            builder.Add(new DotfileFileStatus(
                Path.GetFileName(resolved),
                resolved,
                exists,
                isSymlink,
                fileStatus));
        }

        return builder.ToImmutable();
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

    internal static async Task<(AppModule? Module, AppManifest? Manifest, string? Yaml, string? Path)>
        FindModuleByGalleryIdAsync(
            ImmutableArray<AppModule> modules,
            string galleryId,
            CancellationToken cancellationToken)
    {
        foreach (var module in modules)
        {
            var manifestPath = System.IO.Path.Combine(module.ModulePath, "manifest.yaml");
            if (!File.Exists(manifestPath))
                continue;

            var yaml = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var parser = new ManifestParser();
            var result = parser.Parse(yaml, module.Name);
            if (!result.IsSuccess)
                continue;

            if (string.Equals(result.Manifest!.GalleryId, galleryId, StringComparison.OrdinalIgnoreCase))
            {
                return (module, result.Manifest, yaml, manifestPath);
            }
        }

        return (null, null, null, null);
    }

    private async Task<ImmutableArray<CatalogEntry>> FindAlternativesAsync(
        string category,
        string galleryId,
        CancellationToken cancellationToken)
    {
        var allApps = await _catalog.GetAllAppsAsync(cancellationToken);
        return allApps
            .Where(a => string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(a.Id, galleryId, StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();
    }
}
