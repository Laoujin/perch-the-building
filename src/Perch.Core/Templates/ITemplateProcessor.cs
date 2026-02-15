namespace Perch.Core.Templates;

public interface ITemplateProcessor
{
    bool ContainsPlaceholders(string content);
    IReadOnlyList<string> FindReferences(string content);
    string ReplacePlaceholders(string content, IReadOnlyDictionary<string, string> resolvedValues);
}
