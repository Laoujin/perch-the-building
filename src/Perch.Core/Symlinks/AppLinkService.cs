using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Modules;

namespace Perch.Core.Symlinks;

public sealed class AppLinkService : IAppLinkService
{
    private readonly SymlinkOrchestrator _orchestrator;
    private readonly ISymlinkProvider _symlinkProvider;
    private readonly IPlatformDetector _platformDetector;
    private readonly ISettingsProvider _settingsProvider;

    public AppLinkService(
        SymlinkOrchestrator orchestrator,
        ISymlinkProvider symlinkProvider,
        IPlatformDetector platformDetector,
        ISettingsProvider settingsProvider)
    {
        _orchestrator = orchestrator;
        _symlinkProvider = symlinkProvider;
        _platformDetector = platformDetector;
        _settingsProvider = settingsProvider;
    }

    public async Task<IReadOnlyList<DeployResult>> LinkAppAsync(CatalogEntry app, CancellationToken ct = default)
    {
        if (app.Config is null || app.Config.Links.IsDefaultOrEmpty)
            return [];

        var settings = await _settingsProvider.LoadAsync(ct);
        var platform = _platformDetector.CurrentPlatform;
        var results = new List<DeployResult>();

        foreach (var link in app.Config.Links)
        {
            if (!link.Targets.TryGetValue(platform, out var targetPath))
                continue;

            var resolvedTarget = Environment.ExpandEnvironmentVariables(targetPath);
            var sourcePath = Path.GetFullPath(Path.Combine(settings.ConfigRepoPath!, link.Source));
            results.Add(_orchestrator.ProcessLink(app.Name, sourcePath, resolvedTarget, link.LinkType));
        }

        return results;
    }

    public async Task<IReadOnlyList<DeployResult>> UnlinkAppAsync(CatalogEntry app, CancellationToken ct = default)
    {
        if (app.Config is null || app.Config.Links.IsDefaultOrEmpty)
            return [];

        var platform = _platformDetector.CurrentPlatform;
        var results = new List<DeployResult>();

        foreach (var link in app.Config.Links)
        {
            if (!link.Targets.TryGetValue(platform, out var targetPath))
                continue;

            var resolvedTarget = Environment.ExpandEnvironmentVariables(targetPath);
            results.Add(UnlinkSingle(app.Name, link.Source, resolvedTarget));
        }

        await Task.CompletedTask;
        return results;
    }

    public async Task<IReadOnlyList<DeployResult>> FixAppLinksAsync(CatalogEntry app, CancellationToken ct = default)
    {
        if (app.Config is null || app.Config.Links.IsDefaultOrEmpty)
            return [];

        var settings = await _settingsProvider.LoadAsync(ct);
        var platform = _platformDetector.CurrentPlatform;
        var results = new List<DeployResult>();

        foreach (var link in app.Config.Links)
        {
            if (!link.Targets.TryGetValue(platform, out var targetPath))
                continue;

            var resolvedTarget = Environment.ExpandEnvironmentVariables(targetPath);
            var expectedSource = Path.GetFullPath(Path.Combine(settings.ConfigRepoPath!, link.Source));
            results.Add(FixSingle(app.Name, expectedSource, resolvedTarget));
        }

        return results;
    }

    private DeployResult UnlinkSingle(string moduleName, string source, string targetPath)
    {
        try
        {
            if (!_symlinkProvider.IsSymlink(targetPath))
                return new DeployResult(moduleName, source, targetPath, ResultLevel.Ok, "Not a symlink (skipped)");

            File.Delete(targetPath);
            return new DeployResult(moduleName, source, targetPath, ResultLevel.Ok, "Unlinked");
        }
        catch (Exception ex)
        {
            return new DeployResult(moduleName, source, targetPath, ResultLevel.Error, ex.Message);
        }
    }

    private DeployResult FixSingle(string moduleName, string expectedSource, string targetPath)
    {
        try
        {
            if (!_symlinkProvider.IsSymlink(targetPath))
                return new DeployResult(moduleName, expectedSource, targetPath, ResultLevel.Ok, "Not a symlink (skipped)");

            var currentTarget = _symlinkProvider.GetSymlinkTarget(targetPath);
            if (string.Equals(currentTarget, expectedSource, StringComparison.OrdinalIgnoreCase))
                return new DeployResult(moduleName, expectedSource, targetPath, ResultLevel.Ok, "Already correct (skipped)");

            File.Delete(targetPath);
            _symlinkProvider.CreateSymlink(targetPath, expectedSource);
            return new DeployResult(moduleName, expectedSource, targetPath, ResultLevel.Ok, "Relinked");
        }
        catch (Exception ex)
        {
            return new DeployResult(moduleName, expectedSource, targetPath, ResultLevel.Error, ex.Message);
        }
    }
}
