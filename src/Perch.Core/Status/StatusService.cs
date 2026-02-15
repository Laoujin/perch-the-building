using Perch.Core.Machines;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Symlinks;

namespace Perch.Core.Status;

public sealed class StatusService : IStatusService
{
    private readonly IModuleDiscoveryService _discoveryService;
    private readonly ISymlinkProvider _symlinkProvider;
    private readonly IPlatformDetector _platformDetector;
    private readonly IGlobResolver _globResolver;
    private readonly IMachineProfileService _machineProfileService;
    private readonly IRegistryProvider _registryProvider;

    public StatusService(IModuleDiscoveryService discoveryService, ISymlinkProvider symlinkProvider, IPlatformDetector platformDetector, IGlobResolver globResolver, IMachineProfileService machineProfileService, IRegistryProvider registryProvider)
    {
        _discoveryService = discoveryService;
        _symlinkProvider = symlinkProvider;
        _platformDetector = platformDetector;
        _globResolver = globResolver;
        _machineProfileService = machineProfileService;
        _registryProvider = registryProvider;
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
        MachineProfile? machineProfile = await _machineProfileService.LoadAsync(configRepoPath, cancellationToken).ConfigureAwait(false);

        foreach (AppModule module in discovery.Modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!module.Enabled)
            {
                continue;
            }

            if (module.Platforms.Length > 0 && !module.Platforms.Contains(currentPlatform))
            {
                continue;
            }

            if (machineProfile?.IncludeModules.Length > 0 && !machineProfile.IncludeModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (machineProfile?.ExcludeModules.Length > 0 && machineProfile.ExcludeModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
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

                string expandedTarget = EnvironmentExpander.Expand(target, machineProfile?.Variables);
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

            if (!module.Registry.IsDefault)
            {
                foreach (RegistryEntryDefinition entry in module.Registry)
                {
                    StatusResult result = CheckRegistryEntry(module.DisplayName, entry);
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

    private StatusResult CheckRegistryEntry(string moduleName, RegistryEntryDefinition entry)
    {
        string registryPath = $"{entry.Key}\\{entry.Name}";
        object? currentValue = _registryProvider.GetValue(entry.Key, entry.Name);

        if (currentValue == null)
        {
            return new StatusResult(moduleName, "", registryPath, DriftLevel.Missing, $"Registry value {entry.Name} does not exist");
        }

        if (!Equals(currentValue, entry.Value))
        {
            return new StatusResult(moduleName, "", registryPath, DriftLevel.Drift,
                $"Registry {entry.Name} is {currentValue}, expected {entry.Value}");
        }

        return new StatusResult(moduleName, "", registryPath, DriftLevel.Ok, "OK");
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
