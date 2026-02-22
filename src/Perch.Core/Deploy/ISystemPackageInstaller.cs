using Perch.Core.Packages;

namespace Perch.Core.Deploy;

public interface ISystemPackageInstaller
{
    Task<DeployResult> InstallAsync(PackageDefinition package, bool dryRun, CancellationToken cancellationToken = default);
}
