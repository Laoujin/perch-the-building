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
    private readonly IAppDetailService _detailService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IPendingChangesService _pendingChanges;

    private ImmutableArray<AppCardModel> _allYourApps = [];
    private ImmutableArray<AppCardModel> _allSuggested = [];
    private ImmutableArray<AppCardModel> _allOther = [];
    private readonly Dictionary<string, AppDetail> _detailCache = new(StringComparer.Ordinal);

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

    public ObservableCollection<AppCardModel> YourApps { get; } = [];
    public ObservableCollection<AppCardModel> SuggestedApps { get; } = [];
    public ObservableCollection<AppCategoryCardModel> BrowseCategories { get; } = [];

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

    partial void OnSearchTextChanged(string value) => RebuildTiers();

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

            RebuildTiers();
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

    private void RebuildTiers()
    {
        var query = SearchText;

        YourApps.Clear();
        SuggestedApps.Clear();
        BrowseCategories.Clear();

        foreach (var app in SortTier1(_allYourApps.Where(a => a.MatchesSearch(query))))
            YourApps.Add(app);

        foreach (var app in _allSuggested.Where(a => a.MatchesSearch(query)).OrderBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase))
            SuggestedApps.Add(app);

        var otherFiltered = _allOther.Where(a => a.MatchesSearch(query));
        var groups = otherFiltered
            .GroupBy(a => a.BroadCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var items = group.ToList();
            BrowseCategories.Add(new AppCategoryCardModel(
                group.Key,
                group.Key,
                items.Count,
                items.Count(a => a.IsManaged)));
        }

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var all = _allYourApps.AsEnumerable()
            .Concat(_allSuggested)
            .Concat(_allOther);

        LinkedCount = all.Count(a => a.Status == CardStatus.Linked);
        DriftedCount = all.Count(a => a.Status is CardStatus.Drift or CardStatus.Broken);
        DetectedCount = all.Count(a => a.Status == CardStatus.Detected);
    }

    private static IEnumerable<AppCardModel> SortTier1(IEnumerable<AppCardModel> apps)
    {
        return apps
            .OrderBy(a => StatusSortOrder(a.Status))
            .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase);
    }

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
    private async Task ExpandAppAsync(AppCardModel app)
    {
        app.IsExpanded = !app.IsExpanded;

        if (!app.IsExpanded || app.Detail is not null)
            return;

        if (_detailCache.TryGetValue(app.Id, out var cached))
        {
            app.Detail = cached;
            return;
        }

        app.IsLoadingDetail = true;
        try
        {
            var detail = await _detailService.LoadDetailAsync(app);
            _detailCache[app.Id] = detail;
            app.Detail = detail;
        }
        catch
        {
            // Detail load failure is non-critical — card stays expanded without detail sections
        }
        finally
        {
            app.IsLoadingDetail = false;
        }
    }

    [RelayCommand]
    private void TagClick(string tag) => SearchText = tag;

    public IEnumerable<AppCardModel> GetCategoryApps(string broadCategory)
    {
        return _allOther
            .Where(a => string.Equals(a.BroadCategory, broadCategory, StringComparison.OrdinalIgnoreCase))
            .Where(a => a.MatchesSearch(SearchText))
            .OrderBy(a => StatusSortOrder(a.Status))
            .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase);
    }

    private void BuildDependencyGraph(
        ImmutableArray<AppCardModel> yourApps,
        ImmutableArray<AppCardModel> suggested,
        ImmutableArray<AppCardModel> other)
    {
        var allApps = yourApps.AsEnumerable().Concat(suggested).Concat(other).ToList();
        var byId = allApps.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);

        // Build reverse map: parent -> children that require it
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

                // Circular check: if parent also requires this app, skip
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

        // Assign dependents to parents
        foreach (var (parentId, dependents) in reverseMap)
        {
            if (byId.TryGetValue(parentId, out var parent))
                parent.DependentApps = [.. dependents];
        }

        // Filter children from top-level collections
        _allYourApps = yourApps.Where(a => !childIds.Contains(a.Id)).ToImmutableArray();
        _allSuggested = suggested.Where(a => !childIds.Contains(a.Id)).ToImmutableArray();
        _allOther = other.Where(a => !childIds.Contains(a.Id)).ToImmutableArray();
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
