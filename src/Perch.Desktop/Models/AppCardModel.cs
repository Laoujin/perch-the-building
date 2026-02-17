using System.Collections.Immutable;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Catalog;

namespace Perch.Desktop.Models;

public partial class AppCardModel : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public string? DisplayName { get; }
    public string Category { get; }
    public string? Description { get; }
    public ImmutableArray<string> Tags { get; }
    public InstallDefinition? Install { get; }
    public CatalogConfigDefinition? Config { get; }
    public CardTier Tier { get; }
    public CatalogEntry CatalogEntry { get; }
    public string? Website { get; }
    public string? GitHub { get; }

    [ObservableProperty]
    private CardStatus _status;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    public string DisplayLabel => DisplayName ?? Name;

    public bool CanLink => Status == CardStatus.Detected;
    public bool CanUnlink => Status == CardStatus.Linked;
    public bool CanFix => Status is CardStatus.Broken or CardStatus.Drift;

    public AppCardModel(CatalogEntry entry, CardTier tier, CardStatus status)
    {
        Id = entry.Id;
        Name = entry.Name;
        DisplayName = entry.DisplayName;
        Category = entry.Category;
        Description = entry.Description;
        Tags = entry.Tags;
        Install = entry.Install;
        Config = entry.Config;
        Tier = tier;
        Status = status;
        CatalogEntry = entry;
        Website = entry.Links?.Website;
        GitHub = entry.Links?.GitHub;
    }

    partial void OnStatusChanged(CardStatus value)
    {
        OnPropertyChanged(nameof(CanLink));
        OnPropertyChanged(nameof(CanUnlink));
        OnPropertyChanged(nameof(CanFix));
    }

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (DisplayName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || Category.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
