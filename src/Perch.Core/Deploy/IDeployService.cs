namespace Perch.Core.Deploy;

public interface IDeployService
{
    Task<int> DeployAsync(string configRepoPath, DeployOptions? options = null, CancellationToken cancellationToken = default);
}
