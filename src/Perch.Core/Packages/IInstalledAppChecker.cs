namespace Perch.Core.Packages;

public interface IInstalledAppChecker
{
    Task<IReadOnlySet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);
}
