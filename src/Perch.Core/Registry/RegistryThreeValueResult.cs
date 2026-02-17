using Perch.Core.Modules;

namespace Perch.Core.Registry;

public sealed record RegistryThreeValueResult(
    RegistryEntryDefinition Entry,
    ThreeValueStatus Status,
    object? CurrentValue);
