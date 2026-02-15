using Perch.Core.Backup;
using Perch.Core.Machines;
using Perch.Core.Modules;
using Perch.Core.Packages;
using Perch.Core.Registry;
using Perch.Core.Symlinks;

namespace Perch.Core.Deploy;

public sealed class DeployService : IDeployService
{
    private readonly IModuleDiscoveryService _discoveryService;
    private readonly SymlinkOrchestrator _orchestrator;
    private readonly IPlatformDetector _platformDetector;
    private readonly IGlobResolver _globResolver;
    private readonly ISnapshotProvider _snapshotProvider;
    private readonly IHookRunner _hookRunner;
    private readonly IMachineProfileService _machineProfileService;
    private readonly IRegistryProvider _registryProvider;
    private readonly IGlobalPackageInstaller _globalPackageInstaller;
    private readonly IVscodeExtensionInstaller _vscodeExtensionInstaller;
    private readonly IPsModuleInstaller _psModuleInstaller;
    private readonly PackageManifestParser _packageManifestParser;
    private readonly ISystemPackageInstaller _systemPackageInstaller;

    public DeployService(IModuleDiscoveryService discoveryService, SymlinkOrchestrator orchestrator, IPlatformDetector platformDetector, IGlobResolver globResolver, ISnapshotProvider snapshotProvider, IHookRunner hookRunner, IMachineProfileService machineProfileService, IRegistryProvider registryProvider, IGlobalPackageInstaller globalPackageInstaller, IVscodeExtensionInstaller vscodeExtensionInstaller, IPsModuleInstaller psModuleInstaller, PackageManifestParser packageManifestParser, ISystemPackageInstaller systemPackageInstaller)
    {
        _discoveryService = discoveryService;
        _orchestrator = orchestrator;
        _platformDetector = platformDetector;
        _globResolver = globResolver;
        _snapshotProvider = snapshotProvider;
        _hookRunner = hookRunner;
        _machineProfileService = machineProfileService;
        _registryProvider = registryProvider;
        _globalPackageInstaller = globalPackageInstaller;
        _vscodeExtensionInstaller = vscodeExtensionInstaller;
        _psModuleInstaller = psModuleInstaller;
        _packageManifestParser = packageManifestParser;
        _systemPackageInstaller = systemPackageInstaller;
    }

    public async Task<int> DeployAsync(string configRepoPath, bool dryRun = false, IProgress<DeployResult>? progress = null, IDeployConfirmation? confirmation = null, CancellationToken cancellationToken = default)
    {
        DiscoveryResult discovery = await _discoveryService.DiscoverAsync(configRepoPath, cancellationToken).ConfigureAwait(false);

        foreach (string error in discovery.Errors)
        {
            progress?.Report(new DeployResult("discovery", "", "", ResultLevel.Error, error));
        }

        bool hasErrors = discovery.Errors.Length > 0;
        Platform currentPlatform = _platformDetector.CurrentPlatform;
        MachineProfile? machineProfile = await _machineProfileService.LoadAsync(configRepoPath, cancellationToken).ConfigureAwait(false);

        if (!dryRun)
        {
            IReadOnlyList<string> allTargetPaths = CollectTargetPaths(discovery.Modules, currentPlatform);
            _snapshotProvider.CreateSnapshot(allTargetPaths, cancellationToken);
        }

        bool allConfirmed = false;

        foreach (AppModule module in discovery.Modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? skipReason = GetSkipReason(module, currentPlatform, machineProfile);
            if (skipReason != null)
            {
                progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, skipReason));
                continue;
            }

