namespace Perch.Core.Backup;

public interface ISnapshotProvider
{
    string? CreateSnapshot(IReadOnlyList<string> targetPaths, CancellationToken cancellationToken = default);
    IReadOnlyList<SnapshotInfo> ListSnapshots();
    IReadOnlyList<RestoreResult> RestoreSnapshot(string snapshotId, string? fileFilter = null, CancellationToken cancellationToken = default);
}
