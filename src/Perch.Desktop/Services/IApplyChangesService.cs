namespace Perch.Desktop.Services;

public record ApplyChangesResult(int Applied, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}

public interface IApplyChangesService
{
    bool IsApplying { get; }
    Task<ApplyChangesResult> ApplyAsync(CancellationToken cancellationToken = default);
}
