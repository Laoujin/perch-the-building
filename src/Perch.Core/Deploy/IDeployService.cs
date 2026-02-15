namespace Perch.Core.Deploy;

public interface IDeployService
{
    Task<int> DeployAsync(string configRepoPath, bool dryRun = false, IProgress<DeployResult>? progress = null, CancellationToken cancellationToken = default);
}
