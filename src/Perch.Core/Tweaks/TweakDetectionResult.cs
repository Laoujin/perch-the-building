using System.Collections.Immutable;

using Perch.Core.Modules;

namespace Perch.Core.Tweaks;

public sealed record RegistryEntryStatus(RegistryEntryDefinition Definition, object? CurrentValue, bool IsApplied);

public sealed record TweakDetectionResult(TweakStatus Status, ImmutableArray<RegistryEntryStatus> Entries);
