using System.Collections.Immutable;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Catalog;
using Perch.Core.Modules;
using Perch.Core.Tweaks;

namespace Perch.Desktop.Models;

public partial class TweakCardModel : ObservableObject
{
    public TweakCatalogEntry CatalogEntry { get; }
    public string Id { get; }
    public string Name { get; }
    public string Category { get; }
    public string? Description { get; }
    public ImmutableArray<string> Tags { get; }
    public ImmutableArray<string> Profiles { get; }
    public bool Reversible { get; }
    public ImmutableArray<RegistryEntryDefinition> Registry { get; }

    [ObservableProperty]
    private CardStatus _status;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private int _appliedCount;

    [ObservableProperty]
    private bool _isSuggested;

    [ObservableProperty]
    private ImmutableArray<RegistryEntryStatus> _detectedEntries;

    public string BroadCategory => Category.Split('/')[0];
    public string SubCategory => Category.Contains('/') ? Category[(Category.IndexOf('/') + 1)..] : Category;

    public int TotalCount => Registry.Length;
    public bool RestartRequired => Tags.Any(t => string.Equals(t, "restart", StringComparison.OrdinalIgnoreCase));
    public string RegistryKeyCountText => TotalCount == 1 ? "1 registry key" : $"{TotalCount} registry keys";
    public bool IsAllApplied => AppliedCount == TotalCount && TotalCount > 0;
    public bool HasCapturedValues => !DetectedEntries.IsDefaultOrEmpty
        && DetectedEntries.Any(e => e.CapturedValue != null);

    public TweakCardModel(TweakCatalogEntry entry, CardStatus status)
    {
        CatalogEntry = entry;
        Id = entry.Id;
        Name = entry.Name;
        Category = entry.Category;
        Description = entry.Description;
        Tags = entry.Tags;
        Profiles = entry.Profiles;
        Reversible = entry.Reversible;
        Registry = entry.Registry;
        Status = status;
    }

    public bool MatchesProfile(IEnumerable<UserProfile> profiles)
    {
        if (Profiles.IsDefaultOrEmpty)
            return true;

        return profiles.Any(p => Profiles.Contains(p.ToString().ToLowerInvariant().Replace("poweruser", "power-user")));
    }

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || Category.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
