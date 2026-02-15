using System.Collections.Immutable;
using System.ComponentModel;

namespace Perch.Core.Packages;

public sealed class AptPackageManagerProvider : IPackageManagerProvider
{
    private readonly IProcessRunner _processRunner;

    public AptPackageManagerProvider(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public PackageManager Manager => PackageManager.Apt;

    public async Task<PackageManagerScanResult> ScanInstalledAsync(CancellationToken cancellationToken = default)
    {
        ProcessRunResult result;
        try
        {
            result = await _processRunner.RunAsync("apt", "list --installed", cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return PackageManagerScanResult.Unavailable("apt is not installed.");
        }

        if (result.ExitCode != 0)
        {
            return PackageManagerScanResult.Unavailable($"apt list failed: {result.StandardError}");
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

            // Skip "Listing..." header
            if (trimmed.StartsWith("Listing", StringComparison.OrdinalIgnoreCase))
                continue;

            // Format: "name/suite,now version arch [installed]"
            int slashIndex = trimmed.IndexOf('/');
            if (slashIndex > 0)
            {
                packages.Add(new InstalledPackage(trimmed[..slashIndex], PackageManager.Apt));
            }
        }

        return packages.ToImmutableArray();
    }
}
