using System.Collections.Immutable;

namespace Perch.Core.Catalog;

public sealed record CategoryDefinition(
    string Name,
    int Sort,
    string? Pattern,
    ImmutableDictionary<string, CategoryDefinition> Children);
