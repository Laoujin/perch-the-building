using YamlDotNet.Serialization;

namespace Perch.Core.Catalog;

internal sealed class AppCatalogYamlModel
{
    public string? Name { get; set; }

    [YamlMember(Alias = "display-name")]
    public string? DisplayName { get; set; }

    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
    public string? Description { get; set; }
    public string? Logo { get; set; }
    public string? Kind { get; set; }
    public List<string>? Profiles { get; set; }
    public List<string>? Os { get; set; }
    public bool Hidden { get; set; }
    public string? License { get; set; }
    public CatalogLinksYamlModel? Links { get; set; }
    public InstallYamlModel? Install { get; set; }
    public CatalogConfigYamlModel? Config { get; set; }
    public CatalogExtensionsYamlModel? Extensions { get; set; }
    public List<AppOwnedTweakYamlModel>? Tweaks { get; set; }
    public List<string>? Alternatives { get; set; }
    public List<string>? Suggests { get; set; }
    public List<string>? Requires { get; set; }
}

internal sealed class FontCatalogYamlModel
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
    public string? Description { get; set; }
    public string? Logo { get; set; }
    public List<string>? Profiles { get; set; }
    public bool Hidden { get; set; }
    public string? License { get; set; }

    [YamlMember(Alias = "preview-text")]
    public string? PreviewText { get; set; }

    public InstallYamlModel? Install { get; set; }
}

internal sealed class TweakCatalogYamlModel
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
    public string? Description { get; set; }
    public bool Reversible { get; set; }
    public List<string>? Profiles { get; set; }
    public bool Hidden { get; set; }
    public string? License { get; set; }
    public string? Source { get; set; }
    public List<TweakRegistryYamlModel>? Registry { get; set; }
    public string? Script { get; set; }

    [YamlMember(Alias = "undo-script")]
    public string? UndoScript { get; set; }

    public List<string>? Suggests { get; set; }
    public List<string>? Requires { get; set; }
    public List<string>? Alternatives { get; set; }

    [YamlMember(Alias = "windows-versions")]
    public List<int>? WindowsVersions { get; set; }
}

internal sealed class AppOwnedTweakYamlModel
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool Reversible { get; set; }
    public List<TweakRegistryYamlModel>? Registry { get; set; }
    public string? Script { get; set; }

    [YamlMember(Alias = "undo-script")]
    public string? UndoScript { get; set; }
}

internal sealed class CatalogLinksYamlModel
{
    public string? Website { get; set; }
    public string? Docs { get; set; }
    public string? GitHub { get; set; }
}

internal sealed class InstallYamlModel
{
    public string? Winget { get; set; }
    public string? Choco { get; set; }

    [YamlMember(Alias = "dotnet-tool")]
    public string? DotnetTool { get; set; }

    [YamlMember(Alias = "node-package")]
    public string? NodePackage { get; set; }
}

internal sealed class CatalogConfigYamlModel
{
    public List<CatalogConfigLinkYamlModel>? Links { get; set; }

    [YamlMember(Alias = "clean-filter")]
    public CatalogCleanFilterYamlModel? CleanFilter { get; set; }
}

internal sealed class CatalogConfigLinkYamlModel
{
    public string? Source { get; set; }
    public Dictionary<string, string>? Target { get; set; }

    [YamlMember(Alias = "link-type")]
    public string? LinkType { get; set; }

    public List<string>? Platforms { get; set; }
    public bool Template { get; set; }
}

internal sealed class CatalogCleanFilterYamlModel
{
    public List<string>? Files { get; set; }
    public List<CatalogFilterRuleYamlModel>? Rules { get; set; }
}

internal sealed class CatalogFilterRuleYamlModel
{
    public string? Type { get; set; }
    public List<string>? Elements { get; set; }
    public List<string>? Keys { get; set; }
}

internal sealed class CatalogExtensionsYamlModel
{
    public List<string>? Bundled { get; set; }
    public List<string>? Recommended { get; set; }
}

internal sealed class TweakRegistryYamlModel
{
    public string? Key { get; set; }
    public string? Name { get; set; }
    public object? Value { get; set; }
    public string? Type { get; set; }

    [YamlMember(Alias = "default-value")]
    public object? DefaultValue { get => _defaultValue; set { _defaultValue = value; HasDefaultValue = true; } }
    private object? _defaultValue;

    [YamlIgnore]
    public bool HasDefaultValue { get; private set; }
}

internal sealed class GitHubStarYamlModel
{
    public string? Repo { get; set; }
    public int Stars { get; set; }
}

internal sealed class CatalogIndexYamlModel
{
    public List<CatalogIndexEntryYamlModel>? Apps { get; set; }
    public List<CatalogIndexEntryYamlModel>? Fonts { get; set; }
    public List<CatalogIndexEntryYamlModel>? Tweaks { get; set; }
}

internal sealed class CatalogIndexEntryYamlModel
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
    public string? Kind { get; set; }
    public List<string>? Profiles { get; set; }
    public bool Hidden { get; set; }
    public string? Path { get; set; }
}
