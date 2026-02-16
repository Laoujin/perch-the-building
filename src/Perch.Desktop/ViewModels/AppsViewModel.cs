using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Core.Packages;

namespace Perch.Desktop.ViewModels;

public sealed partial class AppsViewModel : ViewModelBase
{
    private readonly IAppScanService _appScanService;
    private readonly ISettingsProvider _settingsProvider;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasConfigRepo = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<AppEntryViewModel> AllApps { get; } = [];
    public ObservableCollection<AppEntryViewModel> ManagedApps { get; } = [];
    public ObservableCollection<AppEntryViewModel> InstalledApps { get; } = [];
    public ObservableCollection<AppEntryViewModel> DefinedApps { get; } = [];

    public AppsViewModel(IAppScanService appScanService, ISettingsProvider settingsProvider)
    {
        _appScanService = appScanService;
        _settingsProvider = settingsProvider;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ConfigRepoPath))
        {
            HasConfigRepo = false;
            return;
        }

        HasConfigRepo = true;
        IsLoading = true;
        AllApps.Clear();

        try
        {
            var result = await _appScanService.ScanAsync(settings.ConfigRepoPath, cancellationToken);

            foreach (var entry in result.Entries.OrderBy(e => e.Name))
            {
                AllApps.Add(new AppEntryViewModel(entry));
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ApplyFilter();
        IsLoading = false;
    }

    private void ApplyFilter()
    {
        ManagedApps.Clear();
        InstalledApps.Clear();
        DefinedApps.Clear();

        foreach (var app in AllApps)
        {
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !app.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                continue;

            switch (app.Category)
            {
                case AppCategory.Managed:
                    ManagedApps.Add(app);
                    break;
                case AppCategory.InstalledNoModule:
                    InstalledApps.Add(app);
                    break;
                case AppCategory.DefinedNotInstalled:
                    DefinedApps.Add(app);
                    break;
            }
        }
    }
}

public sealed class AppEntryViewModel
{
    public string Name { get; }
    public AppCategory Category { get; }
    public string? Source { get; }

    public string CategoryDisplay => Category switch
    {
        AppCategory.Managed => "Managed",
        AppCategory.InstalledNoModule => "No Module",
        AppCategory.DefinedNotInstalled => "Not Installed",
        _ => "Unknown",
    };

    public string StatusRibbon => Category switch
    {
        AppCategory.Managed => "Linked",
        AppCategory.InstalledNoModule => "Not linked",
        AppCategory.DefinedNotInstalled => "Not installed",
        _ => "",
    };

    public AppEntryViewModel(AppEntry entry)
    {
        Name = entry.Name;
        Category = entry.Category;
        Source = entry.Source?.ToString();
    }
}
