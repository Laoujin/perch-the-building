using System.Collections.Immutable;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Packages;

namespace Perch.Core.Git;

public sealed class CleanFilterService : ICleanFilterService
{
    private readonly IProcessRunner _processRunner;

    public CleanFilterService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<ImmutableArray<CleanFilterResult>> SetupAsync(string configRepoPath, ImmutableArray<AppModule> modules, CancellationToken cancellationToken = default)
    {
        var results = new List<CleanFilterResult>();

        foreach (AppModule module in modules)
        {
            if (module.CleanFilter == null)
            {
                continue;
            }

            CleanFilterDefinition filter = module.CleanFilter;
            string scriptAbsolutePath = Path.GetFullPath(Path.Combine(module.ModulePath, filter.Script));

            if (!File.Exists(scriptAbsolutePath))
            {
                results.Add(new CleanFilterResult(module.Name, ResultLevel.Error, $"Clean filter script not found: {scriptAbsolutePath}"));
                continue;
            }

            string scriptRelativePath = Path.GetRelativePath(configRepoPath, scriptAbsolutePath).Replace('\\', '/');

            ProcessRunResult configResult = await _processRunner.RunAsync(
                "git", $"config --local filter.{filter.Name}.clean \"{scriptRelativePath}\"",
                configRepoPath, cancellationToken).ConfigureAwait(false);

            if (configResult.ExitCode != 0)
            {
                results.Add(new CleanFilterResult(module.Name, ResultLevel.Error, $"Failed to register git filter: {configResult.StandardError.Trim()}"));
                continue;
            }

            string gitattributesPath = Path.Combine(configRepoPath, ".gitattributes");
            string existingContent = File.Exists(gitattributesPath)
                ? await File.ReadAllTextAsync(gitattributesPath, cancellationToken).ConfigureAwait(false)
                : "";

            var linesToAdd = new List<string>();
            foreach (string file in filter.Files)
            {
                string entry = $"{module.Name}/{file} filter={filter.Name}";
                if (!existingContent.Contains(entry, StringComparison.Ordinal))
                {
                    linesToAdd.Add(entry);
                }
            }

            if (linesToAdd.Count > 0)
            {
                string separator = existingContent.Length > 0 && !existingContent.EndsWith('\n') ? Environment.NewLine : "";
                string newContent = separator + string.Join(Environment.NewLine, linesToAdd) + Environment.NewLine;
                await File.AppendAllTextAsync(gitattributesPath, newContent, cancellationToken).ConfigureAwait(false);
            }

            results.Add(new CleanFilterResult(module.Name, ResultLevel.Ok, $"Registered clean filter '{filter.Name}'"));
        }

        return results.ToImmutableArray();
    }
}
