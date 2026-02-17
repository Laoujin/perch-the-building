using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Symlinks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels;

public sealed partial class AppsViewModel : ViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly IAppLinkService _appLinkService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<AppCategoryGroup> GroupedApps { get; } = [];

    private ImmutableArray<AppCardModel> _allApps = [];

    public AppsViewModel(IGalleryDetectionService detectionService, IAppLinkService appLinkService)
    {
        _detectionService = detectionService;
        _appLinkService = appLinkService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            _allApps = await _detectionService.DetectAllAppsAsync(cancellationToken);
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        if (_allApps.IsDefaultOrEmpty)
        {
            GroupedApps.Clear();
            return;
        }

        var filtered = _allApps.Where(a => a.MatchesSearch(SearchText));

        var groups = filtered
            .GroupBy(a => a.Category)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AppCategoryGroup(
                g.Key,
                new ObservableCollection<AppCardModel>(
                    g.OrderBy(a => StatusSortOrder(a.Status))
                     .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase))))
            .ToList();

        GroupedApps.Clear();
        foreach (var group in groups)
            GroupedApps.Add(group);
    }

    private static int StatusSortOrder(CardStatus status) => status switch
    {
        CardStatus.Linked => 0,
        CardStatus.Drift => 1,
        CardStatus.Broken => 2,
        CardStatus.Detected => 3,
        CardStatus.NotInstalled => 4,
        _ => 5,
    };

    [RelayCommand]
    private async Task LinkAppAsync(AppCardModel app)
    {
        var results = await _appLinkService.LinkAppAsync(app.CatalogEntry);
        if (results.All(r => r.Level != Core.Deploy.ResultLevel.Error))
            app.Status = CardStatus.Linked;
    }

    [RelayCommand]
    private async Task UnlinkAppAsync(AppCardModel app)
    {
        var results = await _appLinkService.UnlinkAppAsync(app.CatalogEntry);
        if (results.All(r => r.Level != Core.Deploy.ResultLevel.Error))
            app.Status = CardStatus.Detected;
    }

    [RelayCommand]
    private async Task FixAppAsync(AppCardModel app)
    {
        var results = await _appLinkService.FixAppLinksAsync(app.CatalogEntry);
        if (results.All(r => r.Level != Core.Deploy.ResultLevel.Error))
            app.Status = CardStatus.Linked;
    }
}

public sealed class AppCategoryGroup
{
    public string Category { get; }
    public ObservableCollection<AppCardModel> Apps { get; }

    public AppCategoryGroup(string category, ObservableCollection<AppCardModel> apps)
    {
        Category = category;
        Apps = apps;
    }
}
