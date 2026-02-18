using Perch.Core.Config;
using Perch.Core.Machines;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Perch.Core.Registry;

public sealed class YamlCapturedRegistryStore : ICapturedRegistryStore
{
    private readonly ISettingsProvider? _settingsProvider;
    private readonly string? _configRepoPath;
    private readonly string _hostname;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public YamlCapturedRegistryStore(ISettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
        _hostname = Environment.MachineName;
    }

    internal YamlCapturedRegistryStore(string configRepoPath, string hostname)
    {
        _configRepoPath = configRepoPath;
        _hostname = hostname;
    }

    public async Task<CapturedRegistryData> LoadAsync(CancellationToken cancellationToken = default)
    {
        string? filePath = await ResolveFilePathAsync(cancellationToken).ConfigureAwait(false);
        if (filePath == null)
            return new CapturedRegistryData();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
                return new CapturedRegistryData();

            string yaml = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var model = Deserializer.Deserialize<MachineProfileYamlModel>(yaml);
            return ToData(model?.CapturedRegistry);
        }
        catch (Exception)
        {
            return new CapturedRegistryData();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(CapturedRegistryData data, CancellationToken cancellationToken = default)
    {
        string? filePath = await ResolveFilePathAsync(cancellationToken).ConfigureAwait(false);
        if (filePath == null)
            return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            MachineProfileYamlModel model;
            if (File.Exists(filePath))
            {
                string existing = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                model = Deserializer.Deserialize<MachineProfileYamlModel>(existing) ?? new MachineProfileYamlModel();
            }
            else
            {
                model = new MachineProfileYamlModel();
            }

            model.CapturedRegistry = data.Entries.Count > 0
                ? new Dictionary<string, CapturedRegistryEntry>(data.Entries, StringComparer.OrdinalIgnoreCase)
                : null;

            string yaml = Serializer.Serialize(model);
            await File.WriteAllTextAsync(filePath, yaml, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string?> ResolveFilePathAsync(CancellationToken cancellationToken)
    {
        string? configRepo = _configRepoPath;
        if (configRepo == null && _settingsProvider != null)
        {
            var settings = await _settingsProvider.LoadAsync(cancellationToken).ConfigureAwait(false);
            configRepo = settings.ConfigRepoPath;
        }

        if (string.IsNullOrEmpty(configRepo))
            return null;

        return Path.Combine(configRepo, "machines", $"{_hostname}.yaml");
    }

    private static CapturedRegistryData ToData(Dictionary<string, CapturedRegistryEntry>? entries)
    {
        var data = new CapturedRegistryData();
        if (entries == null)
            return data;

        foreach (var kvp in entries)
            data.Entries[kvp.Key] = kvp.Value;

        return data;
    }
}
