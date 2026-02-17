namespace Perch.Core;

public static class PathDisplay
{
    private const string Ellipsis = "...";

    public static string TruncateMiddle(string path, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, Ellipsis.Length + 2);

        if (path.Length <= maxLength)
            return path;

        var separator = path.Contains('\\') ? '\\' : '/';
        var segments = path.Split(separator);

        if (segments.Length <= 2)
            return TruncatePlain(path, maxLength);

        // Always keep first segment (root/drive) and last segment (filename)
        var head = segments[0];
        var tail = segments[^1];
        var budget = maxLength - head.Length - tail.Length - Ellipsis.Length - 2; // 2 separators

        if (budget <= 0)
            return $"{head}{separator}{Ellipsis}{separator}{tail}";

        // Add segments from the end (more useful context) until budget runs out
        var suffix = new List<string>();
        for (var i = segments.Length - 2; i >= 1; i--)
        {
            var cost = segments[i].Length + 1; // +1 for separator
            if (budget < cost)
                break;

            suffix.Insert(0, segments[i]);
            budget -= cost;
        }

        if (suffix.Count == segments.Length - 2)
            return path;

        var suffixPart = suffix.Count > 0
            ? separator + string.Join(separator, suffix)
            : string.Empty;

        return $"{head}{separator}{Ellipsis}{suffixPart}{separator}{tail}";
    }

    private static string TruncatePlain(string path, int maxLength)
    {
        var half = (maxLength - Ellipsis.Length) / 2;
        return string.Concat(path.AsSpan(0, half), Ellipsis, path.AsSpan(path.Length - half));
    }
}