            if (confirmation != null && !allConfirmed)
            {
                var choice = confirmation.Confirm(module.DisplayName);
                switch (choice)
                {
                    case DeployConfirmationChoice.No:
                        progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, "Skipped (user declined)"));
                        continue;
                    case DeployConfirmationChoice.All:
                        allConfirmed = true;
                        break;
                    case DeployConfirmationChoice.Quit:
                        return hasErrors ? 1 : 0;
                }
            }

            if (await DeployModuleAsync(module, currentPlatform, dryRun, progress, cancellationToken).ConfigureAwait(false))
            {
                hasErrors = true;
            }
        }

        if (await ProcessSystemPackagesAsync(configRepoPath, currentPlatform, dryRun, progress, cancellationToken).ConfigureAwait(false))
        {
            hasErrors = true;
        }

        return hasErrors ? 1 : 0;
    }

    private static string? GetSkipReason(AppModule module, Platform currentPlatform, MachineProfile? machineProfile)
    {
        if (!module.Enabled)
        {
            return "Skipped (disabled)";
        }

        if (module.Platforms.Length > 0 && !module.Platforms.Contains(currentPlatform))
        {
            return $"Skipped (not for {currentPlatform})";
        }

        if (machineProfile?.IncludeModules.Length > 0 && !machineProfile.IncludeModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
        {
            return "Skipped (not in machine profile)";
        }

        if (machineProfile?.ExcludeModules.Length > 0 && machineProfile.ExcludeModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
        {
            return "Skipped (excluded by machine profile)";
        }

        return null;
    }

    private async Task<bool> DeployModuleAsync(AppModule module, Platform currentPlatform, bool dryRun, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
    {
        bool hasErrors = false;

        if (!dryRun && module.Hooks?.PreDeploy != null)
        {
            string preDeployPath = Path.GetFullPath(Path.Combine(module.ModulePath, module.Hooks.PreDeploy));
            DeployResult hookResult = await _hookRunner.RunAsync(module.DisplayName, preDeployPath, module.ModulePath, cancellationToken).ConfigureAwait(false);
            progress?.Report(hookResult);
            if (hookResult.Level == ResultLevel.Error)
            {
                return true;
            }
        }

        bool moduleHadErrors = ProcessModuleLinks(module, currentPlatform, dryRun, progress);
        ProcessModuleRegistry(module, dryRun, progress);
        if (await ProcessModuleGlobalPackagesAsync(module, dryRun, progress, cancellationToken).ConfigureAwait(false))
        {
            moduleHadErrors = true;
        }
        if (await ProcessListAsync(module.VscodeExtensions, id => _vscodeExtensionInstaller.InstallAsync(module.DisplayName, id, dryRun, cancellationToken), progress, cancellationToken).ConfigureAwait(false))
        {
            moduleHadErrors = true;
        }
        if (await ProcessListAsync(module.PsModules, name => _psModuleInstaller.InstallAsync(module.DisplayName, name, dryRun, cancellationToken), progress, cancellationToken).ConfigureAwait(false))
        {
            moduleHadErrors = true;
        }
        hasErrors = moduleHadErrors;

        if (!dryRun && module.Hooks?.PostDeploy != null && !moduleHadErrors)
        {
            string postDeployPath = Path.GetFullPath(Path.Combine(module.ModulePath, module.Hooks.PostDeploy));
            DeployResult hookResult = await _hookRunner.RunAsync(module.DisplayName, postDeployPath, module.ModulePath, cancellationToken).ConfigureAwait(false);
            progress?.Report(hookResult);
            if (hookResult.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        return hasErrors;
    }

    private async Task<bool> ProcessSystemPackagesAsync(string configRepoPath, Platform currentPlatform, bool dryRun, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
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
            progress?.Report(new DeployResult("system-packages", "", "", ResultLevel.Error, error));
        }

        bool hasErrors = parseResult.Errors.Length > 0;

        foreach (PackageDefinition package in parseResult.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsPlatformMatch(package.Manager, currentPlatform))
            {
                continue;
            }

            DeployResult result = await _systemPackageInstaller.InstallAsync(package.Name, package.Manager, dryRun, cancellationToken).ConfigureAwait(false);
            progress?.Report(result);

            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        return hasErrors;
    }

    private static bool IsPlatformMatch(PackageManager manager, Platform platform) =>
        manager switch
        {
            PackageManager.Chocolatey or PackageManager.Winget => platform == Platform.Windows,
            PackageManager.Apt => platform == Platform.Linux,
            PackageManager.Brew => platform == Platform.MacOS,
            _ => false,
        };

    private bool ProcessModuleLinks(AppModule module, Platform currentPlatform, bool dryRun, IProgress<DeployResult>? progress)
    {
        bool hasErrors = false;

        foreach (LinkEntry link in module.Links)
        {
            string? target = link.GetTargetForPlatform(currentPlatform);
            if (target == null)
            {
                progress?.Report(new DeployResult(module.DisplayName, link.Source, "", ResultLevel.Ok,
                    $"Skipped (no target for {currentPlatform})"));
                continue;
            }

            string expandedTarget = EnvironmentExpander.Expand(target);
            string sourcePath = Path.GetFullPath(Path.Combine(module.ModulePath, link.Source));

            IReadOnlyList<string> resolvedTargets = _globResolver.Resolve(expandedTarget);
            if (resolvedTargets.Count == 0)
            {
                progress?.Report(new DeployResult(module.DisplayName, sourcePath, expandedTarget, ResultLevel.Warning,
                    "No matches for glob pattern"));
                continue;
            }

            foreach (string resolvedTarget in resolvedTargets)
            {
                DeployResult result = _orchestrator.ProcessLink(module.DisplayName, sourcePath, resolvedTarget, link.LinkType, dryRun);
                progress?.Report(result);

                if (result.Level == ResultLevel.Error)
                {
                    hasErrors = true;
                }
            }
        }

        return hasErrors;
    }

    private void ProcessModuleRegistry(AppModule module, bool dryRun, IProgress<DeployResult>? progress)
    {
        if (module.Registry.IsDefault || module.Registry.Length == 0)
        {
            return;
        }

        foreach (RegistryEntryDefinition entry in module.Registry)
        {
            if (dryRun)
            {
                progress?.Report(new DeployResult(module.DisplayName, "", $"{entry.Key}\\{entry.Name}",
                    ResultLevel.Ok, $"Would set {entry.Key}\\{entry.Name} to {entry.Value}"));
                continue;
            }

            object? currentValue = _registryProvider.GetValue(entry.Key, entry.Name);
            if (Equals(currentValue, entry.Value))
            {
                progress?.Report(new DeployResult(module.DisplayName, "", $"{entry.Key}\\{entry.Name}",
                    ResultLevel.Ok, $"Registry {entry.Name} already set to {entry.Value}"));
                continue;
            }

            _registryProvider.SetValue(entry.Key, entry.Name, entry.Value, entry.Kind);
            progress?.Report(new DeployResult(module.DisplayName, "", $"{entry.Key}\\{entry.Name}",
                ResultLevel.Ok, $"Set {entry.Name} to {entry.Value}"));
        }
    }

    private async Task<bool> ProcessModuleGlobalPackagesAsync(AppModule module, bool dryRun, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
    {
        if (module.GlobalPackages == null || module.GlobalPackages.Packages.IsEmpty)
        {
            return false;
        }

        bool hasErrors = false;
        foreach (string package in module.GlobalPackages.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeployResult result = await _globalPackageInstaller.InstallAsync(module.DisplayName, module.GlobalPackages.Manager, package, dryRun, cancellationToken).ConfigureAwait(false);
            progress?.Report(result);
            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        return hasErrors;
    }

    private static async Task<bool> ProcessListAsync(System.Collections.Immutable.ImmutableArray<string> items, Func<string, Task<DeployResult>> action, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
    {
        if (items.IsDefaultOrEmpty)
        {
            return false;
        }

        bool hasErrors = false;
        foreach (string item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeployResult result = await action(item).ConfigureAwait(false);
            progress?.Report(result);
            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        return hasErrors;
    }

    private IReadOnlyList<string> CollectTargetPaths(System.Collections.Immutable.ImmutableArray<AppModule> modules, Platform currentPlatform)
    {
        var targets = new List<string>();
        foreach (AppModule module in modules)
        {
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
                IReadOnlyList<string> resolvedTargets = _globResolver.Resolve(expandedTarget);
                targets.AddRange(resolvedTargets);
            }
        }

        return targets;
    }
}
