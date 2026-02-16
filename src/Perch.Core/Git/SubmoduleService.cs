using Perch.Core.Packages;

namespace Perch.Core.Git;

public sealed class SubmoduleService : ISubmoduleService
{
    private readonly IProcessRunner _processRunner;

    public SubmoduleService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<bool> InitializeIfNeededAsync(string repoPath, CancellationToken cancellationToken = default)
    {
        string gitmodulesPath = Path.Combine(repoPath, ".gitmodules");
        if (!File.Exists(gitmodulesPath))
        {
            return false;
        }

        var statusResult = await _processRunner.RunAsync("git", "submodule status", repoPath, cancellationToken).ConfigureAwait(false);
        if (statusResult.ExitCode != 0)
        {
            return false;
        }

        bool hasUninitialized = statusResult.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.TrimStart().StartsWith('-'));

        if (!hasUninitialized)
        {
            return false;
        }

        var initResult = await _processRunner.RunAsync("git", "submodule update --init", repoPath, cancellationToken).ConfigureAwait(false);
        return initResult.ExitCode == 0;
    }
}
