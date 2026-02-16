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

    [ObservableProperty]
    private CardStatus _status;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    public string DisplayLabel => DisplayName ?? Name;

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
