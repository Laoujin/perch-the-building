using System.Collections.Immutable;
using Perch.Core;
using Perch.Core.Catalog;

namespace Perch.Core.Modules;

public sealed record AppManifest(string ModuleName, string DisplayName, bool Enabled, ImmutableArray<Platform> Platforms, ImmutableArray<LinkEntry> Links, DeployHooks? Hooks = null, CleanFilterDefinition? CleanFilter = null, ImmutableArray<RegistryEntryDefinition> Registry = default, GlobalPackagesDefinition? GlobalPackages = null, ImmutableArray<string> VscodeExtensions = default, ImmutableArray<string> PsModules = default, string? GalleryId = null, ImmutableArray<PathEntry> PathEntries = default, InstallDefinition? Install = null);

public sealed record PathEntry(ImmutableDictionary<Platform, string> Paths)
{
    public string? GetPathForPlatform(Platform platform) =>
        Paths.TryGetValue(platform, out var path) ? path : null;
}
