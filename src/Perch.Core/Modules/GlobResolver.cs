namespace Perch.Core.Modules;

public sealed class GlobResolver : IGlobResolver
{
    public IReadOnlyList<string> Resolve(string path)
    {
        if (!ContainsWildcard(path))
        {
            return [path];
        }

        string[] segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = new List<string> { "" };

        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];
            var next = new List<string>();

            if (!ContainsWildcard(segment))
            {
                foreach (string dir in current)
                {
                    next.Add(Path.Combine(dir, segment));
                }
            }
            else
            {
                bool isLast = i == segments.Length - 1;
                foreach (string dir in current)
                {
                    if (!Directory.Exists(dir))
                    {
                        continue;
                    }

                    if (isLast)
                    {
                        next.AddRange(Directory.GetDirectories(dir, segment));
                        next.AddRange(Directory.GetFiles(dir, segment));
                    }
                    else
                    {
                        next.AddRange(Directory.GetDirectories(dir, segment));
                    }
                }
            }

            current = next;
        }

        return current;
    }

    private static bool ContainsWildcard(string value) =>
        value.Contains('*') || value.Contains('?');
}
