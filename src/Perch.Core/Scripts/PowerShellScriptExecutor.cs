using System.Diagnostics;

namespace Perch.Core.Scripts;

public sealed class PowerShellScriptExecutor : IScriptExecutor
{
    public async Task<ScriptExecutionResult> ExecuteAsync(string script, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = "-NoProfile -NonInteractive -Command -",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = { ["TERM"] = "dumb", ["NO_COLOR"] = "1" },
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        await process.StandardInput.WriteAsync(script);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new ScriptExecutionResult(
            process.ExitCode == 0,
            output.TrimEnd(),
            string.IsNullOrWhiteSpace(error) ? null : error.TrimEnd());
    }
}
