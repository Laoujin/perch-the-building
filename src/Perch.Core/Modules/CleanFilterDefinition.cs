using System.Collections.Immutable;
using Perch.Core.Git;

namespace Perch.Core.Modules;

public sealed record CleanFilterDefinition(string Name, string? Script, ImmutableArray<string> Files, ImmutableArray<FilterRule> Rules = default);
