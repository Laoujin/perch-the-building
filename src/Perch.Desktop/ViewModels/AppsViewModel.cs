using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels;

public sealed partial class AppsViewModel : ViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IPendingChangesService _pendingChanges;

    private ImmutableArray<AppCardModel> _allApps = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _linkedCount;

    [ObservableProperty]
    private int _driftedCount;

    [ObservableProperty]
    private int _detectedCount;

    public ObservableCollection<AppCategoryCardModel> Categories { get; } = [];

    public AppsViewModel(
        IGalleryDetectionService detectionService,
        IAppDetailService detailService,
        ISettingsProvider settingsProvider,
        IPendingChangesService pendingChanges)
    {
        _detectionService = detectionService;
        _ = detailService; // kept for DI compatibility
        _settingsProvider = settingsProvider;
        _pendingChanges = pendingChanges;
    }

    partial void OnSearchTextChanged(string value) => RebuildCategories();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var profiles = await LoadProfilesAsync(cancellationToken);
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
        Categories.Clear();

        var filtered = _allApps.Where(a => a.MatchesSearch(query));
        var groups = filtered
            .GroupBy(a => a.BroadCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var items = group.ToList();
            Categories.Add(new AppCategoryCardModel(
                group.Key,
                group.Key,
                items.Count,
                items.Count(a => a.IsManaged)));
        }

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
        CardStatus.Drift => 0,
        CardStatus.Broken => 0,
        CardStatus.Detected => 1,
        CardStatus.Linked => 2,
        _ => 3,
    };

    [RelayCommand]
    private void ToggleApp(AppCardModel app)
    {
        if (!app.CanToggle)
            return;

        if (app.IsManaged)
        {
            _pendingChanges.Remove(app.Id, PendingChangeKind.LinkApp);
            _pendingChanges.Add(new UnlinkAppChange(app));
        }
        else
        {
            _pendingChanges.Remove(app.Id, PendingChangeKind.UnlinkApp);
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

    public IEnumerable<AppCategoryGroup> GetCategorySubGroups(string broadCategory)
    {
        return _allApps
            .Where(a => string.Equals(a.BroadCategory, broadCategory, StringComparison.OrdinalIgnoreCase))
            .Where(a => a.MatchesSearch(SearchText))
            .GroupBy(a => a.SubCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AppCategoryGroup(
                g.Key,
                new ObservableCollection<AppCardModel>(
                    g.OrderBy(a => TierSortOrder(a.Tier))
                     .ThenBy(a => StatusSortOrder(a.Status))
                     .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase))));
    }

    private void BuildDependencyGraph(
        ImmutableArray<AppCardModel> yourApps,
        ImmutableArray<AppCardModel> suggested,
        ImmutableArray<AppCardModel> other)
    {
        var allApps = yourApps.AsEnumerable().Concat(suggested).Concat(other).ToList();
        var byId = allApps.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);

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
    }

    private async Task<HashSet<UserProfile>> LoadProfilesAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        var profiles = new HashSet<UserProfile>();
        if (settings.Profiles is { Count: > 0 })
        {
            foreach (var name in settings.Profiles)
            {
                if (Enum.TryParse<UserProfile>(name, ignoreCase: true, out var profile))
                    profiles.Add(profile);
            }
        }

        if (profiles.Count == 0)
            profiles = [UserProfile.Developer, UserProfile.PowerUser];

        return profiles;
    }
}

public sealed record AppCategoryGroup(string SubCategory, ObservableCollection<AppCardModel> Apps);
