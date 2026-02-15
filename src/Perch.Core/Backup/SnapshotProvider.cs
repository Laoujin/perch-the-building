namespace Perch.Core.Backup;

public sealed class SnapshotProvider : ISnapshotProvider
{
    private readonly string _backupRoot;

    public SnapshotProvider()
        : this(DefaultBackupRoot())
    {
    }

    internal SnapshotProvider(string backupRoot)
    {
        _backupRoot = backupRoot;
    }

    public string? CreateSnapshot(IReadOnlyList<string> targetPaths, CancellationToken cancellationToken = default)
    {
        var existingPaths = new List<string>();
        foreach (string path in targetPaths)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                existingPaths.Add(path);
            }
        }

        if (existingPaths.Count == 0)
        {
            return null;
        }

        string snapshotDir = Path.Combine(_backupRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(snapshotDir);

        foreach (string path in existingPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativeName = Path.GetFileName(path);
            string destPath = Path.Combine(snapshotDir, relativeName);

            if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
            {
                CopyDirectory(path, destPath);
            }
            else
            {
                File.Copy(path, destPath, overwrite: true);
            }
        }

        return snapshotDir;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }

    private static string DefaultBackupRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "perch", "backups");
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "perch", "backups");
    }
}
