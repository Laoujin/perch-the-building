namespace Perch.Core.Modules;

internal sealed class ManifestYamlModel
{
    public bool Enabled { get; set; } = true;
    public string? DisplayName { get; set; }
    public List<string>? Platforms { get; set; }
    public List<LinkYamlModel>? Links { get; set; }
    public HooksYamlModel? Hooks { get; set; }
    public CleanFilterYamlModel? CleanFilter { get; set; }
    public List<RegistryYamlModel>? Registry { get; set; }
    public GlobalPackagesYamlModel? GlobalPackages { get; set; }
    public List<string>? VscodeExtensions { get; set; }
    public List<string>? PsModules { get; set; }
}

internal sealed class GlobalPackagesYamlModel
{
    public string? Manager { get; set; }
    public List<string>? Packages { get; set; }
}

internal sealed class HooksYamlModel
{
    public string? PreDeploy { get; set; }
    public string? PostDeploy { get; set; }
}

internal sealed class CleanFilterYamlModel
{
    public string? Name { get; set; }
    public string? Script { get; set; }
    public List<string>? Files { get; set; }
    public List<FilterRuleYamlModel>? Rules { get; set; }
}

internal sealed class FilterRuleYamlModel
{
    public string? Type { get; set; }
    public List<string>? Elements { get; set; }
    public List<string>? Keys { get; set; }
}

internal sealed class LinkYamlModel
{
    public string? Source { get; set; }
    public object? Target { get; set; }
    public string? LinkType { get; set; }
    public bool Template { get; set; }
}

internal sealed class RegistryYamlModel
{
    public string? Key { get; set; }
    public string? Name { get; set; }
    public object? Value { get; set; }
    public string? Type { get; set; }
}
