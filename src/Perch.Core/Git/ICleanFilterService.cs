using System.Collections.Immutable;
using Perch.Core.Modules;

namespace Perch.Core.Git;

public interface ICleanFilterService
{
    Task<ImmutableArray<CleanFilterResult>> SetupAsync(string configRepoPath, ImmutableArray<AppModule> modules, CancellationToken cancellationToken = default);
}
