namespace Perch.Core.Backup;

public interface ISnapshotProvider
{
    string? CreateSnapshot(IReadOnlyList<string> targetPaths, CancellationToken cancellationToken = default);
}
