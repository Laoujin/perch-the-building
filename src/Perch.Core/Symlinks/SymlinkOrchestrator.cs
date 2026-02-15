using Perch.Core.Backup;
using Perch.Core.Deploy;
using Perch.Core.Modules;

namespace Perch.Core.Symlinks;

public sealed class SymlinkOrchestrator
{
    private readonly ISymlinkProvider _symlinkProvider;
    private readonly IFileBackupProvider _backupProvider;
    private readonly IFileLockDetector _fileLockDetector;

    public SymlinkOrchestrator(ISymlinkProvider symlinkProvider, IFileBackupProvider backupProvider, IFileLockDetector fileLockDetector)
    {
        _symlinkProvider = symlinkProvider;
        _backupProvider = backupProvider;
        _fileLockDetector = fileLockDetector;
    }

    public DeployResult ProcessLink(string moduleName, string sourcePath, string targetPath, LinkType linkType, bool dryRun = false)
    {
        try
        {
            string? targetDir = Path.GetDirectoryName(targetPath);
            if (targetDir != null && !Directory.Exists(targetDir))
            {
                return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Error,
                    $"Parent directory does not exist: {targetDir}");
            }

            if (_symlinkProvider.IsSymlink(targetPath))
            {
                string? existingTarget = _symlinkProvider.GetSymlinkTarget(targetPath);
                if (string.Equals(existingTarget, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Ok,
                        "Already linked (skipped)");
                }
            }

            if (_fileLockDetector.IsLocked(targetPath))
            {
                return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Error,
                    "Target file is locked by another process");
            }

            if (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                if (dryRun)
                {
                    return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Warning,
                        "Would back up existing and create link");
                }

                string backupPath = _backupProvider.BackupFile(targetPath);
                CreateLink(targetPath, sourcePath, linkType);
                return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Warning,
                    $"Backed up existing to {backupPath}, created link");
            }

            if (dryRun)
            {
                return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Ok,
                    "Would create link");
            }

            CreateLink(targetPath, sourcePath, linkType);
            return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Ok, "Created link");
        }
        catch (Exception ex) when (IsSymlinkPrivilegeError(ex))
        {
            string message = OperatingSystem.IsWindows()
                ? "Symlink creation requires Developer Mode or administrator privileges. Enable Developer Mode at Settings > System > For developers."
                : "Insufficient permissions to create symlink. Check directory permissions or run with elevated privileges.";
            return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Error, message);
        }
        catch (Exception ex)
        {
            return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Error, ex.Message);
        }
    }

    private static bool IsSymlinkPrivilegeError(Exception ex) =>
        ex is UnauthorizedAccessException ||
        (ex is IOException && ex.Message.Contains("privilege", StringComparison.OrdinalIgnoreCase));

    private void CreateLink(string linkPath, string targetPath, LinkType linkType)
    {
        if (linkType == LinkType.Junction)
        {
            _symlinkProvider.CreateJunction(linkPath, targetPath);
        }
        else
        {
            _symlinkProvider.CreateSymlink(linkPath, targetPath);
        }
    }
}
