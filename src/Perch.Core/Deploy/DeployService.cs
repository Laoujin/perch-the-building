using Perch.Core.Modules;
using Perch.Core.Symlinks;

namespace Perch.Core.Deploy;

public sealed class DeployService : IDeployService
{
    private readonly IModuleDiscoveryService _discoveryService;
    private readonly SymlinkOrchestrator _orchestrator;
    private readonly IPlatformDetector _platformDetector;
    private readonly IGlobResolver _globResolver;

    public DeployService(IModuleDiscoveryService discoveryService, SymlinkOrchestrator orchestrator, IPlatformDetector platformDetector, IGlobResolver globResolver)
    {
        _discoveryService = discoveryService;
        _orchestrator = orchestrator;
        _platformDetector = platformDetector;
        _globResolver = globResolver;
    }

    public async Task<int> DeployAsync(string configRepoPath, IProgress<DeployResult>? progress = null, CancellationToken cancellationToken = default)
    {
        DiscoveryResult discovery = await _discoveryService.DiscoverAsync(configRepoPath, cancellationToken).ConfigureAwait(false);

        foreach (string error in discovery.Errors)
        {
            progress?.Report(new DeployResult("discovery", "", "", ResultLevel.Error, error));
        }

        bool hasErrors = discovery.Errors.Length > 0;
        Platform currentPlatform = _platformDetector.CurrentPlatform;

        foreach (AppModule module in discovery.Modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (module.Platforms.Length > 0 && !module.Platforms.Contains(currentPlatform))
            {
                progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok,
                    $"Skipped (not for {currentPlatform})"));
                continue;
            }

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
                    DeployResult result = _orchestrator.ProcessLink(module.DisplayName, sourcePath, resolvedTarget, link.LinkType);
                    progress?.Report(result);

                    if (result.Level == ResultLevel.Error)
                    {
                        hasErrors = true;
                    }
                }
            }
        }

        return hasErrors ? 1 : 0;
    }
}
