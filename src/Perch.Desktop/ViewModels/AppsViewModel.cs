using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels;

public sealed partial class AppsViewModel : GalleryViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly IAppDetailService _detailService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IPendingChangesService _pendingChanges;

    private ImmutableArray<AppCardModel> _allApps = [];
    private Dictionary<string, AppCardModel> _allAppsByIdIncludingChildren = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<UserProfile> _activeProfiles = [UserProfile.Developer, UserProfile.PowerUser];

    private static readonly Dictionary<string, string[]> _subcategoryOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Development"] = ["IDEs", "Editors", "Languages", "Version Control", "Terminals", "CLI Tools", "Containers", "Shells", "Tools", "API Tools", "Databases", "Build Tools", "Diff Tools", ".NET", "Node"],
        ["Gaming"] = ["Stores", "Launchers", "Streaming", "Controllers", "Performance", "Saves", "Modding", "Emulators"],
        ["Utilities"] = ["Productivity", "System", "Compression", "Screenshots", "Clipboard", "PDF", "Storage", "Downloads", "Uninstallers", "System Monitors"],
        ["Media"] = ["Players", "Video", "Audio", "Graphics", "3D"],
        ["Communication"] = ["Chat", "Video", "Email"],
        ["Security"] = ["Passwords", "Encryption", "Downloads", "Sandboxing"],
        ["Networking"] = ["Remote Access", "FTP", "Protocol", "Security"],
    };

    [ObservableProperty]
    private int _linkedCount;

    [ObservableProperty]
    private int _driftedCount;

    [ObservableProperty]
    private int _detectedCount;

    [ObservableProperty]
    private AppCardModel? _selectedApp;

    [ObservableProperty]
    private AppDetail? _detail;

    [ObservableProperty]
    private bool _isLoadingDetail;

    public override bool ShowGrid => SelectedApp is null;
    public override bool ShowDetail => SelectedApp is not null;
    public bool HasModule => Detail?.OwningModule is not null;
    public bool HasEcosystem => EcosystemGroups.Count > 0;
    public bool HasAlternatives => AlternativeApps.Count > 0;

    public BulkObservableCollection<AppCategoryCardModel> Categories { get; } = [];
    public BulkObservableCollection<EcosystemGroup> EcosystemGroups { get; } = [];
    public BulkObservableCollection<AppCardModel> AlternativeApps { get; } = [];

    public AppsViewModel(
        IGalleryDetectionService detectionService,
        IAppDetailService detailService,
        ISettingsProvider settingsProvider,
        IPendingChangesService pendingChanges)
    {
        _detectionService = detectionService;
        _detailService = detailService;
        _settingsProvider = settingsProvider;
        _pendingChanges = pendingChanges;
    }

    partial void OnSelectedAppChanged(AppCardModel? value)
    {
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(ShowDetail));
    }

    partial void OnDetailChanged(AppDetail? value)
    {
        OnPropertyChanged(nameof(HasModule));
    }

    protected override void OnSearchTextUpdated() => RebuildCategories();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var profiles = await LoadProfilesAsync(_settingsProvider, cancellationToken);
            _activeProfiles = profiles;
            var result = await _detectionService.DetectAppsAsync(profiles, cancellationToken);

            BuildDependencyGraph(result.YourApps, result.Suggested, result.OtherApps);

            RebuildCategories();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load applications: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RebuildCategories()
    {
        var query = SearchText;

        var categories = _allApps
            .Where(a => a.MatchesSearch(query))
            .GroupBy(a => a.BroadCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => GetBroadCategoryPriority(g))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var items = g.ToList();
                var subGroups = items
                    .GroupBy(a => a.SubCategory, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(sg => GetSubCategoryPriority(g.Key, sg.Key))
                    .ThenBy(sg => sg.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(sg => new AppCategoryGroup(
                        sg.Key,
                        new ObservableCollection<AppCardModel>(
                            sg.OrderBy(a => TierSortOrder(a.Tier))
                              .ThenBy(a => StatusSortOrder(a.Status))
                              .ThenByDescending(a => a.GitHubStars ?? 0)
                              .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase))))
                    .ToList();

                return new AppCategoryCardModel(
                    g.Key,
                    g.Key,
                    items.Count,
                    items.Count(a => a.IsManaged),
                    items.Count(a => a.Status is CardStatus.Detected or CardStatus.Linked or CardStatus.Drift or CardStatus.Broken),
                    items.Count(a => a.IsSuggested),
                    subGroups: subGroups);
            });

        Categories.ReplaceAll(categories);
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        LinkedCount = _allApps.Count(a => a.Status == CardStatus.Linked);
        DriftedCount = _allApps.Count(a => a.Status is CardStatus.Drift or CardStatus.Broken);
        DetectedCount = _allApps.Count(a => a.Status == CardStatus.Detected);
    }

    private static int TierSortOrder(CardTier tier) => tier switch
    {
        CardTier.YourApps => 0,
        CardTier.Suggested => 1,
        _ => 2,
    };

    private static int StatusSortOrder(CardStatus status) => status switch
    {
        CardStatus.Drift or CardStatus.Broken => 0,
        _ => 1,
    };

    [RelayCommand]
    private void ToggleApp(AppCardModel app)
    {
        if (!app.CanToggle)
            return;

        if (_pendingChanges.Contains(app.Id, PendingChangeKind.LinkApp))
        {
            _pendingChanges.Remove(app.Id, PendingChangeKind.LinkApp);
        }
        else if (_pendingChanges.Contains(app.Id, PendingChangeKind.UnlinkApp))
        {
            _pendingChanges.Remove(app.Id, PendingChangeKind.UnlinkApp);
        }
        else if (app.IsManaged)
        {
            _pendingChanges.Add(new UnlinkAppChange(app));
        }
        else
        {
            _pendingChanges.Add(new LinkAppChange(app));
        }
    }

    [RelayCommand]
    private void ToggleCategoryExpand(AppCategoryCardModel category)
    {
        category.IsExpanded = !category.IsExpanded;
    }

    [RelayCommand]
    private void TagClick(string tag) => SearchText = tag;

    [RelayCommand]
    private async Task ConfigureAppAsync(AppCardModel card, CancellationToken cancellationToken)
    {
        SelectedApp = card;
        Detail = null;
        EcosystemGroups.ReplaceAll([]);
        AlternativeApps.ReplaceAll([]);
        OnPropertyChanged(nameof(HasEcosystem));
        OnPropertyChanged(nameof(HasAlternatives));
        IsLoadingDetail = true;

        try
        {
            Detail = await _detailService.LoadDetailAsync(card, cancellationToken);
            BuildEcosystemGroups(card);
            BuildAlternativeApps();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            IsLoadingDetail = false;
        }
    }

    [RelayCommand]
    private void BackToGrid()
    {
        SelectedApp = null;
        Detail = null;
        EcosystemGroups.ReplaceAll([]);
        AlternativeApps.ReplaceAll([]);
        OnPropertyChanged(nameof(HasEcosystem));
        OnPropertyChanged(nameof(HasAlternatives));
    }

    private void BuildEcosystemGroups(AppCardModel card)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { card.Id };
        var ecosystemApps = new List<AppCardModel>();

        if (!card.DependentApps.IsDefaultOrEmpty)
        {
            foreach (var dep in card.DependentApps)
            {
                if (seen.Add(dep.Id))
                    ecosystemApps.Add(dep);
            }
        }

        if (!card.CatalogEntry.Suggests.IsDefaultOrEmpty)
        {
            foreach (var suggestId in card.CatalogEntry.Suggests)
            {
                if (seen.Add(suggestId) && _allAppsByIdIncludingChildren.TryGetValue(suggestId, out var suggested))
                    ecosystemApps.Add(suggested);
            }
        }

        var groups = ecosystemApps
            .GroupBy(a => a.SubCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => GetSubCategoryPriority(card.BroadCategory, g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new EcosystemGroup(
                g.Key,
                g.OrderBy(a => TierSortOrder(a.Tier))
                 .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                 .ToImmutableArray()));

        EcosystemGroups.ReplaceAll(groups);
        OnPropertyChanged(nameof(HasEcosystem));
    }

    private void BuildAlternativeApps()
    {
        if (Detail is not null && !Detail.Alternatives.IsDefaultOrEmpty)
        {
            AlternativeApps.ReplaceAll(
                Detail.Alternatives
                    .Select(alt => _allAppsByIdIncludingChildren.GetValueOrDefault(alt.Id))
                    .Where(card => card is not null)!);
        }
        else
        {
            AlternativeApps.ReplaceAll([]);
        }

        OnPropertyChanged(nameof(HasAlternatives));
    }

    private void BuildDependencyGraph(
        ImmutableArray<AppCardModel> yourApps,
        ImmutableArray<AppCardModel> suggested,
        ImmutableArray<AppCardModel> other)
    {
        var allApps = yourApps.AsEnumerable().Concat(suggested).Concat(other).ToList();
        _allAppsByIdIncludingChildren = allApps.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        var byId = _allAppsByIdIncludingChildren;

        var reverseMap = new Dictionary<string, List<AppCardModel>>(StringComparer.OrdinalIgnoreCase);
        var childIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in allApps)
        {
            if (app.CatalogEntry.Requires.IsDefaultOrEmpty)
                continue;

            foreach (var reqId in app.CatalogEntry.Requires)
            {
                if (!byId.ContainsKey(reqId))
                    continue;

                var parent = byId[reqId];
                if (!parent.CatalogEntry.Requires.IsDefaultOrEmpty &&
                    parent.CatalogEntry.Requires.Contains(app.Id, StringComparer.OrdinalIgnoreCase))
                    continue;

                if (!reverseMap.TryGetValue(reqId, out var list))
                {
                    list = [];
                    reverseMap[reqId] = list;
                }
                list.Add(app);
                childIds.Add(app.Id);
            }
        }

        foreach (var (parentId, dependents) in reverseMap)
        {
            if (byId.TryGetValue(parentId, out var parent))
                parent.DependentApps = [.. dependents];
        }

        _allApps = allApps.Where(a => !childIds.Contains(a.Id)).ToImmutableArray();
        ComputeTopPicks(_allApps);
    }

    private static void ComputeTopPicks(ImmutableArray<AppCardModel> allApps)
    {
        var alternativeGroups = new Dictionary<string, List<AppCardModel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in allApps)
        {
            if (app.CatalogEntry.Alternatives.IsDefaultOrEmpty)
                continue;

            var groupKey = string.Join(",", app.CatalogEntry.Alternatives
                .Append(app.Id)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            if (!alternativeGroups.TryGetValue(groupKey, out var list))
            {
                list = [];
                alternativeGroups[groupKey] = list;
            }
            list.Add(app);
        }

        foreach (var group in alternativeGroups.Values)
        {
            var candidates = group.Where(a => a.Status == CardStatus.NotInstalled).ToList();
            if (candidates.Count < 2)
                continue;

            var sorted = candidates
                .Where(a => a.GitHubStars is > 0)
                .OrderByDescending(a => a.GitHubStars!.Value)
                .ToList();

            if (sorted.Count < 2)
                continue;

            if (sorted[0].GitHubStars!.Value >= sorted[1].GitHubStars!.Value * 2)
                sorted[0].IsTopPick = true;
        }
    }

    private int GetBroadCategoryPriority(IEnumerable<AppCardModel> appsInCategory)
    {
        return appsInCategory.Any(a =>
            !a.CatalogEntry.Profiles.IsDefaultOrEmpty
            && ProfileMatcher.Matches(a.CatalogEntry.Profiles, _activeProfiles)) ? 0 : 1;
    }

    private static int GetSubCategoryPriority(string broadCategory, string subCategory)
    {
        if (!_subcategoryOrder.TryGetValue(broadCategory, out var order))
            return int.MaxValue;

        var index = Array.FindIndex(order, s => string.Equals(s, subCategory, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : int.MaxValue;
    }

}

public sealed record EcosystemGroup(string Name, ImmutableArray<AppCardModel> Apps);
