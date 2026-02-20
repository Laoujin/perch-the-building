using System.Collections.Immutable;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Catalog;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels;

public sealed partial class LanguagesViewModel : GalleryViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly IAppDetailService _detailService;
    private readonly IPendingChangesService _pendingChanges;

    private ImmutableArray<EcosystemCardModel> _allEcosystems = [];

    [ObservableProperty]
    private int _syncedCount;

    [ObservableProperty]
    private int _driftedCount;

    [ObservableProperty]
    private int _detectedCount;

    [ObservableProperty]
    private EcosystemCardModel? _selectedEcosystem;

    [ObservableProperty]
    private AppCardModel? _selectedItem;

    [ObservableProperty]
    private AppDetail? _itemDetail;

    [ObservableProperty]
    private bool _isLoadingDetail;

    public override bool ShowGrid => SelectedEcosystem is null;
    public override bool ShowDetail => SelectedEcosystem is not null;
    public bool ShowEcosystemDetail => SelectedEcosystem is not null && SelectedItem is null;
    public bool ShowItemDetail => SelectedItem is not null;
    public bool HasAlternatives => AlternativeApps.Count > 0;

    public BulkObservableCollection<EcosystemCardModel> Ecosystems { get; } = [];
    public BulkObservableCollection<AppCategoryGroup> SubCategories { get; } = [];
    public BulkObservableCollection<AppCardModel> AlternativeApps { get; } = [];

    public LanguagesViewModel(
        IGalleryDetectionService detectionService,
        IAppDetailService detailService,
        IPendingChangesService pendingChanges)
    {
        _detectionService = detectionService;
        _detailService = detailService;
        _pendingChanges = pendingChanges;
    }

    partial void OnSelectedEcosystemChanged(EcosystemCardModel? value)
    {
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(ShowDetail));
        OnPropertyChanged(nameof(ShowEcosystemDetail));
    }

    partial void OnSelectedItemChanged(AppCardModel? value)
    {
        OnPropertyChanged(nameof(ShowEcosystemDetail));
        OnPropertyChanged(nameof(ShowItemDetail));
    }

    protected override void OnSearchTextUpdated() => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _detectionService.InvalidateCache();
            var allApps = await _detectionService.DetectAllAppsAsync(cancellationToken);
            BuildEcosystems(allApps);
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load languages: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildEcosystems(ImmutableArray<AppCardModel> allApps)
    {
        var byId = allApps.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);

        var reverseRequires = new Dictionary<string, List<AppCardModel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in allApps)
        {
            if (app.CatalogEntry.Requires.IsDefaultOrEmpty)
                continue;

            foreach (var reqId in app.CatalogEntry.Requires)
            {
                if (!reverseRequires.TryGetValue(reqId, out var list))
                {
                    list = [];
                    reverseRequires[reqId] = list;
                }
                list.Add(app);
            }
        }

        var runtimes = allApps
            .Where(a => a.CatalogEntry.Kind == CatalogKind.Runtime
                && !a.CatalogEntry.Hidden)
            .ToList();

        var ecosystems = runtimes.Select(runtime =>
        {
            var ecosystemName = runtime.Name
                .Replace(" SDK", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" Runtime", "", StringComparison.OrdinalIgnoreCase);

            var items = new List<AppCardModel> { runtime };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { runtime.Id };

            if (!runtime.CatalogEntry.Suggests.IsDefaultOrEmpty)
            {
                foreach (var suggestId in runtime.CatalogEntry.Suggests)
                {
                    if (seen.Add(suggestId) && byId.TryGetValue(suggestId, out var suggested))
                        items.Add(suggested);
                }
            }

            if (reverseRequires.TryGetValue(runtime.Id, out var dependents))
            {
                foreach (var dep in dependents)
                {
                    if (seen.Add(dep.Id))
                        items.Add(dep);
                }
            }

            var eco = new EcosystemCardModel(
                runtime.Id,
                ecosystemName,
                runtime.Description,
                runtime.LogoUrl,
                runtime.Website,
                runtime.Docs,
                runtime.GitHub,
                runtime.License);

            eco.GitHubStars = runtime.GitHubStars;
            eco.Items = [.. items];
            eco.UpdateCounts();
            return eco;
        }).ToImmutableArray();

        _allEcosystems = ecosystems;
    }

    private void ApplyFilter()
    {
        var query = SearchText;
        Ecosystems.ReplaceAll(_allEcosystems.Where(e => e.MatchesSearch(query)));
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        SyncedCount = _allEcosystems.Sum(e => e.SyncedCount);
        DriftedCount = _allEcosystems.Sum(e => e.DriftedCount);
        DetectedCount = _allEcosystems.Sum(e => e.DetectedCount);
    }

    [RelayCommand]
    private void SelectEcosystem(EcosystemCardModel ecosystem)
    {
        SelectedEcosystem = ecosystem;
        SelectedItem = null;
        ItemDetail = null;
        AlternativeApps.ReplaceAll([]);
        OnPropertyChanged(nameof(HasAlternatives));
        RebuildSubCategories();
    }

    private void RebuildSubCategories()
    {
        if (SelectedEcosystem is null)
        {
            SubCategories.ReplaceAll([]);
            return;
        }

        var groups = SelectedEcosystem.Items
            .GroupBy(a => a.SubCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => SubCategorySortOrder(g.Key))
            .Select(g => new AppCategoryGroup(
                g.Key,
                new System.Collections.ObjectModel.ObservableCollection<AppCardModel>(g)));

        SubCategories.ReplaceAll(groups);
    }

    [RelayCommand]
    private async Task SelectItemAsync(AppCardModel card, CancellationToken cancellationToken)
    {
        SelectedItem = card;
        ItemDetail = null;
        AlternativeApps.ReplaceAll([]);
        OnPropertyChanged(nameof(HasAlternatives));
        IsLoadingDetail = true;

        try
        {
            ItemDetail = await _detailService.LoadDetailAsync(card, cancellationToken);
            OnPropertyChanged(nameof(HasAlternatives));
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
        SelectedEcosystem = null;
        SelectedItem = null;
        ItemDetail = null;
        SubCategories.ReplaceAll([]);
        AlternativeApps.ReplaceAll([]);
        OnPropertyChanged(nameof(HasAlternatives));
    }

    [RelayCommand]
    private void BackToEcosystem()
    {
        SelectedItem = null;
        ItemDetail = null;
        AlternativeApps.ReplaceAll([]);
        OnPropertyChanged(nameof(HasAlternatives));
    }

    [RelayCommand]
    private void ToggleApp(AppCardModel app)
    {
        if (!app.CanToggle)
            return;

        if (_pendingChanges.Contains(app.Id, PendingChangeKind.LinkApp))
            _pendingChanges.Remove(app.Id, PendingChangeKind.LinkApp);
        else if (_pendingChanges.Contains(app.Id, PendingChangeKind.UnlinkApp))
            _pendingChanges.Remove(app.Id, PendingChangeKind.UnlinkApp);
        else if (app.IsManaged)
            _pendingChanges.Add(new UnlinkAppChange(app));
        else
            _pendingChanges.Add(new LinkAppChange(app));
    }

    private static int StatusSortOrder(CardStatus status) => status switch
    {
        CardStatus.Drift or CardStatus.Broken => 0,
        CardStatus.Detected => 1,
        CardStatus.Linked => 2,
        _ => 3,
    };

    private static int SubCategorySortOrder(string subCategory) => subCategory switch
    {
        _ when subCategory.Equals("IDEs", StringComparison.OrdinalIgnoreCase) => 0,
        _ when subCategory.Equals("Runtimes", StringComparison.OrdinalIgnoreCase) => 1,
        _ when subCategory.Equals("Languages", StringComparison.OrdinalIgnoreCase) => 1,
        _ when subCategory.Equals("Decompilers", StringComparison.OrdinalIgnoreCase) => 2,
        _ when subCategory.Equals("Profilers", StringComparison.OrdinalIgnoreCase) => 3,
        _ when subCategory.Equals("IDE Extensions", StringComparison.OrdinalIgnoreCase) => 4,
        _ when subCategory.Equals("CLI Tools", StringComparison.OrdinalIgnoreCase) => 5,
        _ => 10,
    };
}
