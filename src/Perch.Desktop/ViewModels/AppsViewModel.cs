using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Startup;
using Perch.Core.Symlinks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels;

public sealed partial class AppsViewModel : ViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly IAppLinkService _appLinkService;
    private readonly IAppDetailService _detailService;
    private readonly IStartupService _startupService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _selectedAppCategory;

    [ObservableProperty]
    private AppCardModel? _selectedApp;

    [ObservableProperty]
    private AppDetail? _appDetail;

    [ObservableProperty]
    private bool _isLoadingAppDetail;

    [ObservableProperty]
    private bool _isInStartup;

    public ObservableCollection<AppCategoryCardModel> AppCategories { get; } = [];
    public ObservableCollection<AppCategoryGroup> FilteredCategoryApps { get; } = [];

    public bool ShowAppCategories => SelectedAppCategory is null && SelectedApp is null;
    public bool ShowAppDetail => SelectedAppCategory is not null && SelectedApp is null;
    public bool ShowAppConfigDetail => SelectedApp is not null;

    public bool HasModule => AppDetail?.OwningModule is not null;
    public bool HasNoModule => AppDetail is not null && AppDetail.OwningModule is null;
    private ImmutableArray<AppCardModel> _allApps = [];

    public AppsViewModel(
        IGalleryDetectionService detectionService,
        IAppLinkService appLinkService,
        IAppDetailService detailService,
        IStartupService startupService)
    {
        _detectionService = detectionService;
        _appLinkService = appLinkService;
        _detailService = detailService;
        _startupService = startupService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedAppCategoryChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowAppCategories));
        OnPropertyChanged(nameof(ShowAppDetail));
        OnPropertyChanged(nameof(ShowAppConfigDetail));
    }

    partial void OnSelectedAppChanged(AppCardModel? value)
    {
        OnPropertyChanged(nameof(ShowAppCategories));
        OnPropertyChanged(nameof(ShowAppDetail));
        OnPropertyChanged(nameof(ShowAppConfigDetail));
    }

    partial void OnAppDetailChanged(AppDetail? value)
    {
        OnPropertyChanged(nameof(HasModule));
        OnPropertyChanged(nameof(HasNoModule));
    }

    [RelayCommand]
    private async Task ConfigureAppAsync(AppCardModel card, CancellationToken cancellationToken)
    {
        SelectedApp = card;
        AppDetail = null;
        IsLoadingAppDetail = true;
        IsInStartup = false;

        try
        {
            var detailTask = _detailService.LoadDetailAsync(card, cancellationToken);
            var startupTask = _startupService.GetAllAsync(cancellationToken);
            await Task.WhenAll(detailTask, startupTask);

            AppDetail = detailTask.Result;
            IsInStartup = startupTask.Result.Any(s =>
                s.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            IsLoadingAppDetail = false;
        }
    }

    [RelayCommand]
    private async Task ToggleAppStartupAsync()
    {
        if (SelectedApp is null)
            return;

        var startupEntries = await _startupService.GetAllAsync();
        var existing = startupEntries.FirstOrDefault(s =>
            s.Name.Equals(SelectedApp.Name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            await _startupService.RemoveAsync(existing);
            IsInStartup = false;
        }
        else
        {
            var command = SelectedApp.Install?.Winget ?? SelectedApp.Install?.Choco ?? SelectedApp.Name;
            await _startupService.AddAsync(SelectedApp.Name, command, StartupSource.RegistryCurrentUser);
            IsInStartup = true;
        }
    }

    [RelayCommand]
    private void BackToCategoryDetail()
    {
        SelectedApp = null;
        AppDetail = null;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _allApps = await _detectionService.DetectAllAppsAsync(cancellationToken);
            ApplyFilter();
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

    private void ApplyFilter()
    {
        if (_allApps.IsDefaultOrEmpty)
        {
            AppCategories.Clear();
            FilteredCategoryApps.Clear();
            return;
        }

        if (SelectedAppCategory is not null)
        {
            RebuildCategoryDetail(SelectedAppCategory);
        }

        RebuildCategories();
    }

    private void RebuildCategories()
    {
        AppCategories.Clear();

        var filtered = _allApps.Where(a => a.MatchesSearch(SearchText));

        var groups = filtered
            .GroupBy(a => a.BroadCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var items = group.ToList();
            AppCategories.Add(new AppCategoryCardModel(
                group.Key,
                group.Key,
                items.Count,
                items.Count(a => a.IsSelected)));
        }
    }

    [RelayCommand]
    private void SelectCategory(string broadCategory)
    {
        RebuildCategoryDetail(broadCategory);
        SelectedAppCategory = broadCategory;
    }

    [RelayCommand]
    private void BackToCategories()
    {
        SelectedAppCategory = null;
        RebuildCategories();
    }

    private void RebuildCategoryDetail(string broadCategory)
    {
        FilteredCategoryApps.Clear();

        var filtered = _allApps
            .Where(a => string.Equals(a.BroadCategory, broadCategory, StringComparison.OrdinalIgnoreCase))
            .Where(a => a.MatchesSearch(SearchText));

        var subGroups = filtered
            .GroupBy(a => a.SubCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in subGroups)
        {
            FilteredCategoryApps.Add(new AppCategoryGroup(
                group.Key,
                new ObservableCollection<AppCardModel>(
                    group.OrderBy(a => StatusSortOrder(a.Status))
                         .ThenBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase))));
        }
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
