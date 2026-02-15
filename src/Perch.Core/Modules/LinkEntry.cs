using System.Collections.Immutable;
using Perch.Core;

namespace Perch.Core.Modules;

public sealed record LinkEntry(string Source, string? Target, ImmutableDictionary<Platform, string>? PlatformTargets, LinkType LinkType)
{
    public LinkEntry(string source, string target, LinkType linkType)
        : this(source, target, null, linkType) { }

    public string? GetTargetForPlatform(Platform platform) =>
        Target ?? (PlatformTargets?.TryGetValue(platform, out var t) == true ? t : null);
}
