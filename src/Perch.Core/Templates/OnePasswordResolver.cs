using Perch.Core.Packages;

namespace Perch.Core.Templates;

public sealed class OnePasswordResolver : IReferenceResolver
{
    private readonly IProcessRunner _processRunner;

    public OnePasswordResolver(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<ReferenceResolveResult> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        ProcessRunResult result = await _processRunner.RunAsync("op", $"read \"{reference}\"", null, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            string error = !string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardError.Trim()
                : result.StandardOutput.Trim();
            return new ReferenceResolveResult(null, $"op read failed (exit {result.ExitCode}): {error}");
        }

        return new ReferenceResolveResult(result.StandardOutput.Trim(), null);
    }
}
