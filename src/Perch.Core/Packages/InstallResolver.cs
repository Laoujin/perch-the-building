using System.Collections.Immutable;
using Perch.Core.Catalog;

namespace Perch.Core.Packages;

public sealed class InstallResolver : IInstallResolver
{
    private readonly ICatalogService _catalogService;

    public InstallResolver(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public async Task<InstallResolution> ResolveAsync(
        InstallManifest manifest,
        string machineName,
        Platform currentPlatform,
        CancellationToken cancellationToken = default)
    {
        var appIds = ResolveAppIds(manifest, machineName);
        var packages = new List<PackageDefinition>();
        var errors = new List<string>();

        foreach (string appId in appIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CatalogEntry? entry;
            try
            {
                entry = await _catalogService.GetAppAsync(appId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to load gallery entry '{appId}': {ex.Message}");
                continue;
            }

            if (entry?.Install == null)
            {
                errors.Add($"Gallery entry '{appId}' not found or has no install metadata.");
                continue;
            }

            var package = ResolvePackage(entry.Install, currentPlatform);
            if (package != null)
            {
                packages.Add(package);
            }
        }

        return new InstallResolution(packages.ToImmutableArray(), errors.ToImmutableArray());
    }

    public async Task<InstallResolution> ResolveFontsAsync(
        ImmutableArray<string> fontIds,
        Platform currentPlatform,
        CancellationToken cancellationToken = default)
    {
        var packages = new List<PackageDefinition>();
        var errors = new List<string>();

        foreach (string fontId in fontIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CatalogEntry? entry;
            try
            {
                entry = await _catalogService.GetAppAsync(fontId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to load gallery entry '{fontId}': {ex.Message}");
                continue;
            }

            if (entry?.Install == null)
            {
                errors.Add($"Gallery entry '{fontId}' not found or has no install metadata.");
                continue;
            }

            var package = ResolvePackage(entry.Install, currentPlatform);
            if (package != null)
            {
                packages.Add(package);
            }
        }

        return new InstallResolution(packages.ToImmutableArray(), errors.ToImmutableArray());
    }

    private static ImmutableArray<string> ResolveAppIds(InstallManifest manifest, string machineName)
    {
        var apps = new HashSet<string>(manifest.Apps, StringComparer.OrdinalIgnoreCase);

        if (manifest.Machines.TryGetValue(machineName, out var overrides))
        {
            foreach (string add in overrides.Add)
            {
                apps.Add(add);
            }

            foreach (string exclude in overrides.Exclude)
            {
                apps.Remove(exclude);
            }
        }

        return apps.ToImmutableArray();
    }

    private static PackageDefinition? ResolvePackage(InstallDefinition install, Platform platform)
    {
        if (platform == Platform.Windows)
        {
            // Collect all alternative IDs for installed check
            var allIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(install.Winget))
                allIds.Add(install.Winget!);
            if (!string.IsNullOrWhiteSpace(install.Choco))
                allIds.Add(install.Choco!);

            // Prefer winget, fallback to choco
            if (!string.IsNullOrWhiteSpace(install.Winget))
            {
                return new PackageDefinition(install.Winget!, PackageManager.Winget, allIds.ToImmutableArray());
            }

            if (!string.IsNullOrWhiteSpace(install.Choco))
            {
                return new PackageDefinition(install.Choco!, PackageManager.Chocolatey, allIds.ToImmutableArray());
            }
        }

        return null;
    }
}
