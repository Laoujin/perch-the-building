namespace Perch.Core.Modules;

internal sealed class ManifestYamlModel
{
    public string? DisplayName { get; set; }
    public List<string>? Platforms { get; set; }
    public List<LinkYamlModel>? Links { get; set; }
}

internal sealed class LinkYamlModel
{
    public string? Source { get; set; }
    public object? Target { get; set; }
    public string? LinkType { get; set; }
}
