using Perch.Core.Packages;

namespace Perch.Core.Deploy;

public sealed class SystemPackageInstaller : ISystemPackageInstaller
{
    private readonly IProcessRunner _processRunner;
    private readonly IInstalledAppChecker _installedAppChecker;
    private IReadOnlySet<string>? _installedPackages;

    public SystemPackageInstaller(IProcessRunner processRunner, IInstalledAppChecker installedAppChecker)
    {
        _processRunner = processRunner;
        _installedAppChecker = installedAppChecker;
    }

    public async Task<DeployResult> InstallAsync(PackageDefinition package, bool dryRun, CancellationToken cancellationToken = default)
    {
        (string command, string arguments) = GetCommand(package.Manager, package.Name);

        if (command.Length == 0)
        {
            return new DeployResult("system-packages", "", package.Name, ResultLevel.Ok, $"Skipped {package.Name} ({package.Manager} handled elsewhere)");
        }

        _installedPackages ??= await _installedAppChecker.GetInstalledPackageIdsAsync(cancellationToken).ConfigureAwait(false);

        // Check all alternative IDs (winget + choco) for exact match
        foreach (string id in package.AlternativeIds)
        {
            if (_installedPackages.Contains(id))
            {
                return new DeployResult("system-packages", "", package.Name, ResultLevel.Synced, $"Already installed: {id}");
            }
        }

        if (dryRun)
        {
            return new DeployResult("system-packages", "", package.Name, ResultLevel.Ok, $"Would install: {command} {arguments}");
        }

        ProcessRunResult result = await _processRunner.RunAsync(command, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            string errorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            return new DeployResult("system-packages", "", package.Name, ResultLevel.Error, $"{command} {arguments} failed (exit {result.ExitCode}): {errorDetail.Trim()}");
        }

        return new DeployResult("system-packages", "", package.Name, ResultLevel.Ok, $"Installed {package.Name} via {package.Manager.ToString().ToLowerInvariant()}");
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
