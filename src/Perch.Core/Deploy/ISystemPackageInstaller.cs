using Perch.Core.Packages;

namespace Perch.Core.Deploy;

public interface ISystemPackageInstaller
{
    Task<DeployResult> InstallAsync(string packageName, PackageManager manager, bool dryRun, CancellationToken cancellationToken = default);
}
