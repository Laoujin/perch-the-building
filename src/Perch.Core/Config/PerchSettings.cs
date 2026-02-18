namespace Perch.Core.Config;

public sealed record PerchSettings
{
    public const string DefaultGalleryUrl = "https://laoujin.github.io/perch-gallery/";

    public string? ConfigRepoPath { get; init; }
    public string GalleryUrl { get; init; } = DefaultGalleryUrl;
    public string? GalleryLocalPath { get; init; }
    public List<string>? Profiles { get; init; }
    public bool DisableGalleryCache { get; init; }
    public bool Dev { get; init; }
}
