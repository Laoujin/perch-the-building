using System.Collections.Immutable;

namespace Perch.Core.Diff;

public sealed record DiffResult(ImmutableArray<DiffChange> Changes, string RootPath);
