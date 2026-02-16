using System.Text.RegularExpressions;

namespace Perch.Core.Templates;

public sealed partial class TemplateProcessor : ITemplateProcessor
{
    [GeneratedRegex(@"\{\{(op://[^}]+)\}\}")]
    private static partial Regex OpReferencePattern();

    [GeneratedRegex(@"\{\{([^}]+)\}\}")]
    private static partial Regex AllPlaceholderPattern();

    public bool ContainsPlaceholders(string content) =>
        AllPlaceholderPattern().IsMatch(content);

    public IReadOnlyList<string> FindReferences(string content) =>
        OpReferencePattern().Matches(content)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

    public IReadOnlyList<string> FindVariables(string content) =>
        AllPlaceholderPattern().Matches(content)
            .Select(m => m.Groups[1].Value)
            .Where(v => !v.StartsWith("op://"))
            .Distinct()
            .ToList();

    public string ReplacePlaceholders(string content, IReadOnlyDictionary<string, string> resolvedValues) =>
        AllPlaceholderPattern().Replace(content, match =>
        {
            string reference = match.Groups[1].Value;
            return resolvedValues.TryGetValue(reference, out string? value) ? value : match.Value;
        });
}
