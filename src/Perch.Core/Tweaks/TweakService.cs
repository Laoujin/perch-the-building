using System.Collections.Immutable;

using Perch.Core.Catalog;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Registry;

namespace Perch.Core.Tweaks;

public sealed class TweakService : ITweakService
{
    private readonly IRegistryProvider _registryProvider;

    public TweakService(IRegistryProvider registryProvider)
    {
        _registryProvider = registryProvider;
    }

    public TweakDetectionResult Detect(TweakCatalogEntry tweak)
    {
        if (tweak.Registry.IsDefaultOrEmpty)
        {
            return new TweakDetectionResult(TweakStatus.Applied, ImmutableArray<RegistryEntryStatus>.Empty);
        }

        var entries = ImmutableArray.CreateBuilder<RegistryEntryStatus>(tweak.Registry.Length);
        int appliedCount = 0;

        foreach (RegistryEntryDefinition entry in tweak.Registry)
        {
            object? currentValue = _registryProvider.GetValue(entry.Key, entry.Name);
            bool isApplied = Equals(currentValue, entry.Value);
            entries.Add(new RegistryEntryStatus(entry, currentValue, isApplied));
            if (isApplied) appliedCount++;
        }

        TweakStatus status = appliedCount == tweak.Registry.Length
            ? TweakStatus.Applied
            : appliedCount == 0
                ? TweakStatus.NotApplied
                : TweakStatus.Partial;

        return new TweakDetectionResult(status, entries.MoveToImmutable());
    }

    public TweakOperationResult Apply(TweakCatalogEntry tweak, bool dryRun = false)
    {
        if (tweak.Registry.IsDefaultOrEmpty)
        {
            return new TweakOperationResult(ResultLevel.Ok, ImmutableArray<TweakEntryResult>.Empty);
        }

        var results = ImmutableArray.CreateBuilder<TweakEntryResult>(tweak.Registry.Length);
        ResultLevel overall = ResultLevel.Ok;

        foreach (RegistryEntryDefinition entry in tweak.Registry)
        {
            string location = $@"{entry.Key}\{entry.Name}";

            if (dryRun)
            {
                results.Add(new TweakEntryResult(entry.Key, entry.Name, ResultLevel.Ok,
                    $"Would set {location} to {entry.Value}"));
                continue;
            }

            object? currentValue = _registryProvider.GetValue(entry.Key, entry.Name);
            if (Equals(currentValue, entry.Value))
            {
                results.Add(new TweakEntryResult(entry.Key, entry.Name, ResultLevel.Ok,
                    $"Already set to {entry.Value}"));
                continue;
            }

            if (entry.Value == null)
            {
                _registryProvider.DeleteValue(entry.Key, entry.Name);
                results.Add(new TweakEntryResult(entry.Key, entry.Name, ResultLevel.Ok,
                    $"Deleted {entry.Name}"));
            }
            else
            {
                _registryProvider.SetValue(entry.Key, entry.Name, entry.Value, entry.Kind);
                results.Add(new TweakEntryResult(entry.Key, entry.Name, ResultLevel.Ok,
                    $"Set {entry.Name} to {entry.Value}"));
            }
        }

        return new TweakOperationResult(overall, results.MoveToImmutable());
    }

    public TweakOperationResult Revert(TweakCatalogEntry tweak, bool dryRun = false)
    {
        if (!tweak.Reversible)
        {
            return new TweakOperationResult(ResultLevel.Error,
                [new TweakEntryResult("", "", ResultLevel.Error, "Tweak is not reversible")]);
        }

        if (tweak.Registry.IsDefaultOrEmpty)
        {
            return new TweakOperationResult(ResultLevel.Ok, ImmutableArray<TweakEntryResult>.Empty);
        }

        var results = ImmutableArray.CreateBuilder<TweakEntryResult>(tweak.Registry.Length);

        foreach (RegistryEntryDefinition entry in tweak.Registry)
        {
            if (dryRun)
            {
                string action = entry.DefaultValue != null
                    ? $"Would restore {entry.Name} to {entry.DefaultValue}"
                    : $"Would delete {entry.Name}";
                results.Add(new TweakEntryResult(entry.Key, entry.Name, ResultLevel.Ok, action));
                continue;
            }

            if (entry.DefaultValue != null)
            {
                _registryProvider.SetValue(entry.Key, entry.Name, entry.DefaultValue, entry.Kind);
                results.Add(new TweakEntryResult(entry.Key, entry.Name, ResultLevel.Ok,
                    $"Restored {entry.Name} to {entry.DefaultValue}"));
            }
            else
            {
                _registryProvider.DeleteValue(entry.Key, entry.Name);
                results.Add(new TweakEntryResult(entry.Key, entry.Name, ResultLevel.Ok,
                    $"Deleted {entry.Name}"));
            }
        }

        return new TweakOperationResult(ResultLevel.Ok, results.MoveToImmutable());
    }
}
