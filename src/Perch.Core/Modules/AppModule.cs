using System.Collections.Immutable;
using Perch.Core;

namespace Perch.Core.Modules;

public sealed record AppModule(string Name, string DisplayName, string ModulePath, ImmutableArray<Platform> Platforms, ImmutableArray<LinkEntry> Links, DeployHooks? Hooks = null, CleanFilterDefinition? CleanFilter = null);
