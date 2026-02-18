using System.Collections.Immutable;

using Perch.Core.Deploy;

namespace Perch.Core.Tweaks;

public sealed record TweakEntryResult(string Key, string Name, ResultLevel Level, string Message);

public sealed record TweakOperationResult(ResultLevel Level, ImmutableArray<TweakEntryResult> Entries);
