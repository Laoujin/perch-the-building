namespace Perch.Core.Diff;

public sealed record DiffChange(string RelativePath, DiffChangeType Type);
