namespace Perch.Core.Diff;

public interface IDiffSnapshotService
{
    Task CaptureAsync(string rootPath, CancellationToken cancellationToken = default);
    Task<DiffResult> CompareAsync(CancellationToken cancellationToken = default);
    bool HasActiveSnapshot();
}
