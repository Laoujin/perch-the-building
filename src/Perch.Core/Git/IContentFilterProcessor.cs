using System.Collections.Immutable;

namespace Perch.Core.Git;

public interface IContentFilterProcessor
{
    string Apply(string content, ImmutableArray<FilterRule> rules);
}
