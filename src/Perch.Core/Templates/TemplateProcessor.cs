using System.Text.RegularExpressions;

namespace Perch.Core.Templates;

public sealed partial class TemplateProcessor : ITemplateProcessor
{
    [GeneratedRegex(@"\{\{(op://[^}]+)\}\}")]
    private static partial Regex PlaceholderPattern();

    public bool ContainsPlaceholders(string content) =>
        PlaceholderPattern().IsMatch(content);

    public IReadOnlyList<string> FindReferences(string content) =>
        PlaceholderPattern().Matches(content)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

    public string ReplacePlaceholders(string content, IReadOnlyDictionary<string, string> resolvedValues) =>
        PlaceholderPattern().Replace(content, match =>
        {
            string reference = match.Groups[1].Value;
            return resolvedValues.TryGetValue(reference, out string? value) ? value : match.Value;
        });
}
