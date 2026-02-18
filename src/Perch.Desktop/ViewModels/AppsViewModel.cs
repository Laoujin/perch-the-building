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

    private ImmutableArray<AppCardModel> _allApps = [];
    private Dictionary<string, AppCardModel> _allAppsByIdIncludingChildren = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<UserProfile> _activeProfiles = [UserProfile.Developer, UserProfile.PowerUser];

    private static readonly Dictionary<string, UserProfile[]> _broadCategoryProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Development"] = [UserProfile.Developer],
        ["System"] = [UserProfile.PowerUser],
        ["Utilities"] = [UserProfile.PowerUser],
        ["Media"] = [UserProfile.Gamer, UserProfile.Casual],
        ["Gaming"] = [UserProfile.Gamer],
        ["Communication"] = [UserProfile.Casual],
        ["Browsers"] = [UserProfile.Casual],
        ["Appearance"] = [],
    };

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

    [ObservableProperty]
    private AppCardModel? _selectedApp;

    [ObservableProperty]
    private AppDetail? _detail;

    [ObservableProperty]
    private bool _isLoadingDetail;

    public bool ShowCardGrid => SelectedApp is null;
    public bool ShowDetailView => SelectedApp is not null;
    public bool HasModule => Detail?.OwningModule is not null;
    public bool HasEcosystem => EcosystemGroups.Count > 0;
    public bool HasAlternatives => AlternativeApps.Count > 0;

    public ObservableCollection<AppCategoryCardModel> Categories { get; } = [];
    public ObservableCollection<EcosystemGroup> EcosystemGroups { get; } = [];
    public ObservableCollection<AppCardModel> AlternativeApps { get; } = [];

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
        OnPropertyChanged(nameof(ShowCardGrid));
        OnPropertyChanged(nameof(ShowDetailView));
    }

    partial void OnDetailChanged(AppDetail? value)
    {
        OnPropertyChanged(nameof(HasModule));
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
        Categories.Clear();

        var filtered = _allApps.Where(a => a.MatchesSearch(query));
        var groups = filtered
            .GroupBy(a => a.BroadCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => GetBroadCategoryPriority(g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var items = group.ToList();
            Categories.Add(new AppCategoryCardModel(
                group.Key,
                group.Key,
                items.Count,
                items.Count(a => a.IsManaged),
                items.Count(a => a.Status is CardStatus.Detected or CardStatus.Linked or CardStatus.Drift or CardStatus.Broken),
                items.Count(a => a.IsSuggested)));
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
        EcosystemGroups.Clear();
        AlternativeApps.Clear();
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
        EcosystemGroups.Clear();
        AlternativeApps.Clear();
        OnPropertyChanged(nameof(HasEcosystem));
        OnPropertyChanged(nameof(HasAlternatives));
    }

    private void BuildEcosystemGroups(AppCardModel card)
    {
        EcosystemGroups.Clear();

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

        if (ecosystemApps.Count == 0)
        {
            OnPropertyChanged(nameof(HasEcosystem));
            return;
        }

        var groups = ecosystemApps
            .GroupBy(a => a.SubCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => GetSubCategoryPriority(card.BroadCategory, g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var sorted = group
                .OrderBy(a => TierSortOrder(a.Tier))
                .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();

            EcosystemGroups.Add(new EcosystemGroup(group.Key, sorted));
        }

        OnPropertyChanged(nameof(HasEcosystem));
    }

    private void BuildAlternativeApps()
    {
        AlternativeApps.Clear();

        if (Detail is null || Detail.Alternatives.IsDefaultOrEmpty)
        {
            OnPropertyChanged(nameof(HasAlternatives));
            return;
        }

        foreach (var alt in Detail.Alternatives)
        {
            if (_allAppsByIdIncludingChildren.TryGetValue(alt.Id, out var altCard))
                AlternativeApps.Add(altCard);
        }

        OnPropertyChanged(nameof(HasAlternatives));
    }

    public IEnumerable<AppCategoryGroup> GetCategorySubGroups(string broadCategory)
    {
        return _allApps
            .Where(a => string.Equals(a.BroadCategory, broadCategory, StringComparison.OrdinalIgnoreCase))
            .Where(a => a.MatchesSearch(SearchText))
            .GroupBy(a => a.SubCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => GetSubCategoryPriority(broadCategory, g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AppCategoryGroup(
                g.Key,
                new ObservableCollection<AppCardModel>(
                    g.OrderBy(a => TierSortOrder(a.Tier))
                     .ThenBy(a => StatusSortOrder(a.Status))
                     .ThenByDescending(a => a.GitHubStars ?? 0)
                     .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase))));
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

    private int GetBroadCategoryPriority(string broadCategory)
    {
        if (_broadCategoryProfiles.TryGetValue(broadCategory, out var profiles) &&
            profiles.Any(p => _activeProfiles.Contains(p)))
            return 0;

        return 1;
    }

    private static int GetSubCategoryPriority(string broadCategory, string subCategory)
    {
        if (!_subcategoryOrder.TryGetValue(broadCategory, out var order))
            return int.MaxValue;

        var index = Array.FindIndex(order, s => string.Equals(s, subCategory, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : int.MaxValue;
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

public sealed record EcosystemGroup(string Name, ImmutableArray<AppCardModel> Apps);
