namespace Perch.Core.Status;

public interface IStatusService
{
    Task<int> CheckAsync(string configRepoPath, IProgress<StatusResult>? progress = null, CancellationToken cancellationToken = default);
}
