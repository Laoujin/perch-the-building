using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Perch.Core.Config;

public sealed class YamlSettingsProvider : ISettingsProvider
{
    private readonly string _settingsPath;

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer PlainSerializer = new SerializerBuilder().Build();

    private static readonly IDeserializer PlainDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    public YamlSettingsProvider()
        : this(GetDefaultPath())
    {
    }

    internal YamlSettingsProvider(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public async Task<PerchSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await LoadMergedSettingsAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(settings.ConfigRepoPath))
            {
                var discovered = DiscoverConfigRepoFromSymlink();
                if (discovered != null)
                    settings = settings with { ConfigRepoPath = discovered };
            }

            return settings;
        }
        catch
        {
            return new PerchSettings();
        }
    }

    public async Task SaveAsync(PerchSettings settings, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string yaml = Serializer.Serialize(settings);
        await File.WriteAllTextAsync(_settingsPath, yaml, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PerchSettings> LoadMergedSettingsAsync(CancellationToken cancellationToken)
    {
        var baseDict = await LoadAsDictionaryAsync(_settingsPath, cancellationToken).ConfigureAwait(false);

        var localPath = Path.Combine(
            Path.GetDirectoryName(_settingsPath)!,
            "settings.local.yaml");
        var localDict = await LoadAsDictionaryAsync(localPath, cancellationToken).ConfigureAwait(false);

        foreach (var kvp in localDict)
            baseDict[kvp.Key] = kvp.Value;

        if (baseDict.Count == 0)
            return new PerchSettings();

        var mergedYaml = PlainSerializer.Serialize(baseDict);
        return Deserializer.Deserialize<PerchSettings>(mergedYaml) ?? new PerchSettings();
    }

    private static async Task<Dictionary<string, object?>> LoadAsDictionaryAsync(
        string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return new Dictionary<string, object?>();

        try
        {
            var yaml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return PlainDeserializer.Deserialize<Dictionary<string, object?>>(yaml)
                ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    internal string? DiscoverConfigRepoFromSymlink()
    {
        try
        {
            var linkTarget = new FileInfo(_settingsPath).LinkTarget;
            if (linkTarget == null)
                return null;

            if (!Path.IsPathFullyQualified(linkTarget))
            {
                var settingsDir = Path.GetDirectoryName(_settingsPath)!;
                linkTarget = Path.GetFullPath(Path.Combine(settingsDir, linkTarget));
            }

            // Convention: <config-repo>/<module>/settings.yaml
            var moduleDir = Path.GetDirectoryName(linkTarget);
            return moduleDir != null ? Path.GetDirectoryName(moduleDir) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetDefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "perch", "settings.yaml");
}
