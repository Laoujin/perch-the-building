using System.Collections.Immutable;

namespace Perch.Core.Packages;

public sealed record PackageDefinition(
    string Name,
    PackageManager Manager,
    ImmutableArray<string> AlternativeIds = default);
