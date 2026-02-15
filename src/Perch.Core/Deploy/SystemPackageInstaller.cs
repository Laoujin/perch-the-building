using Perch.Core.Packages;

namespace Perch.Core.Deploy;

public sealed class SystemPackageInstaller : ISystemPackageInstaller
{
    private readonly IProcessRunner _processRunner;

    public SystemPackageInstaller(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<DeployResult> InstallAsync(string packageName, PackageManager manager, bool dryRun, CancellationToken cancellationToken = default)
    {
        (string command, string arguments) = GetCommand(manager, packageName);

        if (command.Length == 0)
        {
            return new DeployResult("system-packages", "", packageName, ResultLevel.Ok, $"Skipped {packageName} ({manager} handled elsewhere)");
        }

        if (dryRun)
        {
            return new DeployResult("system-packages", "", packageName, ResultLevel.Ok, $"Would install: {command} {arguments}");
        }

        ProcessRunResult result = await _processRunner.RunAsync(command, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            string errorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            return new DeployResult("system-packages", "", packageName, ResultLevel.Error, $"{command} {arguments} failed (exit {result.ExitCode}): {errorDetail.Trim()}");
        }

        return new DeployResult("system-packages", "", packageName, ResultLevel.Ok, $"Installed {packageName} via {manager.ToString().ToLowerInvariant()}");
    }

    private static (string Command, string Arguments) GetCommand(PackageManager manager, string packageName) =>
        manager switch
        {
            PackageManager.Chocolatey => ("choco", $"install {packageName} -y"),
            PackageManager.Winget => ("winget", $"install --id {packageName} --accept-source-agreements --accept-package-agreements"),
            PackageManager.Apt => ("sudo", $"apt-get install -y {packageName}"),
            PackageManager.Brew => ("brew", $"install {packageName}"),
            _ => ("", ""),
        };
}
