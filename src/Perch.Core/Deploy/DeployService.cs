using Perch.Core.Backup;
using Perch.Core.Modules;
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

    public DeployService(IModuleDiscoveryService discoveryService, SymlinkOrchestrator orchestrator, IPlatformDetector platformDetector, IGlobResolver globResolver, ISnapshotProvider snapshotProvider, IHookRunner hookRunner)
    {
        _discoveryService = discoveryService;
        _orchestrator = orchestrator;
        _platformDetector = platformDetector;
        _globResolver = globResolver;
        _snapshotProvider = snapshotProvider;
        _hookRunner = hookRunner;
    }

    public async Task<int> DeployAsync(string configRepoPath, bool dryRun = false, IProgress<DeployResult>? progress = null, CancellationToken cancellationToken = default)
    {
        DiscoveryResult discovery = await _discoveryService.DiscoverAsync(configRepoPath, cancellationToken).ConfigureAwait(false);

        foreach (string error in discovery.Errors)
        {
            progress?.Report(new DeployResult("discovery", "", "", ResultLevel.Error, error));
        }

        bool hasErrors = discovery.Errors.Length > 0;
        Platform currentPlatform = _platformDetector.CurrentPlatform;

        if (!dryRun)
        {
            IReadOnlyList<string> allTargetPaths = CollectTargetPaths(discovery.Modules, currentPlatform);
            _snapshotProvider.CreateSnapshot(allTargetPaths, cancellationToken);
        }

        foreach (AppModule module in discovery.Modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (module.Platforms.Length > 0 && !module.Platforms.Contains(currentPlatform))
            {
                progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok,
                    $"Skipped (not for {currentPlatform})"));
                continue;
            }

            if (await DeployModuleAsync(module, currentPlatform, dryRun, progress, cancellationToken).ConfigureAwait(false))
            {
                hasErrors = true;
            }
        }

        return hasErrors ? 1 : 0;
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
