using System.Collections.Immutable;

namespace Perch.Core.Modules;

public sealed record CleanFilterDefinition(string Name, string Script, ImmutableArray<string> Files);
