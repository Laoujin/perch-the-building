using Perch.Core.Modules;
using Perch.Core.Packages;

namespace Perch.Core.Deploy;

public sealed class GlobalPackageInstaller : IGlobalPackageInstaller
{
    private readonly IProcessRunner _processRunner;

    public GlobalPackageInstaller(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<DeployResult> InstallAsync(string moduleName, GlobalPackageManager manager, string packageName, bool dryRun, CancellationToken cancellationToken = default)
    {
        bool isInstalled = await IsInstalledAsync(manager, packageName, cancellationToken).ConfigureAwait(false);
        if (isInstalled)
        {
            return new DeployResult(moduleName, "", packageName, ResultLevel.Ok, $"{packageName} already installed");
        }

        string command = manager switch
        {
            GlobalPackageManager.Bun => "bun",
            _ => "npm",
        };

        string arguments = manager switch
        {
            GlobalPackageManager.Bun => $"add -g {packageName}",
            _ => $"install -g {packageName}",
        };

        if (dryRun)
        {
            return new DeployResult(moduleName, "", packageName, ResultLevel.Ok, $"Would run: {command} {arguments}");
        }

        ProcessRunResult result = await _processRunner.RunAsync(command, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            string errorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            return new DeployResult(moduleName, "", packageName, ResultLevel.Error, $"{command} {arguments} failed (exit {result.ExitCode}): {errorDetail.Trim()}");
        }

        return new DeployResult(moduleName, "", packageName, ResultLevel.Ok, $"Installed {packageName} via {command}");
    }

    private async Task<bool> IsInstalledAsync(GlobalPackageManager manager, string packageName, CancellationToken cancellationToken)
    {
        try
        {
            string command = manager switch
            {
                GlobalPackageManager.Bun => "bun",
                _ => "npm",
            };

            string arguments = manager switch
            {
                GlobalPackageManager.Bun => "pm ls -g",
                _ => $"list -g {packageName} --depth=0",
            };

            ProcessRunResult result = await _processRunner.RunAsync(command, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                return false;
            }

            if (manager == GlobalPackageManager.Bun)
            {
                string output = result.StandardOutput ?? "";
                return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Any(line => line.Trim().Equals(packageName, StringComparison.OrdinalIgnoreCase) ||
                                 line.Trim().StartsWith($"{packageName}@", StringComparison.OrdinalIgnoreCase));
            }

            // npm list returns exit 0 if found
            return true;
        }
        catch
        {
            // If we can't determine if installed, assume not installed
            return false;
        }
    }
}
