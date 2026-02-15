namespace Perch.Core.Diff;

public sealed record FileSnapshot(string RelativePath, long Size, string Hash);
