namespace Perch.Core.Templates;

public readonly record struct ReferenceResolveResult(string? Value, string? Error);

public interface IReferenceResolver
{
    Task<ReferenceResolveResult> ResolveAsync(string reference, CancellationToken cancellationToken = default);
}
