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
    public string? Docs { get; }
    public string? License { get; }
    public string? LogoUrl { get; }

    [ObservableProperty]
    private CardStatus _status;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLoadingDetail;

    [ObservableProperty]
    private AppDetail? _detail;

    public int? GitHubStars { get; set; }
    public bool IsTopPick { get; set; }

    public string? GitHubStarsFormatted => GitHubStars switch
    {
        null or 0 => null,
        >= 1000 => $"{GitHubStars.Value / 1000.0:0.#}k",
        _ => $"{GitHubStars.Value}",
    };

    public ImmutableArray<AppCardModel> DependentApps { get; set; } = [];
    public bool HasDependents => !DependentApps.IsDefaultOrEmpty;
    public int DependentAppCount => DependentApps.IsDefaultOrEmpty ? 0 : DependentApps.Length;

    public string DisplayLabel => DisplayName ?? Name;
    public string BroadCategory => Category.Split('/')[0];
    public string SubCategory => Category.Contains('/') ? Category[(Category.IndexOf('/') + 1)..] : Category;

    public bool IsManaged => Status is CardStatus.Linked or CardStatus.Drift or CardStatus.Broken;
    public bool CanToggle => Status != CardStatus.NotInstalled;
    public bool IsSuggested => Tier == CardTier.Suggested;

    public string? KindBadge => CatalogEntry.Kind switch
    {
        CatalogKind.CliTool => "cli-tool",
        CatalogKind.Runtime => "runtime",
        CatalogKind.Dotfile => "dotfile",
        _ => null,
    };

    public AppCardModel(CatalogEntry entry, CardTier tier, CardStatus status, string? logoUrl = null)
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
        Docs = entry.Links?.Docs;
        License = entry.License;
        LogoUrl = logoUrl;
    }

    partial void OnStatusChanged(CardStatus value)
    {
        OnPropertyChanged(nameof(IsManaged));
        OnPropertyChanged(nameof(CanToggle));
    }

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return MatchesSelf(query) || DependentApps.Any(d => d.MatchesSelf(query));
    }

    private bool MatchesSelf(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (DisplayName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || Category.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));
}
