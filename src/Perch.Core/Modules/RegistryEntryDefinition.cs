using Perch.Core.Registry;

namespace Perch.Core.Modules;

public sealed record RegistryEntryDefinition(string Key, string Name, object? Value, RegistryValueType Kind, object? DefaultValue = null);
