using System.Collections.Immutable;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Perch.Core.Packages;

public sealed class PackageManifestParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public PackageManifestParseResult Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return PackageManifestParseResult.Failure("Package manifest is empty.");
        }

        PackageYamlModel model;
        try
        {
            model = Deserializer.Deserialize<PackageYamlModel>(yaml);
        }
        catch (YamlException ex)
        {
            return PackageManifestParseResult.Failure($"Invalid YAML: {ex.Message}");
        }

        if (model?.Packages == null || model.Packages.Count == 0)
        {
            return PackageManifestParseResult.Failure("Package manifest must contain at least one entry in 'packages'.");
        }

        var packages = new List<PackageDefinition>();
        var errors = new List<string>();

        for (int i = 0; i < model.Packages.Count; i++)
        {
            var entry = model.Packages[i];

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                errors.Add($"Package [{i}] is missing 'name'.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Manager) ||
                !Enum.TryParse<PackageManager>(entry.Manager, ignoreCase: true, out var manager))
            {
                errors.Add($"Package [{i}] '{entry.Name}' has unknown or missing 'manager': '{entry.Manager}'.");
                continue;
            }

            packages.Add(new PackageDefinition(entry.Name, manager));
        }

        if (packages.Count > 0 && errors.Count > 0)
        {
            return PackageManifestParseResult.PartialSuccess(packages.ToImmutableArray(), errors.ToImmutableArray());
        }

        if (packages.Count > 0)
        {
            return PackageManifestParseResult.Success(packages.ToImmutableArray());
        }

        return PackageManifestParseResult.Failure(errors.ToImmutableArray());
    }
}
