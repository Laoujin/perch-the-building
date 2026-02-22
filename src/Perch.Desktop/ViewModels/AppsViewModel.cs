using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.Input;

using Perch.Core.Catalog;
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
    private readonly ICatalogService _catalogService;

    private ImmutableArray<AppCardModel> _allApps = [];
    private Dictionary<string, AppCardModel> _allAppsByIdIncludingChildren = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<UserProfile> _activeProfiles = [UserProfile.Developer, UserProfile.PowerUser];
    private ImmutableDictionary<string, CategoryDefinition> _categories = ImmutableDictionary<string, CategoryDefinition>.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private int _linkedCount;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private int _driftedCount;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private int _detectedCount;

    public override bool ShowGrid => true;
    public override bool ShowDetail => false;

    public BulkObservableCollection<AppCategoryCardModel> Categories { get; } = [];

    public AppsViewModel(
        IGalleryDetectionService detectionService,
        IAppDetailService detailService,
        ISettingsProvider settingsProvider,
        IPendingChangesService pendingChanges,
        ICatalogService catalogService)
    {
        _detectionService = detectionService;
        _detailService = detailService;
        _settingsProvider = settingsProvider;
        _pendingChanges = pendingChanges;
        _catalogService = catalogService;
    }

    protected override void OnSearchTextUpdated() => RebuildCategories();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _categories = await _catalogService.GetCategoriesAsync(cancellationToken);
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
                            sg.OrderBy(a => StatusSortOrder(a.Status))
                              .ThenByDescending(a => a.IsHot)
                              .ThenByDescending(a => a.GitHubStars ?? 0)
                              .ThenBy(a => IsCliTool(a) ? 1 : 0)
                              .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase))))
                    .ToList();

                return new AppCategoryCardModel(
                    g.Key,
                    g.Key,
                    items.Count,
                    items.Count(a => a.IsManaged),
                    items.Count(a => a.Status is CardStatus.Detected or CardStatus.Synced or CardStatus.Drifted),
                    items.Count(a => a.IsSuggested),
                    subGroups: subGroups);
            });

        Categories.ReplaceAll(categories);
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        LinkedCount = _allApps.Count(a => a.Status == CardStatus.Synced);
        DriftedCount = _allApps.Count(a => a.Status == CardStatus.Drifted);
        DetectedCount = _allApps.Count(a => a.Status == CardStatus.Detected);
    }

    private static int StatusSortOrder(CardStatus status) => status switch
    {
        CardStatus.Drifted => 0,
        CardStatus.Synced or CardStatus.PendingRemove => 1,
        CardStatus.Detected or CardStatus.PendingAdd => 2,
        _ => 3,
    };

    private static bool IsCliTool(AppCardModel app) =>
        app.CatalogEntry.Kind == CatalogKind.CliTool;

    [RelayCommand]
    private void ToggleApp(AppCardModel app)
    {
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
    private async Task ToggleExpandAsync(AppCardModel card, CancellationToken cancellationToken)
    {
        if (card.IsExpanded)
        {
            card.IsExpanded = false;
            return;
        }

        card.IsExpanded = true;

        if (card.Detail is not null)
            return;

        card.IsLoadingDetail = true;
        try
        {
            card.Detail = await _detailService.LoadDetailAsync(card, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            card.IsExpanded = false;
            return;
        }
        finally
        {
            card.IsLoadingDetail = false;
        }
    }

    [RelayCommand]
    private void NavigateToApp(string appId)
    {
        if (_allAppsByIdIncludingChildren.TryGetValue(appId, out var target))
            SearchText = target.DisplayLabel;
        else
            SearchText = appId;
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
            var candidates = group.Where(a => a.Status == CardStatus.Unmanaged).ToList();
            if (candidates.Count < 2)
                continue;

            var sorted = candidates
                .Where(a => a.GitHubStars is > 0)
                .OrderByDescending(a => a.GitHubStars!.Value)
                .ToList();

            if (sorted.Count < 2)
                continue;

            if (sorted[0].GitHubStars!.Value >= sorted[1].GitHubStars!.Value * 2)
                sorted[0].IsHot = true;
        }
    }

    private int GetBroadCategoryPriority(IEnumerable<AppCardModel> appsInCategory)
    {
        return appsInCategory.Any(a =>
            !a.CatalogEntry.Profiles.IsDefaultOrEmpty
            && ProfileMatcher.Matches(a.CatalogEntry.Profiles, _activeProfiles)) ? 0 : 1;
    }

    private int GetSubCategoryPriority(string broadCategory, string subCategory)
    {
        if (!_categories.TryGetValue(broadCategory, out var category))
            return int.MaxValue;

        if (!category.Children.TryGetValue(subCategory, out var subCat))
            return int.MaxValue;

        return subCat.Sort;
    }

}
