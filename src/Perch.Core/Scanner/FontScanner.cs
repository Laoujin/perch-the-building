using System.Collections.Immutable;

namespace Perch.Core.Scanner;

public sealed class FontScanner : IFontScanner
{
    private static readonly string[] FontExtensions = [".ttf", ".otf", ".ttc", ".woff", ".woff2"];

    public Task<ImmutableArray<DetectedFont>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var registryNames = BuildRegistryFontNameMap();
        var results = new List<DetectedFont>();

        foreach (string fontDir in GetFontDirectories())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(fontDir))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(fontDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string ext = Path.GetExtension(file);
                if (FontExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
                {
                    var fileName = Path.GetFileName(file);
                    registryNames.TryGetValue(fileName, out var displayName);
                    string name = displayName ?? Path.GetFileNameWithoutExtension(file);
                    results.Add(new DetectedFont(name, null, file));
                }
            }
        }

        return Task.FromResult(results.ToImmutableArray());
    }

    private static Dictionary<string, string> BuildRegistryFontNameMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows())
            return map;

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
            if (key is null)
                return map;

            foreach (var valueName in key.GetValueNames())
            {
                if (key.GetValue(valueName) is not string fileName)
                    continue;

                // Strip type suffix like " (TrueType)" or " (OpenType)"
                var displayName = valueName;
                var parenIndex = displayName.LastIndexOf(" (", StringComparison.Ordinal);
                if (parenIndex > 0)
                    displayName = displayName[..parenIndex];

                // Registry value can be just a filename or a full path
                var fileKey = Path.GetFileName(fileName);
                map.TryAdd(fileKey, displayName);
            }
        }
        catch
        {
            // Registry access failure is non-fatal
        }

        return map;
    }

    private static IEnumerable<string> GetFontDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
            string localFonts = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
            yield return localFonts;
        }
        else if (OperatingSystem.IsLinux())
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, ".local", "share", "fonts");
            yield return "/usr/share/fonts";
            yield return "/usr/local/share/fonts";
        }
        else if (OperatingSystem.IsMacOS())
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, "Library", "Fonts");
            yield return "/Library/Fonts";
            yield return "/System/Library/Fonts";
        }
    }
}
