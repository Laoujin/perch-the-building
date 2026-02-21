namespace Perch.Core.Packages;

public sealed class InstalledAppChecker : IInstalledAppChecker
{
    private readonly IEnumerable<IPackageManagerProvider> _providers;

    public InstalledAppChecker(IEnumerable<IPackageManagerProvider> providers)
    {
        _providers = providers;
    }

    public async Task<IReadOnlySet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default)
    {
        var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            var result = await provider.ScanInstalledAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsAvailable)
            {
                foreach (var package in result.Packages)
                {
                    installedIds.Add(package.Name);
                }
            }
        }

        return installedIds;
    }
}
