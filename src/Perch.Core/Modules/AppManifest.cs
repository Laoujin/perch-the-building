using System.Collections.Immutable;
using Perch.Core;

namespace Perch.Core.Modules;

public sealed record AppManifest(string ModuleName, string DisplayName, ImmutableArray<Platform> Platforms, ImmutableArray<LinkEntry> Links);
