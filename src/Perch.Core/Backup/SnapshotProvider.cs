using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Perch.Core.Backup;

public sealed class SnapshotProvider : ISnapshotProvider
{
    private const string ManifestFileName = "snapshot-manifest.json";
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    private readonly string _backupRoot;
    private readonly IFileBackupProvider _fileBackupProvider;

    public SnapshotProvider()
        : this(DefaultBackupRoot(), new FileBackupProvider())
    {
    }

    internal SnapshotProvider(string backupRoot)
        : this(backupRoot, new FileBackupProvider())
    {
    }

    internal SnapshotProvider(string backupRoot, IFileBackupProvider fileBackupProvider)
    {
        _backupRoot = backupRoot;
        _fileBackupProvider = fileBackupProvider;
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

        var manifestEntries = new List<ManifestEntry>();

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

            manifestEntries.Add(new ManifestEntry(relativeName, path));
        }

        string manifestPath = Path.Combine(snapshotDir, ManifestFileName);
        string json = JsonSerializer.Serialize(manifestEntries, s_jsonOptions);
        File.WriteAllText(manifestPath, json);

        return snapshotDir;
    }

    public IReadOnlyList<SnapshotInfo> ListSnapshots()
    {
        if (!Directory.Exists(_backupRoot))
        {
            return Array.Empty<SnapshotInfo>();
        }

        var snapshots = new List<SnapshotInfo>();

        foreach (string dir in Directory.GetDirectories(_backupRoot))
        {
            string dirName = Path.GetFileName(dir);
            if (!DateTime.TryParseExact(dirName, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
            {
                continue;
            }

            var files = ReadManifest(dir);
            snapshots.Add(new SnapshotInfo(dirName, dir, timestamp, files));
        }

        snapshots.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        return snapshots;
    }

    public IReadOnlyList<RestoreResult> RestoreSnapshot(string snapshotId, string? fileFilter = null, CancellationToken cancellationToken = default)
    {
        string snapshotDir = Path.Combine(_backupRoot, snapshotId);
        if (!Directory.Exists(snapshotDir))
        {
            return new[] { new RestoreResult(snapshotId, "", RestoreOutcome.Error, $"Snapshot '{snapshotId}' not found.") };
        }

        string manifestPath = Path.Combine(snapshotDir, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return new[] { new RestoreResult(snapshotId, "", RestoreOutcome.Error, "No manifest found — legacy snapshot cannot be auto-restored.") };
        }

        string json = File.ReadAllText(manifestPath);
        var entries = JsonSerializer.Deserialize<List<ManifestEntry>>(json) ?? [];

        if (fileFilter != null)
        {
            entries = entries.Where(e => string.Equals(e.FileName, fileFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var results = new List<RestoreResult>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sourcePath = Path.Combine(snapshotDir, entry.FileName);
            if (!File.Exists(sourcePath))
            {
                results.Add(new RestoreResult(entry.FileName, entry.OriginalPath, RestoreOutcome.Error, "File missing from snapshot."));
                continue;
            }

            try
            {
                if (File.Exists(entry.OriginalPath))
                {
                    _fileBackupProvider.BackupFile(entry.OriginalPath);
                }

                string? targetDir = Path.GetDirectoryName(entry.OriginalPath);
                if (targetDir != null)
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.Copy(sourcePath, entry.OriginalPath, overwrite: true);
                results.Add(new RestoreResult(entry.FileName, entry.OriginalPath, RestoreOutcome.Restored, null));
            }
            catch (Exception ex)
            {
                results.Add(new RestoreResult(entry.FileName, entry.OriginalPath, RestoreOutcome.Error, ex.Message));
            }
        }

        return results;
    }

    private static ImmutableArray<SnapshotFileInfo> ReadManifest(string snapshotDir)
    {
        string manifestPath = Path.Combine(snapshotDir, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return ImmutableArray<SnapshotFileInfo>.Empty;
        }

        string json = File.ReadAllText(manifestPath);
        var entries = JsonSerializer.Deserialize<List<ManifestEntry>>(json) ?? [];
        return entries.Select(e => new SnapshotFileInfo(e.FileName, e.OriginalPath)).ToImmutableArray();
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

    private sealed record ManifestEntry(string FileName, string OriginalPath);
}
