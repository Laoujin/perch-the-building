using Perch.Core.Machines;
using Perch.Core.Modules;
using Perch.Core.Packages;
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
    private readonly IEnumerable<IPackageManagerProvider> _packageManagerProviders;
    private readonly PackageManifestParser _packageManifestParser;
    private readonly IProcessRunner _processRunner;

    public StatusService(IModuleDiscoveryService discoveryService, ISymlinkProvider symlinkProvider, IPlatformDetector platformDetector, IGlobResolver globResolver, IMachineProfileService machineProfileService, IRegistryProvider registryProvider, IEnumerable<IPackageManagerProvider> packageManagerProviders, PackageManifestParser packageManifestParser, IProcessRunner processRunner)
    {
        _discoveryService = discoveryService;
        _symlinkProvider = symlinkProvider;
        _platformDetector = platformDetector;
        _globResolver = globResolver;
        _machineProfileService = machineProfileService;
        _registryProvider = registryProvider;
        _packageManagerProviders = packageManagerProviders;
        _packageManifestParser = packageManifestParser;
        _processRunner = processRunner;
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

        var packageCache = new Dictionary<PackageManager, IReadOnlySet<string>>();
        IReadOnlySet<string>? psModuleCache = null;
        bool psModuleUnavailable = false;

        foreach (AppModule module in discovery.Modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldSkipModule(module, currentPlatform, machineProfile))
            {
                continue;
            }

            if (CheckModuleLinks(module, currentPlatform, machineProfile, progress))
            {
                hasDrift = true;
            }

            if (CheckModuleRegistry(module, progress))
            {
                hasDrift = true;
            }

            if (await CheckGlobalPackagesAsync(module, packageCache, progress, cancellationToken).ConfigureAwait(false))
            {
                hasDrift = true;
            }

            if (await CheckVscodeExtensionsAsync(module, packageCache, progress, cancellationToken).ConfigureAwait(false))
            {
                hasDrift = true;
            }

            (bool psDrift, psModuleCache, psModuleUnavailable) = await CheckPsModulesAsync(module, psModuleCache, psModuleUnavailable, progress, cancellationToken).ConfigureAwait(false);
            if (psDrift)
            {
                hasDrift = true;
            }
        }

        if (await CheckSystemPackagesAsync(configRepoPath, currentPlatform, packageCache, progress, cancellationToken).ConfigureAwait(false))
        {
            hasDrift = true;
        }

        return hasDrift ? 1 : 0;
    }

    private static bool ShouldSkipModule(AppModule module, Platform currentPlatform, MachineProfile? machineProfile)
    {
        if (!module.Enabled)
        {
            return true;
        }

        if (module.Platforms.Length > 0 && !module.Platforms.Contains(currentPlatform))
        {
            return true;
        }

        if (machineProfile?.IncludeModules.Length > 0 && !machineProfile.IncludeModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (machineProfile?.ExcludeModules.Length > 0 && machineProfile.ExcludeModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private bool CheckModuleLinks(AppModule module, Platform currentPlatform, MachineProfile? machineProfile, IProgress<StatusResult>? progress)
    {
        bool hasDrift = false;

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

        return hasDrift;
    }

    private bool CheckModuleRegistry(AppModule module, IProgress<StatusResult>? progress)
    {
        if (module.Registry.IsDefault)
        {
            return false;
        }

        bool hasDrift = false;

        foreach (RegistryEntryDefinition entry in module.Registry)
        {
            StatusResult result = CheckRegistryEntry(module.DisplayName, entry);
            progress?.Report(result);

            if (result.Level is DriftLevel.Missing or DriftLevel.Drift or DriftLevel.Error)
            {
                hasDrift = true;
            }
        }

        return hasDrift;
    }

    private async Task<bool> CheckGlobalPackagesAsync(AppModule module, Dictionary<PackageManager, IReadOnlySet<string>> cache, IProgress<StatusResult>? progress, CancellationToken cancellationToken)
    {
        if (module.GlobalPackages == null || module.GlobalPackages.Packages.IsEmpty)
        {
            return false;
        }

        if (module.GlobalPackages.Manager == GlobalPackageManager.Bun)
        {
            return false;
        }

        var manager = module.GlobalPackages.Manager switch
        {
            GlobalPackageManager.Npm => PackageManager.Npm,
            _ => (PackageManager?)null,
        };

        if (manager == null)
        {
            return false;
        }

        IReadOnlySet<string> installed = await GetInstalledPackageNamesAsync(manager.Value, cache, cancellationToken).ConfigureAwait(false);
        bool hasDrift = false;

        foreach (string package in module.GlobalPackages.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool found = installed.Contains(package);
            var level = found ? DriftLevel.Ok : DriftLevel.Missing;
            string message = found ? "OK" : $"Global package {package} is not installed";
            progress?.Report(new StatusResult(module.DisplayName, "", package, level, message, StatusCategory.GlobalPackage));

            if (!found)
            {
                hasDrift = true;
            }
        }

        return hasDrift;
    }

    private async Task<bool> CheckVscodeExtensionsAsync(AppModule module, Dictionary<PackageManager, IReadOnlySet<string>> cache, IProgress<StatusResult>? progress, CancellationToken cancellationToken)
    {
        if (module.VscodeExtensions.IsDefaultOrEmpty)
        {
            return false;
        }

        IReadOnlySet<string> installed = await GetInstalledPackageNamesAsync(PackageManager.VsCode, cache, cancellationToken).ConfigureAwait(false);
        bool hasDrift = false;

        foreach (string extension in module.VscodeExtensions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool found = installed.Contains(extension);
            var level = found ? DriftLevel.Ok : DriftLevel.Missing;
            string message = found ? "OK" : $"VS Code extension {extension} is not installed";
            progress?.Report(new StatusResult(module.DisplayName, "", extension, level, message, StatusCategory.VscodeExtension));

            if (!found)
            {
                hasDrift = true;
            }
        }

        return hasDrift;
    }

    private async Task<(bool HasDrift, IReadOnlySet<string>? Cache, bool Unavailable)> CheckPsModulesAsync(AppModule module, IReadOnlySet<string>? cache, bool unavailable, IProgress<StatusResult>? progress, CancellationToken cancellationToken)
    {
        if (module.PsModules.IsDefaultOrEmpty)
        {
            return (false, cache, unavailable);
        }

        if (unavailable)
        {
            return (false, cache, true);
        }

        if (cache == null)
        {
            try
            {
                ProcessRunResult result = await _processRunner.RunAsync(
                    "pwsh", "-NoProfile -NonInteractive -Command \"(Get-Module -ListAvailable).Name\"",
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    progress?.Report(new StatusResult(module.DisplayName, "", "pwsh", DriftLevel.Error, $"pwsh failed: {result.StandardError}", StatusCategory.PsModule));
                    return (true, null, true);
                }

                cache = result.StandardOutput
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                progress?.Report(new StatusResult(module.DisplayName, "", "pwsh", DriftLevel.Error, $"pwsh unavailable: {ex.Message}", StatusCategory.PsModule));
                return (true, null, true);
            }
        }

        bool hasDrift = false;

        foreach (string psModule in module.PsModules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool found = cache.Contains(psModule);
            var level = found ? DriftLevel.Ok : DriftLevel.Missing;
            string message = found ? "OK" : $"PowerShell module {psModule} is not installed";
            progress?.Report(new StatusResult(module.DisplayName, "", psModule, level, message, StatusCategory.PsModule));

            if (!found)
            {
                hasDrift = true;
            }
        }

        return (hasDrift, cache, false);
    }

    private async Task<bool> CheckSystemPackagesAsync(string configRepoPath, Platform currentPlatform, Dictionary<PackageManager, IReadOnlySet<string>> cache, IProgress<StatusResult>? progress, CancellationToken cancellationToken)
    {
        string packagesPath = Path.Combine(configRepoPath, "packages.yaml");
        if (!File.Exists(packagesPath))
        {
            return false;
        }

        string yaml = await File.ReadAllTextAsync(packagesPath, cancellationToken).ConfigureAwait(false);
        PackageManifestParseResult parseResult = _packageManifestParser.Parse(yaml);

        foreach (string error in parseResult.Errors)
        {
            progress?.Report(new StatusResult("system-packages", "", "", DriftLevel.Error, error, StatusCategory.SystemPackage));
        }

        bool hasDrift = parseResult.Errors.Length > 0;

        foreach (PackageDefinition package in parseResult.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!package.Manager.IsPlatformMatch(currentPlatform))
            {
                continue;
            }

            IReadOnlySet<string> installed = await GetInstalledPackageNamesAsync(package.Manager, cache, cancellationToken).ConfigureAwait(false);
            bool found = installed.Contains(package.Name);
            var level = found ? DriftLevel.Ok : DriftLevel.Missing;
            string message = found ? "OK" : $"System package {package.Name} is not installed";
            progress?.Report(new StatusResult("system-packages", "", package.Name, level, message, StatusCategory.SystemPackage));

            if (!found)
            {
                hasDrift = true;
            }
        }

        return hasDrift;
    }

    private async Task<IReadOnlySet<string>> GetInstalledPackageNamesAsync(PackageManager manager, Dictionary<PackageManager, IReadOnlySet<string>> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(manager, out IReadOnlySet<string>? cached))
        {
            return cached;
        }

        IPackageManagerProvider? provider = _packageManagerProviders.FirstOrDefault(p => p.Manager == manager);
        if (provider == null)
        {
            HashSet<string> empty = new(StringComparer.OrdinalIgnoreCase);
            cache[manager] = empty;
            return empty;
        }

        PackageManagerScanResult scan = await provider.ScanInstalledAsync(cancellationToken).ConfigureAwait(false);
        HashSet<string> names = new(scan.Packages.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        cache[manager] = names;
        return names;
    }

    private StatusResult CheckRegistryEntry(string moduleName, RegistryEntryDefinition entry)
    {
        string registryPath = $"{entry.Key}\\{entry.Name}";
        object? currentValue = _registryProvider.GetValue(entry.Key, entry.Name);

        if (currentValue == null)
        {
            return new StatusResult(moduleName, "", registryPath, DriftLevel.Missing, $"Registry value {entry.Name} does not exist", StatusCategory.Registry);
        }

        if (!Equals(currentValue, entry.Value))
        {
            return new StatusResult(moduleName, "", registryPath, DriftLevel.Drift,
                $"Registry {entry.Name} is {currentValue}, expected {entry.Value}", StatusCategory.Registry);
        }

        return new StatusResult(moduleName, "", registryPath, DriftLevel.Ok, "OK", StatusCategory.Registry);
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
