using Perch.Core.Modules;
using Perch.Core.Symlinks;

namespace Perch.Core.Status;

public sealed class StatusService : IStatusService
{
    private readonly IModuleDiscoveryService _discoveryService;
    private readonly ISymlinkProvider _symlinkProvider;
    private readonly IPlatformDetector _platformDetector;
    private readonly IGlobResolver _globResolver;

    public StatusService(IModuleDiscoveryService discoveryService, ISymlinkProvider symlinkProvider, IPlatformDetector platformDetector, IGlobResolver globResolver)
    {
        _discoveryService = discoveryService;
        _symlinkProvider = symlinkProvider;
        _platformDetector = platformDetector;
        _globResolver = globResolver;
    }

    public async Task<int> CheckAsync(string configRepoPath, IProgress<StatusResult>? progress = null, CancellationToken cancellationToken = default)
    {
        DiscoveryResult discovery = await _discoveryService.DiscoverAsync(configRepoPath, cancellationToken).ConfigureAwait(false);

        foreach (string error in discovery.Errors)
        {
            progress?.Report(new StatusResult("discovery", "", "", DriftLevel.Error, error));
        }

        bool hasDrift = discovery.Errors.Length > 0;
        Platform currentPlatform = _platformDetector.CurrentPlatform;

        foreach (AppModule module in discovery.Modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (module.Platforms.Length > 0 && !module.Platforms.Contains(currentPlatform))
            {
                continue;
            }

            foreach (LinkEntry link in module.Links)
            {
                string? target = link.GetTargetForPlatform(currentPlatform);
                if (target == null)
                {
                    continue;
                }

                string expandedTarget = EnvironmentExpander.Expand(target);
                string sourcePath = Path.GetFullPath(Path.Combine(module.ModulePath, link.Source));

                IReadOnlyList<string> resolvedTargets = _globResolver.Resolve(expandedTarget);
                foreach (string resolvedTarget in resolvedTargets)
                {
                    StatusResult result = CheckLink(module.DisplayName, sourcePath, resolvedTarget);
                    progress?.Report(result);

                    if (result.Level is DriftLevel.Missing or DriftLevel.Drift or DriftLevel.Error)
                    {
                        hasDrift = true;
                    }
                }
            }
        }

        return hasDrift ? 1 : 0;
    }

    private StatusResult CheckLink(string moduleName, string sourcePath, string targetPath)
    {
        try
        {
            if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
            {
                return new StatusResult(moduleName, sourcePath, targetPath, DriftLevel.Missing, "Target does not exist");
            }

            if (!_symlinkProvider.IsSymlink(targetPath))
            {
                return new StatusResult(moduleName, sourcePath, targetPath, DriftLevel.Drift, "Target is a regular file, not a symlink");
            }

            string? actualTarget = _symlinkProvider.GetSymlinkTarget(targetPath);
            if (!string.Equals(actualTarget, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return new StatusResult(moduleName, sourcePath, targetPath, DriftLevel.Drift,
                    $"Symlink points to {actualTarget} instead of {sourcePath}");
            }

            return new StatusResult(moduleName, sourcePath, targetPath, DriftLevel.Ok, "OK");
        }
        catch (Exception ex)
        {
            return new StatusResult(moduleName, sourcePath, targetPath, DriftLevel.Error, ex.Message);
        }
    }
}
