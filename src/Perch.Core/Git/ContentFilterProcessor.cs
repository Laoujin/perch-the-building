using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Perch.Core.Git;

public sealed partial class ContentFilterProcessor : IContentFilterProcessor
{
    public string Apply(string content, ImmutableArray<FilterRule> rules)
    {
        if (rules.IsDefaultOrEmpty)
        {
            return content;
        }

        string result = content;
        foreach (FilterRule rule in rules)
        {
            result = rule.Type switch
            {
                "strip-xml-elements" => StripXmlElements(result, rule.Patterns),
                "strip-ini-keys" => StripIniKeys(result, rule.Patterns),
                _ => result,
            };
        }

        return result;
    }

    private static string StripXmlElements(string content, ImmutableArray<string> elements)
    {
        string result = content;
        foreach (string element in elements)
        {
            string pattern = $@"[ \t]*<{Regex.Escape(element)}\b[^>]*/>\s*\n?|[ \t]*<{Regex.Escape(element)}\b[^>]*>[\s\S]*?</{Regex.Escape(element)}>\s*\n?";
            result = Regex.Replace(result, pattern, "");
        }

        return result;
    }

    private static string StripIniKeys(string content, ImmutableArray<string> keys)
    {
        string result = content;
        foreach (string key in keys)
        {
            string pattern = $@"^[ \t]*{Regex.Escape(key)}\s*=.*\r?\n?";
            result = Regex.Replace(result, pattern, "", RegexOptions.Multiline);
        }

        return result;
    }
}
