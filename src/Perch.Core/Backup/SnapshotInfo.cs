using System.Collections.Immutable;

namespace Perch.Core.Backup;

public sealed record SnapshotInfo(string Id, string Path, DateTime Timestamp, ImmutableArray<SnapshotFileInfo> Files);

public sealed record SnapshotFileInfo(string FileName, string OriginalPath);
