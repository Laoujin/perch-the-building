using System.Collections.Immutable;
using System.ComponentModel;

namespace Perch.Core.Packages;

public sealed class BrewPackageManagerProvider : IPackageManagerProvider
{
    private readonly IProcessRunner _processRunner;

    public BrewPackageManagerProvider(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public PackageManager Manager => PackageManager.Brew;

    public async Task<PackageManagerScanResult> ScanInstalledAsync(CancellationToken cancellationToken = default)
    {
        ProcessRunResult result;
        try
        {
            result = await _processRunner.RunAsync("brew", "list --formula", cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return PackageManagerScanResult.Unavailable("brew is not installed.");
        }

        if (result.ExitCode != 0)
        {
            return PackageManagerScanResult.Unavailable($"brew list failed: {result.StandardError}");
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

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                packages.Add(new InstalledPackage(trimmed, PackageManager.Brew));
            }
        }

        return packages.ToImmutableArray();
    }
}
