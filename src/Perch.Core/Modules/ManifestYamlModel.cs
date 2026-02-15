namespace Perch.Core.Modules;

internal sealed class ManifestYamlModel
{
    public string? DisplayName { get; set; }
    public List<string>? Platforms { get; set; }
    public List<LinkYamlModel>? Links { get; set; }
    public HooksYamlModel? Hooks { get; set; }
    public CleanFilterYamlModel? CleanFilter { get; set; }
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
}

internal sealed class LinkYamlModel
{
    public string? Source { get; set; }
    public object? Target { get; set; }
    public string? LinkType { get; set; }
}
