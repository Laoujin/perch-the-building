using System.Collections.Immutable;
using Perch.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Perch.Core.Modules;

public sealed class ManifestParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public ManifestParseResult Parse(string yaml, string moduleName)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return ManifestParseResult.Failure("Manifest is empty.");
        }

        ManifestYamlModel model;
        try
        {
            model = Deserializer.Deserialize<ManifestYamlModel>(yaml);
        }
        catch (Exception ex)
        {
            return ManifestParseResult.Failure($"Invalid YAML: {ex.Message}");
        }

        if (model?.Links == null || model.Links.Count == 0)
        {
            return ManifestParseResult.Failure("Manifest must contain at least one entry in 'links'.");
        }

        var links = new List<LinkEntry>();
        for (int i = 0; i < model.Links.Count; i++)
        {
            var link = model.Links[i];
            if (string.IsNullOrWhiteSpace(link.Source))
            {
                return ManifestParseResult.Failure($"Link [{i}] is missing 'source'.");
            }

            if (link.Target == null)
            {
                return ManifestParseResult.Failure($"Link [{i}] is missing 'target'.");
            }

            var linkType = ParseLinkType(link.LinkType);
            var entry = ParseTarget(link.Source, link.Target, linkType);
            if (entry == null)
            {
                return ManifestParseResult.Failure($"Link [{i}] has an invalid 'target'.");
            }

            links.Add(entry);
        }

        string displayName = string.IsNullOrWhiteSpace(model.DisplayName) ? moduleName : model.DisplayName;
        var platforms = ParsePlatforms(model.Platforms);
        var hooks = ParseHooks(model.Hooks);
        var cleanFilter = ParseCleanFilter(model.CleanFilter);
        var manifest = new AppManifest(moduleName, displayName, platforms, links.ToImmutableArray(), hooks, cleanFilter);
        return ManifestParseResult.Success(manifest);
    }

    private static ImmutableArray<Platform> ParsePlatforms(List<string>? values)
    {
        if (values == null || values.Count == 0)
        {
            return ImmutableArray<Platform>.Empty;
        }

        var platforms = new List<Platform>();
        foreach (string value in values)
        {
            if (Enum.TryParse<Platform>(value, ignoreCase: true, out var platform))
            {
                platforms.Add(platform);
            }
        }

        return platforms.ToImmutableArray();
    }

    private static LinkEntry? ParseTarget(string source, object target, LinkType linkType)
    {
        if (target is string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : new LinkEntry(source, s, linkType);
        }

        if (target is Dictionary<object, object> dict)
        {
            var platformTargets = new Dictionary<Platform, string>();
            foreach (var kvp in dict)
            {
                if (kvp.Key is string key && kvp.Value is string value
                    && Enum.TryParse<Platform>(key, ignoreCase: true, out var platform))
                {
                    platformTargets[platform] = value;
                }
            }

            return platformTargets.Count > 0
                ? new LinkEntry(source, null, platformTargets.ToImmutableDictionary(), linkType)
                : null;
        }

        return null;
    }

    private static DeployHooks? ParseHooks(HooksYamlModel? hooks)
    {
        if (hooks == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(hooks.PreDeploy) && string.IsNullOrWhiteSpace(hooks.PostDeploy))
        {
            return null;
        }

        return new DeployHooks(hooks.PreDeploy, hooks.PostDeploy);
    }

    private static CleanFilterDefinition? ParseCleanFilter(CleanFilterYamlModel? model)
    {
        if (model == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Script) || model.Files == null || model.Files.Count == 0)
        {
            return null;
        }

        return new CleanFilterDefinition(model.Name!, model.Script!, model.Files.ToImmutableArray());
    }

    private static LinkType ParseLinkType(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "junction" => LinkType.Junction,
            _ => LinkType.Symlink,
        };
}
