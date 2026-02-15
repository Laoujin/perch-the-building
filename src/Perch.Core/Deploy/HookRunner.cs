using Perch.Core.Packages;

namespace Perch.Core.Deploy;

public sealed class HookRunner : IHookRunner
{
    private readonly IProcessRunner _processRunner;

    public HookRunner(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<DeployResult> RunAsync(string moduleName, string scriptPath, string workingDirectory, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(scriptPath))
        {
            return new DeployResult(moduleName, scriptPath, "", ResultLevel.Error, $"Hook script not found: {scriptPath}");
        }

        string fileName;
        string arguments;

        if (scriptPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "pwsh";
            arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
        }
        else if (scriptPath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "bash";
            arguments = $"\"{scriptPath}\"";
        }
        else
        {
            fileName = scriptPath;
            arguments = "";
        }

        ProcessRunResult result = await _processRunner.RunAsync(fileName, arguments, workingDirectory, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            string errorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            return new DeployResult(moduleName, scriptPath, "", ResultLevel.Error, $"Hook failed (exit {result.ExitCode}): {errorDetail.Trim()}");
        }

        return new DeployResult(moduleName, scriptPath, "", ResultLevel.Ok, "Hook completed");
    }
}
