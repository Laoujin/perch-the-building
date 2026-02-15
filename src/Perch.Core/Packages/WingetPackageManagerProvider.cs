using System.Collections.Immutable;
using System.ComponentModel;

namespace Perch.Core.Packages;

public sealed class WingetPackageManagerProvider : IPackageManagerProvider
{
    private readonly IProcessRunner _processRunner;

    public WingetPackageManagerProvider(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public PackageManager Manager => PackageManager.Winget;

    public async Task<PackageManagerScanResult> ScanInstalledAsync(CancellationToken cancellationToken = default)
    {
        ProcessRunResult result;
        try
        {
            result = await _processRunner.RunAsync("winget", "list --source winget", cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return PackageManagerScanResult.Unavailable("winget is not installed.");
        }

        if (result.ExitCode != 0)
        {
            return PackageManagerScanResult.Unavailable($"winget list failed: {result.StandardError}");
        }

        var packages = ParseOutput(result.StandardOutput);
        return new PackageManagerScanResult(true, packages, null);
    }

    internal static ImmutableArray<InstalledPackage> ParseOutput(string output)
    {
        var packages = new List<InstalledPackage>();
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Find the header separator line (dashes)
        int separatorIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith('-') && trimmed.Contains("--"))
            {
                separatorIndex = i;
                break;
            }
        }

        if (separatorIndex < 0 || separatorIndex + 1 >= lines.Length)
        {
            return ImmutableArray<InstalledPackage>.Empty;
        }

        // Find the end of the first dash group = Name column width
        string separator = lines[separatorIndex];
        int nameColumnEnd = separator.IndexOf(' ');
        if (nameColumnEnd < 0)
        {
            nameColumnEnd = separator.Length;
        }

        for (int i = separatorIndex + 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string name = (line.Length > nameColumnEnd ? line[..nameColumnEnd] : line).Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                packages.Add(new InstalledPackage(name, PackageManager.Winget));
            }
        }

        return packages.ToImmutableArray();
    }
}
