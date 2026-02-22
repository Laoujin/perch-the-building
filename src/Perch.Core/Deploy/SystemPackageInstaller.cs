using System.Text.RegularExpressions;
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

    public async Task<DeployResult> InstallAsync(string packageName, PackageManager manager, bool dryRun, CancellationToken cancellationToken = default)
    {
        (string command, string arguments) = GetCommand(manager, packageName);

        if (command.Length == 0)
        {
            return new DeployResult("system-packages", "", packageName, ResultLevel.Ok, $"Skipped {packageName} ({manager} handled elsewhere)");
        }

        _installedPackages ??= await _installedAppChecker.GetInstalledPackageIdsAsync(cancellationToken).ConfigureAwait(false);

        if (IsInstalled(packageName, _installedPackages))
        {
            return new DeployResult("system-packages", "", packageName, ResultLevel.Synced, $"Already installed: {packageName}");
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

    private static bool IsInstalled(string packageId, IReadOnlySet<string> installedPackages)
    {
        if (installedPackages.Contains(packageId))
            return true;

        // Fallback: check if any installed package matches the app name with optional version
        // e.g., "Git" matches "Git" or "Git 2.53.0" (for apps installed outside winget)
        string[] parts = packageId.Split('.');
        foreach (string part in parts)
        {
            if (part.Length >= 3 && installedPackages.Any(pkg =>
                Regex.IsMatch(pkg, $@"^{Regex.Escape(part)}( [0-9.]+)?$", RegexOptions.IgnoreCase)))
                return true;
        }

        return false;
    }
}
