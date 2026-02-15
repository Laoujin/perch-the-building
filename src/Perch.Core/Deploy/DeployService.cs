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

    public async Task<int> DeployAsync(string configRepoPath, DeployOptions? options = null, CancellationToken cancellationToken = default)
    {
        bool dryRun = options?.DryRun ?? false;
        IProgress<DeployResult>? progress = options?.Progress;
        var beforeModule = options?.BeforeModule;

        DiscoveryResult discovery = await _discoveryService.DiscoverAsync(configRepoPath, cancellationToken).ConfigureAwait(false);

        foreach (string error in discovery.Errors)
        {
            progress?.Report(new DeployResult("discovery", "", "", ResultLevel.Error, error));
        }

        bool hasErrors = discovery.Errors.Length > 0;
        Platform currentPlatform = _platformDetector.CurrentPlatform;
        MachineProfile? machineProfile = await _machineProfileService.LoadAsync(configRepoPath, cancellationToken).ConfigureAwait(false);

        var eligibleModules = new List<AppModule>();
        foreach (AppModule module in discovery.Modules)
        {
            string? skipReason = GetSkipReason(module, currentPlatform, machineProfile);
            if (skipReason != null)
            {
                progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, skipReason, DeployEventType.ModuleSkipped));
            }
            else
            {
                progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, "", DeployEventType.ModuleDiscovered));
                eligibleModules.Add(module);
            }
        }

        IReadOnlyDictionary<string, string>? variables = machineProfile?.Variables;

        if (!dryRun)
        {
            IReadOnlyList<string> allTargetPaths = CollectTargetPaths(discovery.Modules, currentPlatform, variables);
            _snapshotProvider.CreateSnapshot(allTargetPaths, cancellationToken);
        }

        foreach (AppModule module in eligibleModules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (beforeModule != null)
            {
                IReadOnlyList<DeployResult> preview = dryRun
                    ? CollectModulePreview(module, currentPlatform, variables)
                    : await CollectModulePreviewAsync(module, currentPlatform, variables, cancellationToken).ConfigureAwait(false);

                ModuleAction action = await beforeModule(module, preview).ConfigureAwait(false);
                if (action == ModuleAction.Skip)
                {
                    progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, "Skipped (user)", DeployEventType.ModuleSkipped));
                    continue;
                }

                if (action == ModuleAction.Abort)
                {
                    break;
                }
            }

            progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, "", DeployEventType.ModuleStarted));

            bool moduleHadErrors = await DeployModuleAsync(module, currentPlatform, variables, dryRun, progress, cancellationToken).ConfigureAwait(false);

            ResultLevel completionLevel = moduleHadErrors ? ResultLevel.Error : ResultLevel.Ok;
            progress?.Report(new DeployResult(module.DisplayName, "", "", completionLevel, "", DeployEventType.ModuleCompleted));

            if (moduleHadErrors)
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

    private IReadOnlyList<DeployResult> CollectModulePreview(AppModule module, Platform currentPlatform, IReadOnlyDictionary<string, string>? variables)
    {
        var results = new List<DeployResult>();
        var previewProgress = new SynchronousProgress<DeployResult>(results.Add);
        ProcessModuleLinks(module, currentPlatform, variables, true, previewProgress);
        ProcessModuleRegistry(module, true, previewProgress);
        return results;
    }

    private async Task<IReadOnlyList<DeployResult>> CollectModulePreviewAsync(AppModule module, Platform currentPlatform, IReadOnlyDictionary<string, string>? variables, CancellationToken cancellationToken)
    {
        var results = new List<DeployResult>();
        var previewProgress = new SynchronousProgress<DeployResult>(results.Add);
        ProcessModuleLinks(module, currentPlatform, variables, true, previewProgress);
        ProcessModuleRegistry(module, true, previewProgress);
        await ProcessModuleGlobalPackagesAsync(module, true, previewProgress, cancellationToken).ConfigureAwait(false);
        await ProcessListAsync(module.VscodeExtensions, id => _vscodeExtensionInstaller.InstallAsync(module.DisplayName, id, true, cancellationToken), previewProgress, cancellationToken).ConfigureAwait(false);
        await ProcessListAsync(module.PsModules, name => _psModuleInstaller.InstallAsync(module.DisplayName, name, true, cancellationToken), previewProgress, cancellationToken).ConfigureAwait(false);
        return results;
    }

    private async Task<bool> DeployModuleAsync(AppModule module, Platform currentPlatform, IReadOnlyDictionary<string, string>? variables, bool dryRun, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
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

        bool moduleHadErrors = ProcessModuleLinks(module, currentPlatform, variables, dryRun, progress);
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

            if (!package.Manager.IsPlatformMatch(currentPlatform))
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


    private bool ProcessModuleLinks(AppModule module, Platform currentPlatform, IReadOnlyDictionary<string, string>? variables, bool dryRun, IProgress<DeployResult>? progress)
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

            string expandedTarget = EnvironmentExpander.Expand(target, variables);
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

    private IReadOnlyList<string> CollectTargetPaths(System.Collections.Immutable.ImmutableArray<AppModule> modules, Platform currentPlatform, IReadOnlyDictionary<string, string>? variables)
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

                string expandedTarget = EnvironmentExpander.Expand(target, variables);
                IReadOnlyList<string> resolvedTargets = _globResolver.Resolve(expandedTarget);
                targets.AddRange(resolvedTargets);
            }
        }

        return targets;
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
