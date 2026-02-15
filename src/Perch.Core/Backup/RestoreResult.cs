namespace Perch.Core.Backup;

public sealed record RestoreResult(string FileName, string OriginalPath, RestoreOutcome Outcome, string? Message);

public enum RestoreOutcome
{
    Restored,
    Skipped,
    Error,
}
