using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Perch.Core.Diff;

public sealed class DiffSnapshotService : IDiffSnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task CaptureAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        var files = await CaptureFilesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        var snapshot = new DiffSnapshot(fullPath, DateTime.UtcNow, files);

        string snapshotPath = GetSnapshotPath();
        string? dir = Path.GetDirectoryName(snapshotPath);
        if (dir != null)
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(snapshotPath, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DiffResult> CompareAsync(CancellationToken cancellationToken = default)
    {
        string snapshotPath = GetSnapshotPath();
        if (!File.Exists(snapshotPath))
        {
            throw new InvalidOperationException("No active snapshot. Run 'perch diff start' first.");
        }

        string json = await File.ReadAllTextAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        DiffSnapshot before = JsonSerializer.Deserialize<DiffSnapshot>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize snapshot.");

        var afterFiles = await CaptureFilesAsync(before.RootPath, cancellationToken).ConfigureAwait(false);
        var changes = ComputeChanges(before.Files, afterFiles);

        File.Delete(snapshotPath);

        return new DiffResult(changes, before.RootPath);
    }

    public bool HasActiveSnapshot() => File.Exists(GetSnapshotPath());

    private static async Task<ImmutableArray<FileSnapshot>> CaptureFilesAsync(string rootPath, CancellationToken cancellationToken)
    {
        var files = new List<FileSnapshot>();

        foreach (string filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(filePath);
            string relativePath = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
            string hash = await ComputeHashAsync(filePath, cancellationToken).ConfigureAwait(false);
            files.Add(new FileSnapshot(relativePath, fileInfo.Length, hash));
        }

        files.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.Ordinal));
        return files.ToImmutableArray();
    }

    private static ImmutableArray<DiffChange> ComputeChanges(ImmutableArray<FileSnapshot> before, ImmutableArray<FileSnapshot> after)
    {
        var beforeMap = before.ToDictionary(f => f.RelativePath, StringComparer.Ordinal);
        var afterMap = after.ToDictionary(f => f.RelativePath, StringComparer.Ordinal);
        var changes = new List<DiffChange>();

        foreach (var file in after)
        {
            if (!beforeMap.TryGetValue(file.RelativePath, out var prev))
            {
                changes.Add(new DiffChange(file.RelativePath, DiffChangeType.Added));
            }
            else if (prev.Hash != file.Hash)
            {
                changes.Add(new DiffChange(file.RelativePath, DiffChangeType.Modified));
            }
        }

        foreach (var file in before)
        {
            if (!afterMap.ContainsKey(file.RelativePath))
            {
                changes.Add(new DiffChange(file.RelativePath, DiffChangeType.Deleted));
            }
        }

        changes.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.Ordinal));
        return changes.ToImmutableArray();
    }

    private static async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static string GetSnapshotPath()
    {
        if (OperatingSystem.IsWindows())
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "perch", "diff-snapshot.json");
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "perch", "diff-snapshot.json");
    }
}
