using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Catalog;
using Perch.Core.Packages;
using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class AppCatalogStepViewModel : WizardStepViewModel
{
    private readonly ICatalogService _catalogService;
    private readonly WizardState _state;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All Apps";

    [ObservableProperty]
    private AppFilter _selectedFilter = AppFilter.All;

    public ObservableCollection<AppItemViewModel> Apps { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["All Apps"];

    public override string Title => "Apps & Fonts";
    public override int StepNumber => 5;

    public AppCatalogStepViewModel(ICatalogService catalogService, WizardState state)
    {
        _catalogService = catalogService;
        _state = state;
    }

    public async Task LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        var installedNames = _state.ScanResult?.InstalledPackages
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var installedFontNames = _state.ScanResult?.InstalledFonts
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var apps = await _catalogService.GetAllAppsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var app in apps)
        {
            AddAppItem(app, IsAppInstalled(app, installedNames));
        }

        var fonts = await _catalogService.GetAllFontsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var font in fonts)
        {
            var entry = FontToCatalogEntry(font);
            bool isInstalled = installedFontNames.Any(n =>
                n.Contains(font.Name, StringComparison.OrdinalIgnoreCase));
            AddAppItem(entry, isInstalled);
        }

        IsLoading = false;
    }

    private void AddAppItem(CatalogEntry entry, bool isInstalled)
    {
        var item = new AppItemViewModel(entry, isInstalled);
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppItemViewModel.WillInstall) or nameof(AppItemViewModel.WillAdoptConfig))
            {
                UpdateState();
            }
        };
        Apps.Add(item);

        if (!Categories.Contains(entry.Category))
        {
            Categories.Add(entry.Category);
        }
    }

    private void UpdateState()
    {
        _state.AppsToInstall = Apps
            .Where(a => a.WillInstall)
            .Select(a => a.Entry.Id)
            .ToImmutableHashSet();

        _state.ConfigsToAdopt = Apps
            .Where(a => a.WillAdoptConfig)
            .Select(a => a.Entry.Id)
            .ToImmutableHashSet();
    }

    private static CatalogEntry FontToCatalogEntry(FontCatalogEntry font) =>
        new(
            Id: font.Id,
            Name: font.Name,
            DisplayName: font.Name,
            Category: "Fonts",
            Tags: font.Tags,
            Description: font.Description,
            Logo: font.Logo,
            Links: null,
            Install: font.Install,
            Config: null,
            Extensions: null);

    private static bool IsAppInstalled(CatalogEntry app, HashSet<string> installedNames)
    {
        if (installedNames.Contains(app.Name))
        {
            return true;
        }

        if (app.Install?.Winget != null && installedNames.Contains(app.Install.Winget))
        {
            return true;
        }

        return app.Install?.Choco != null && installedNames.Contains(app.Install.Choco);
    }
}

public enum AppFilter
{
    All,
    Installed,
    NotInstalled,
    HasConfig,
}

public sealed partial class AppItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _willInstall;

    [ObservableProperty]
    private bool _willAdoptConfig;

    public CatalogEntry Entry { get; }
    public bool IsInstalled { get; }
    public bool HasConfig => Entry.Config != null;

    public AppItemViewModel(CatalogEntry entry, bool isInstalled)
    {
        Entry = entry;
        IsInstalled = isInstalled;
    }
}
