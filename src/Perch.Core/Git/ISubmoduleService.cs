namespace Perch.Core.Git;

public interface ISubmoduleService
{
    Task<bool> InitializeIfNeededAsync(string repoPath, CancellationToken cancellationToken = default);
}
