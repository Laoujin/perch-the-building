namespace Perch.Core.Status;

public sealed record StatusResult(string ModuleName, string SourcePath, string TargetPath, DriftLevel Level, string Message);
