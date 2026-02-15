using System.Collections.Immutable;

namespace Perch.Core.Diff;

public sealed record DiffSnapshot(string RootPath, DateTime CapturedAt, ImmutableArray<FileSnapshot> Files);
