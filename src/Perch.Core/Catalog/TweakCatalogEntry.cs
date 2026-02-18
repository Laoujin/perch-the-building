using System.Collections.Immutable;

using Perch.Core.Modules;

namespace Perch.Core.Catalog;

public sealed record TweakCatalogEntry(
    string Id,
    string Name,
    string Category,
    ImmutableArray<string> Tags,
    string? Description,
    bool Reversible,
    ImmutableArray<string> Profiles,
    ImmutableArray<RegistryEntryDefinition> Registry,
    string? Script = null,
    string? UndoScript = null,
    ImmutableArray<string> Suggests = default,
    ImmutableArray<string> Requires = default,
    ImmutableArray<string> Alternatives = default,
    ImmutableArray<int> WindowsVersions = default,
    bool Hidden = false,
    string? License = null,
    string? Source = null);
