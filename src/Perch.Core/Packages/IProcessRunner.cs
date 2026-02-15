namespace Perch.Core.Packages;

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken cancellationToken = default);
}

public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);
