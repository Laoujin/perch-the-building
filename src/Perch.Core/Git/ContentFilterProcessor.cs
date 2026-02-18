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
                "strip-json-keys" => StripJsonKeys(result, rule.Patterns),
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

    private static string StripJsonKeys(string content, ImmutableArray<string> keys)
    {
        string result = content;
        foreach (string key in keys)
        {
            result = RemoveJsonKey(result, key);
        }

        return result;
    }

    private static string RemoveJsonKey(string content, string key)
    {
        string escapedKey = Regex.Escape(key);
        int startSearch = 0;

        while (startSearch < content.Length)
        {
            var match = Regex.Match(content[startSearch..], $@"^[ \t]*""{escapedKey}""\s*:", RegexOptions.Multiline);
            if (!match.Success)
            {
                break;
            }

            int absoluteStart = startSearch + match.Index;
            int lineStart = content.LastIndexOf('\n', Math.Max(absoluteStart - 1, 0)) + 1;
            int valueStart = absoluteStart + match.Length;

            int valueEnd = FindJsonValueEnd(content, valueStart);
            if (valueEnd < 0)
            {
                break;
            }

            int removeEnd = SkipCommaAndTrailingWhitespace(content, valueEnd);

            if (removeEnd < content.Length && content[removeEnd] is '\r' or '\n')
            {
                removeEnd = SkipNewline(content, removeEnd);
            }
            else if (lineStart > 0)
            {
                lineStart = AdjustForTrailingCommaBeforeClosingBrace(content, lineStart, removeEnd);
            }

            content = content[..lineStart] + content[removeEnd..];
            startSearch = lineStart;
        }

        return content;
    }

    private static int SkipCommaAndTrailingWhitespace(string content, int pos)
    {
        if (pos < content.Length && content[pos] == ',')
        {
            pos++;
        }

        while (pos < content.Length && content[pos] is ' ' or '\t')
        {
            pos++;
        }

        return pos;
    }

    private static int SkipNewline(string content, int pos)
    {
        if (content[pos] == '\r' && pos + 1 < content.Length && content[pos + 1] == '\n')
        {
            return pos + 2;
        }

        return pos + 1;
    }

    private static int AdjustForTrailingCommaBeforeClosingBrace(string content, int lineStart, int removeEnd)
    {
        int nextNonWhitespace = removeEnd;
        while (nextNonWhitespace < content.Length && content[nextNonWhitespace] is ' ' or '\t' or '\r' or '\n')
        {
            nextNonWhitespace++;
        }

        if (nextNonWhitespace >= content.Length || content[nextNonWhitespace] != '}')
        {
            return lineStart;
        }

        int trailingComma = lineStart - 1;
        while (trailingComma >= 0 && content[trailingComma] is '\r' or '\n')
        {
            trailingComma--;
        }

        return trailingComma >= 0 && content[trailingComma] == ',' ? trailingComma : lineStart;
    }

    private static int FindJsonValueEnd(string content, int start)
    {
        while (start < content.Length && content[start] is ' ' or '\t' or '\r' or '\n')
        {
            start++;
        }

        if (start >= content.Length)
        {
            return -1;
        }

        char ch = content[start];

        if (ch == '"')
        {
            int i = start + 1;
            while (i < content.Length)
            {
                if (content[i] == '\\')
                {
                    i += 2;
                    continue;
                }

                if (content[i] == '"')
                {
                    return i + 1;
                }

                i++;
            }

            return -1;
        }

        if (ch is '{' or '[')
        {
            char open = ch;
            char close = ch == '{' ? '}' : ']';
            int depth = 1;
            bool inString = false;
            int i = start + 1;

            while (i < content.Length && depth > 0)
            {
                char c = content[i];

                if (inString)
                {
                    if (c == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inString = true;
                    }
                    else if (c == open)
                    {
                        depth++;
                    }
                    else if (c == close)
                    {
                        depth--;
                    }
                }

                i++;
            }

            return depth == 0 ? i : -1;
        }

        {
            int i = start;
            while (i < content.Length && content[i] is not ',' and not '}' and not ']' and not '\r' and not '\n')
            {
                i++;
            }

            return i;
        }
    }
}
