using System.Text.RegularExpressions;

namespace Perch.Core.Modules;

public static partial class EnvironmentExpander
{
    public static string Expand(string input, IReadOnlyDictionary<string, string>? variables = null)
    {
        string result = WindowsVarPattern().Replace(input, match =>
        {
            string varName = match.Groups[1].Value;
            if (variables != null && variables.TryGetValue(varName, out string? customValue))
            {
                return customValue;
            }

            string? value = Environment.GetEnvironmentVariable(varName);
            return value ?? match.Value;
        });

        result = UnixVarPattern().Replace(result, match =>
        {
            string varName = match.Groups[1].Value;
            if (variables != null && variables.TryGetValue(varName, out string? customValue))
            {
                return customValue;
            }

            string? value = Environment.GetEnvironmentVariable(varName);
            return value ?? match.Value;
        });

        return result;
    }

    [GeneratedRegex(@"%([^%]+)%")]
    private static partial Regex WindowsVarPattern();

    [GeneratedRegex(@"\$([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex UnixVarPattern();
}
