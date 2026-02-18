using System.Collections.Immutable;

using Perch.Core.Catalog;
using Perch.Core.Git;

namespace Perch.Core.Modules;

public sealed class ModuleDiscoveryService : IModuleDiscoveryService
{
    private readonly ManifestParser _parser;
    private readonly ICatalogService? _catalogService;
    private readonly IGalleryOverlayService? _overlayService;
    private readonly ISubmoduleService? _submoduleService;

    public ModuleDiscoveryService(ManifestParser parser, ICatalogService? catalogService = null, IGalleryOverlayService? overlayService = null, ISubmoduleService? submoduleService = null)
    {
        _parser = parser;
        _catalogService = catalogService;
        _overlayService = overlayService;
        _submoduleService = submoduleService;
    }

    public async Task<DiscoveryResult> DiscoverAsync(string configRepoPath, CancellationToken cancellationToken = default)
    {
        var modules = new List<AppModule>();
        var errors = new List<string>();

        if (!Directory.Exists(configRepoPath))
        {
            errors.Add($"Config repo path does not exist: {configRepoPath}");
            return new DiscoveryResult(modules.ToImmutableArray(), errors.ToImmutableArray());
        }

        if (_submoduleService != null)
        {
            await _submoduleService.InitializeIfNeededAsync(configRepoPath, cancellationToken).ConfigureAwait(false);
        }

        string[] subdirectories = Directory.GetDirectories(configRepoPath);
        Array.Sort(subdirectories, StringComparer.OrdinalIgnoreCase);

        foreach (string subdir in subdirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string moduleName = Path.GetFileName(subdir);
            string manifestPath = Path.Combine(subdir, "manifest.yaml");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                string yaml = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
                ManifestParseResult parseResult = _parser.Parse(yaml, moduleName);

                if (parseResult.IsSuccess)
                {
                    AppManifest manifest = parseResult.Manifest!;
                    manifest = await ApplyGalleryOverlayAsync(manifest, errors, cancellationToken).ConfigureAwait(false);
                    modules.Add(new AppModule(manifest.ModuleName, manifest.DisplayName, manifest.Enabled, subdir, manifest.Platforms, manifest.Links, manifest.Hooks, manifest.CleanFilter, manifest.Registry, manifest.GlobalPackages, manifest.VscodeExtensions, manifest.PsModules));
                }
                else
                {
                    errors.Add($"[{moduleName}] {parseResult.Error}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"[{moduleName}] Failed to read manifest: {ex.Message}");
            }
        }

        return new DiscoveryResult(modules.ToImmutableArray(), errors.ToImmutableArray());
    }

    private async Task<AppManifest> ApplyGalleryOverlayAsync(AppManifest manifest, List<string> errors, CancellationToken cancellationToken)
    {
        if (manifest.GalleryId == null || _catalogService == null || _overlayService == null)
        {
            return manifest;
        }

        try
        {
            var galleryEntry = await _catalogService.GetAppAsync(manifest.GalleryId, cancellationToken).ConfigureAwait(false);
            if (galleryEntry == null)
            {
                errors.Add($"[{manifest.ModuleName}] Gallery app '{manifest.GalleryId}' not found.");
                return manifest;
            }

            return _overlayService.Merge(manifest, galleryEntry);
        }
        catch (Exception ex)
        {
            errors.Add($"[{manifest.ModuleName}] Failed to load gallery app '{manifest.GalleryId}': {ex.Message}");
            return manifest;
        }
    }
}
