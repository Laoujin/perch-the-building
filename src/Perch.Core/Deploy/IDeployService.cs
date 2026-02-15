namespace Perch.Core.Deploy;

public interface IDeployService
{
    Task<int> DeployAsync(string configRepoPath, bool dryRun = false, IProgress<DeployResult>? progress = null, IDeployConfirmation? confirmation = null, CancellationToken cancellationToken = default);
}
