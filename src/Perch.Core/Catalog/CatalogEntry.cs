using System.Collections.Immutable;

using Perch.Core.Git;
using Perch.Core.Modules;

namespace Perch.Core.Catalog;

public enum CatalogKind
{
    App,
    CliTool,
    Runtime,
    Dotfile,
}

public sealed record CatalogEntry(
    string Id,
    string Name,
    string? DisplayName,
    string Category,
    ImmutableArray<string> Tags,
    string? Description,
    string? Logo,
    CatalogLinks? Links,
    InstallDefinition? Install,
    CatalogConfigDefinition? Config,
    CatalogExtensions? Extensions,
    CatalogKind Kind = CatalogKind.App,
    ImmutableArray<AppOwnedTweak> Tweaks = default,
    ImmutableArray<string> Profiles = default,
    ImmutableArray<string> Os = default,
    bool Hidden = false,
    string? License = null,
    ImmutableArray<string> Alternatives = default,
    ImmutableArray<string> Suggests = default,
    ImmutableArray<string> Requires = default,
    bool Hot = false);

public sealed record CatalogLinks(string? Website, string? Docs, string? GitHub);

public sealed record InstallDefinition(
    string? Winget,
    string? Choco,
    string? DotnetTool = null,
    string? NodePackage = null,
    string? Detect = null);

public sealed record CatalogConfigDefinition(
    ImmutableArray<CatalogConfigLink> Links,
    CatalogCleanFilter? CleanFilter = null);

public sealed record CatalogConfigLink(
    string Source,
    ImmutableDictionary<Platform, string> Targets,
    LinkType LinkType = LinkType.Symlink,
    ImmutableArray<Platform> Platforms = default,
    bool Template = false);

public sealed record CatalogCleanFilter(
    ImmutableArray<string> Files,
    ImmutableArray<FilterRule> Rules);

public sealed record CatalogExtensions(
    ImmutableArray<string> Bundled,
    ImmutableArray<string> Recommended);

public sealed record AppOwnedTweak(
    string Id,
    string Name,
    string? Description,
    ImmutableArray<RegistryEntryDefinition> Registry,
    string? Script = null,
    string? UndoScript = null,
    bool Reversible = false)
{
    public TweakCatalogEntry ToTweakCatalogEntry(CatalogEntry owner) =>
        new(
            $"{owner.Id}/{Id}",
            Name,
            owner.Category,
            ImmutableArray<string>.Empty,
            Description,
            Reversible,
            owner.Profiles,
            Registry,
            Script,
            UndoScript,
            Hidden: owner.Hidden,
            License: owner.License);
}
