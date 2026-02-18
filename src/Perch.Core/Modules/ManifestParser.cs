using System.Collections.Immutable;
using Perch.Core;
using Perch.Core.Git;
using Perch.Core.Registry;
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

        if (!HasActionableContent(model))
        {
            return ManifestParseResult.Failure("Manifest must contain at least one actionable section.");
        }

        string? linksError = ParseLinks(model!.Links, out var links);
        if (linksError != null)
        {
            return ManifestParseResult.Failure(linksError);
        }

        string displayName = string.IsNullOrWhiteSpace(model.DisplayName) ? moduleName : model.DisplayName;
        string? galleryId = string.IsNullOrWhiteSpace(model.Gallery) ? null : model.Gallery;
        var manifest = new AppManifest(
            moduleName, displayName, model.Enabled,
            ParsePlatforms(model.Platforms), links,
            ParseHooks(model.Hooks), ParseCleanFilter(model.CleanFilter),
            ParseRegistry(model.Registry), ParseGlobalPackages(model.GlobalPackages),
            ParseStringList(model.VscodeExtensions), ParseStringList(model.PsModules), galleryId);
        return ManifestParseResult.Success(manifest);
    }

    private static bool HasActionableContent(ManifestYamlModel? model) =>
        (model?.Links != null && model.Links.Count > 0) ||
        (model?.Registry != null && model.Registry.Count > 0) ||
        (model?.GlobalPackages?.Packages != null && model.GlobalPackages.Packages.Count > 0) ||
        (model?.VscodeExtensions != null && model.VscodeExtensions.Count > 0) ||
        (model?.PsModules != null && model.PsModules.Count > 0) ||
        !string.IsNullOrWhiteSpace(model?.Gallery);

    private static string? ParseLinks(List<LinkYamlModel>? linkModels, out ImmutableArray<LinkEntry> links)
    {
        var result = new List<LinkEntry>();
        for (int i = 0; i < (linkModels?.Count ?? 0); i++)
        {
            var link = linkModels![i];
            if (string.IsNullOrWhiteSpace(link.Source))
            {
                links = ImmutableArray<LinkEntry>.Empty;
                return $"Link [{i}] is missing 'source'.";
            }

            if (link.Target == null)
            {
                links = ImmutableArray<LinkEntry>.Empty;
                return $"Link [{i}] is missing 'target'.";
            }

            var linkType = ParseLinkType(link.LinkType);
            var entry = ParseTarget(link.Source, link.Target, linkType, link.Template);
            if (entry == null)
            {
                links = ImmutableArray<LinkEntry>.Empty;
                return $"Link [{i}] has an invalid 'target'.";
            }

            result.Add(entry);
        }

        links = result.ToImmutableArray();
        return null;
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

    private static LinkEntry? ParseTarget(string source, object target, LinkType linkType, bool isTemplate)
    {
        if (target is string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : new LinkEntry(source, s, null, linkType, isTemplate);
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
                ? new LinkEntry(source, null, platformTargets.ToImmutableDictionary(), linkType, isTemplate)
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

        if (string.IsNullOrWhiteSpace(model.Name) || model.Files == null || model.Files.Count == 0)
        {
            return null;
        }

        bool hasScript = !string.IsNullOrWhiteSpace(model.Script);
        var rules = ParseFilterRules(model.Rules);

        if (!hasScript && rules.IsDefaultOrEmpty)
        {
            return null;
        }

        return new CleanFilterDefinition(model.Name!, hasScript ? model.Script : null, model.Files.ToImmutableArray(), rules);
    }

    private static ImmutableArray<FilterRule> ParseFilterRules(List<FilterRuleYamlModel>? rules)
    {
        if (rules == null || rules.Count == 0)
        {
            return ImmutableArray<FilterRule>.Empty;
        }

        var result = new List<FilterRule>();
        foreach (FilterRuleYamlModel rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Type))
            {
                continue;
            }

            var patterns = rule.Type switch
            {
                "strip-xml-elements" => rule.Elements?.Where(e => !string.IsNullOrWhiteSpace(e)).ToImmutableArray() ?? ImmutableArray<string>.Empty,
                "strip-ini-keys" => rule.Keys?.Where(k => !string.IsNullOrWhiteSpace(k)).ToImmutableArray() ?? ImmutableArray<string>.Empty,
                "strip-json-keys" => rule.Keys?.Where(k => !string.IsNullOrWhiteSpace(k)).ToImmutableArray() ?? ImmutableArray<string>.Empty,
                _ => ImmutableArray<string>.Empty,
            };

            if (patterns.Length > 0)
            {
                result.Add(new FilterRule(rule.Type!, patterns));
            }
        }

        return result.ToImmutableArray();
    }

    private static ImmutableArray<RegistryEntryDefinition> ParseRegistry(List<RegistryYamlModel>? entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return ImmutableArray<RegistryEntryDefinition>.Empty;
        }

        var result = new List<RegistryEntryDefinition>();
        foreach (RegistryYamlModel entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Name) || entry.Value == null)
            {
                continue;
            }

            RegistryValueType kind = ParseRegistryValueType(entry.Type);
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

    private static GlobalPackagesDefinition? ParseGlobalPackages(GlobalPackagesYamlModel? model)
    {
        if (model?.Packages == null || model.Packages.Count == 0)
        {
            return null;
        }

        var manager = model.Manager?.ToLowerInvariant() switch
        {
            "bun" => GlobalPackageManager.Bun,
            _ => GlobalPackageManager.Npm,
        };

        var packages = model.Packages
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToImmutableArray();

        return packages.Length > 0 ? new GlobalPackagesDefinition(manager, packages) : null;
    }

    private static ImmutableArray<string> ParseStringList(List<string>? items) =>
        items == null || items.Count == 0
            ? ImmutableArray<string>.Empty
            : items.Where(s => !string.IsNullOrWhiteSpace(s)).ToImmutableArray();

    private static LinkType ParseLinkType(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "junction" => LinkType.Junction,
            _ => LinkType.Symlink,
        };
}
