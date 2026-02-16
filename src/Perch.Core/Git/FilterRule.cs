using System.Collections.Immutable;

namespace Perch.Core.Git;

public sealed record FilterRule(string Type, ImmutableArray<string> Patterns);
