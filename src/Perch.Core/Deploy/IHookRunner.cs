namespace Perch.Core.Deploy;

public interface IHookRunner
{
    Task<DeployResult> RunAsync(string moduleName, string scriptPath, string workingDirectory, CancellationToken cancellationToken = default);
}
