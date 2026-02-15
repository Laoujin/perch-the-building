using System.Collections.Immutable;

namespace Perch.Core.Modules;

public sealed class ModuleDiscoveryService : IModuleDiscoveryService
{
    private readonly ManifestParser _parser;

    public ModuleDiscoveryService(ManifestParser parser)
    {
        _parser = parser;
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

        string[] subdirectories = Directory.GetDirectories(configRepoPath);
        Array.Sort(subdirectories, StringComparer.OrdinalIgnoreCase);

        foreach (string subdir in subdirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string manifestPath = Path.Combine(subdir, "manifest.yaml");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            string yaml = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            string moduleName = Path.GetFileName(subdir);
            ManifestParseResult parseResult = _parser.Parse(yaml, moduleName);

            if (parseResult.IsSuccess)
            {
                AppManifest manifest = parseResult.Manifest!;
                modules.Add(new AppModule(manifest.ModuleName, manifest.DisplayName, subdir, manifest.Platforms, manifest.Links));
            }
            else
            {
                errors.Add($"[{moduleName}] {parseResult.Error}");
            }
        }

        return new DiscoveryResult(modules.ToImmutableArray(), errors.ToImmutableArray());
    }
}
