using System.Collections.Immutable;

using Perch.Core.Modules;
using Perch.Core.Registry;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Perch.Core.Catalog;

public sealed class CatalogParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public CatalogParseResult<CatalogEntry> ParseApp(string yaml, string id)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return CatalogParseResult<CatalogEntry>.Failure("YAML content is empty.");
        }

        AppCatalogYamlModel model;
        try
        {
            model = Deserializer.Deserialize<AppCatalogYamlModel>(yaml);
        }
        catch (YamlException ex)
        {
            return CatalogParseResult<CatalogEntry>.Failure($"Invalid YAML: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return CatalogParseResult<CatalogEntry>.Failure("App entry is missing 'name'.");
        }

        var entry = new CatalogEntry(
            id,
            model.Name!,
            model.DisplayName,
            model.Category ?? "Uncategorized",
            ToImmutableTags(model.Tags),
            model.Description,
            model.Logo,
            ParseLinks(model.Links),
            ParseInstall(model.Install),
            ParseConfig(model.Config),
            ParseExtensions(model.Extensions));

        return CatalogParseResult<CatalogEntry>.Ok(entry);
    }

    public CatalogParseResult<FontCatalogEntry> ParseFont(string yaml, string id)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return CatalogParseResult<FontCatalogEntry>.Failure("YAML content is empty.");
        }

        FontCatalogYamlModel model;
        try
        {
            model = Deserializer.Deserialize<FontCatalogYamlModel>(yaml);
        }
        catch (YamlException ex)
        {
            return CatalogParseResult<FontCatalogEntry>.Failure($"Invalid YAML: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return CatalogParseResult<FontCatalogEntry>.Failure("Font entry is missing 'name'.");
        }

        var entry = new FontCatalogEntry(
            id,
            model.Name!,
            model.Category ?? "Fonts",
            ToImmutableTags(model.Tags),
            model.Description,
            model.Logo,
            model.PreviewText,
            ParseInstall(model.Install));

        return CatalogParseResult<FontCatalogEntry>.Ok(entry);
    }

    public CatalogParseResult<TweakCatalogEntry> ParseTweak(string yaml, string id)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return CatalogParseResult<TweakCatalogEntry>.Failure("YAML content is empty.");
        }

        TweakCatalogYamlModel model;
        try
        {
            model = Deserializer.Deserialize<TweakCatalogYamlModel>(yaml);
        }
        catch (YamlException ex)
        {
            return CatalogParseResult<TweakCatalogEntry>.Failure($"Invalid YAML: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return CatalogParseResult<TweakCatalogEntry>.Failure("Tweak entry is missing 'name'.");
        }

        var registryEntries = ParseTweakRegistry(model.Registry);

        var entry = new TweakCatalogEntry(
            id,
            model.Name!,
            model.Category ?? "Uncategorized",
            ToImmutableTags(model.Tags),
            model.Description,
            model.Reversible,
            ToImmutableTags(model.Profiles),
            model.Priority,
            registryEntries);

        return CatalogParseResult<TweakCatalogEntry>.Ok(entry);
    }

    public CatalogParseResult<CatalogIndex> ParseIndex(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CatalogParseResult<CatalogIndex>.Failure("Index content is empty.");
        }

        CatalogIndexYamlModel model;
        try
        {
            model = Deserializer.Deserialize<CatalogIndexYamlModel>(json);
        }
        catch (YamlException ex)
        {
            return CatalogParseResult<CatalogIndex>.Failure($"Invalid index: {ex.Message}");
        }

        var index = new CatalogIndex(
            ParseIndexEntries(model.Apps),
            ParseIndexEntries(model.Fonts),
            ParseIndexEntries(model.Tweaks));

        return CatalogParseResult<CatalogIndex>.Ok(index);
    }

    private static ImmutableArray<CatalogIndexEntry> ParseIndexEntries(List<CatalogIndexEntryYamlModel>? entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return ImmutableArray<CatalogIndexEntry>.Empty;
        }

        return entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Name))
            .Select(e => new CatalogIndexEntry(
                e.Id!,
                e.Name!,
                e.Category ?? "Uncategorized",
                ToImmutableTags(e.Tags)))
            .ToImmutableArray();
    }

    private static ImmutableArray<string> ToImmutableTags(List<string>? tags) =>
        tags == null || tags.Count == 0
            ? ImmutableArray<string>.Empty
            : tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToImmutableArray();

    private static CatalogLinks? ParseLinks(CatalogLinksYamlModel? model) =>
        model == null ? null : new CatalogLinks(model.Website, model.Docs, model.GitHub);

    private static InstallDefinition? ParseInstall(InstallYamlModel? model) =>
        model == null ? null : new InstallDefinition(model.Winget, model.Choco);

    private static CatalogConfigDefinition? ParseConfig(CatalogConfigYamlModel? model)
    {
        if (model?.Links == null || model.Links.Count == 0)
        {
            return null;
        }

        var links = new List<CatalogConfigLink>();
        foreach (var link in model.Links)
        {
            if (string.IsNullOrWhiteSpace(link.Source) || link.Target == null)
            {
                continue;
            }

            var targets = new Dictionary<Platform, string>();
            foreach (var kvp in link.Target)
            {
                if (Enum.TryParse<Platform>(kvp.Key, ignoreCase: true, out var platform))
                {
                    targets[platform] = kvp.Value;
                }
            }

            if (targets.Count > 0)
            {
                links.Add(new CatalogConfigLink(link.Source, targets.ToImmutableDictionary()));
            }
        }

        return links.Count > 0 ? new CatalogConfigDefinition(links.ToImmutableArray()) : null;
    }

    private static CatalogExtensions? ParseExtensions(CatalogExtensionsYamlModel? model)
    {
        if (model == null)
        {
            return null;
        }

        return new CatalogExtensions(
            ToImmutableTags(model.Bundled),
            ToImmutableTags(model.Recommended));
    }

    private static ImmutableArray<RegistryEntryDefinition> ParseTweakRegistry(List<TweakRegistryYamlModel>? entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return ImmutableArray<RegistryEntryDefinition>.Empty;
        }

        var result = new List<RegistryEntryDefinition>();
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Name == null || entry.Value == null)
            {
                continue;
            }

            var kind = ParseRegistryValueType(entry.Type);
            object value = CoerceRegistryValue(entry.Value, kind);
            result.Add(new RegistryEntryDefinition(entry.Key!, entry.Name!, value, kind));
        }

        return result.ToImmutableArray();
    }

    private static RegistryValueType ParseRegistryValueType(string? type) =>
        type?.ToLowerInvariant() switch
        {
            "dword" => RegistryValueType.DWord,
            "qword" => RegistryValueType.QWord,
            "expandstring" => RegistryValueType.ExpandString,
            _ => RegistryValueType.String,
        };

    private static object CoerceRegistryValue(object value, RegistryValueType kind) =>
        kind switch
        {
            RegistryValueType.DWord when value is int i => i,
            RegistryValueType.DWord when value is long l => (int)l,
            RegistryValueType.DWord when value is string s && int.TryParse(s, out int parsed) => parsed,
            RegistryValueType.QWord when value is long l => l,
            RegistryValueType.QWord when value is int i => (long)i,
            RegistryValueType.QWord when value is string s && long.TryParse(s, out long parsed) => parsed,
            _ => value.ToString() ?? string.Empty,
        };
}
