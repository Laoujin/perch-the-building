using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Symlinks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

using Wpf.Ui;

namespace Perch.Desktop.ViewModels;

public sealed partial class AppsViewModel : ViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly IAppLinkService _appLinkService;
    private readonly IAppDetailService _detailService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISnackbarService _snackbarService;

    private ImmutableArray<AppCardModel> _allYourApps = [];
    private ImmutableArray<AppCardModel> _allSuggested = [];
    private ImmutableArray<AppCardModel> _allOther = [];

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
        IAppLinkService appLinkService,
        IAppDetailService detailService,
        ISettingsProvider settingsProvider,
        ISnackbarService snackbarService)
    {
        _detectionService = detectionService;
        _appLinkService = appLinkService;
        _detailService = detailService;
        _settingsProvider = settingsProvider;
        _snackbarService = snackbarService;
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

            _allYourApps = result.YourApps;
            _allSuggested = result.Suggested;
            _allOther = result.OtherApps;

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
    private async Task ToggleAppAsync(AppCardModel app)
    {
        if (!app.CanToggle)
            return;

        IReadOnlyList<DeployResult> results;
        string action;

        if (app.Status is CardStatus.Broken or CardStatus.Drift)
        {
            results = await _appLinkService.FixAppLinksAsync(app.CatalogEntry);
            action = "Fixed";
        }
        else if (app.IsManaged)
        {
            results = await _appLinkService.UnlinkAppAsync(app.CatalogEntry);
            action = "Unlinked";
        }
        else
        {
            results = await _appLinkService.LinkAppAsync(app.CatalogEntry);
            action = "Linked";
        }

        var hasErrors = results.Any(r => r.Level == ResultLevel.Error);

        if (!hasErrors)
        {
            app.Status = action == "Unlinked" ? CardStatus.Detected : CardStatus.Linked;
            _snackbarService.Show("Success", $"{action} {app.DisplayLabel}",
                Wpf.Ui.Controls.ControlAppearance.Success,
                null, TimeSpan.FromSeconds(3));
        }
        else
        {
            var errorMsg = results.First(r => r.Level == ResultLevel.Error).Message;
            _snackbarService.Show("Error", $"Failed to {action.ToLowerInvariant()} {app.DisplayLabel}: {errorMsg}",
                Wpf.Ui.Controls.ControlAppearance.Danger,
                null, TimeSpan.FromSeconds(5));
        }

        UpdateSummary();
    }

    [RelayCommand]
    private void ToggleCategoryExpand(AppCategoryCardModel category)
    {
        category.IsExpanded = !category.IsExpanded;
    }

    public IEnumerable<AppCardModel> GetCategoryApps(string broadCategory)
    {
        return _allOther
            .Where(a => string.Equals(a.BroadCategory, broadCategory, StringComparison.OrdinalIgnoreCase))
            .Where(a => a.MatchesSearch(SearchText))
            .OrderBy(a => StatusSortOrder(a.Status))
            .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase);
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
