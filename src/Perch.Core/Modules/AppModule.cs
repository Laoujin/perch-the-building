using System.Collections.Immutable;
using Perch.Core;

namespace Perch.Core.Modules;

public sealed record AppModule(string Name, string DisplayName, bool Enabled, string ModulePath, ImmutableArray<Platform> Platforms, ImmutableArray<LinkEntry> Links, DeployHooks? Hooks = null, CleanFilterDefinition? CleanFilter = null, ImmutableArray<RegistryEntryDefinition> Registry = default, GlobalPackagesDefinition? GlobalPackages = null, ImmutableArray<string> VscodeExtensions = default, ImmutableArray<string> PsModules = default, ImmutableArray<PathEntry> PathEntries = default);
