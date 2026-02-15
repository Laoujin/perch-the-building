using System.Collections.Immutable;
using System.ComponentModel;

namespace Perch.Core.Packages;

public sealed class NpmPackageManagerProvider : IPackageManagerProvider
{
    private readonly IProcessRunner _processRunner;

    public NpmPackageManagerProvider(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public PackageManager Manager => PackageManager.Npm;

    public async Task<PackageManagerScanResult> ScanInstalledAsync(CancellationToken cancellationToken = default)
    {
        ProcessRunResult result;
        try
        {
            result = await _processRunner.RunAsync("npm", "list -g --depth=0", cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return PackageManagerScanResult.Unavailable("npm is not installed.");
        }

        if (result.ExitCode != 0)
        {
            return PackageManagerScanResult.Unavailable($"npm list failed: {result.StandardError}");
        }

        var packages = ParseOutput(result.StandardOutput);
        return new PackageManagerScanResult(true, packages, null);
    }

    internal static ImmutableArray<InstalledPackage> ParseOutput(string output)
    {
        var packages = new List<InstalledPackage>();

        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Skip header line (e.g. "/usr/lib" or "C:\Users\...")
            int dashIndex = trimmed.IndexOf("-- ", StringComparison.Ordinal);
            if (dashIndex < 0)
                continue;

            string packagePart = trimmed[(dashIndex + 3)..];
            int atIndex = packagePart.LastIndexOf('@');
            if (atIndex > 0)
            {
                packages.Add(new InstalledPackage(packagePart[..atIndex], PackageManager.Npm));
            }
        }

        return packages.ToImmutableArray();
    }
}
