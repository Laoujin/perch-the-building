using System.Collections.Immutable;
using System.ComponentModel;

namespace Perch.Core.Packages;

public sealed class VsCodeExtensionProvider : IPackageManagerProvider
{
    private readonly IProcessRunner _processRunner;

    public VsCodeExtensionProvider(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public PackageManager Manager => PackageManager.VsCode;

    public async Task<PackageManagerScanResult> ScanInstalledAsync(CancellationToken cancellationToken = default)
    {
        ProcessRunResult result;
        try
        {
            result = await _processRunner.RunAsync("code", "--list-extensions", cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return PackageManagerScanResult.Unavailable("code is not installed.");
        }

        if (result.ExitCode != 0)
        {
            return PackageManagerScanResult.Unavailable($"code --list-extensions failed: {result.StandardError}");
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
                packages.Add(new InstalledPackage(trimmed, PackageManager.VsCode));
            }
        }

        return packages.ToImmutableArray();
    }
}
