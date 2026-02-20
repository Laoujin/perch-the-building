using System.Collections.Immutable;
using System.Diagnostics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Core.Scanner;
using Perch.Core.Startup;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels;

public sealed partial class SystemTweaksViewModel : GalleryViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IStartupService _startupService;
    private readonly ITweakService _tweakService;
    private readonly ICertificateScanner _certificateScanner;
    private readonly IPendingChangesService _pendingChanges;

    [ObservableProperty]
    private string _fontSearchText = string.Empty;

    [ObservableProperty]
    private string _startupSearchText = string.Empty;

    [ObservableProperty]
    private string _certificateSearchText = string.Empty;

    [ObservableProperty]
    private string? _activeCertificateExpiryFilter;

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private string? _activeProfileFilter;

    private HashSet<UserProfile> _userProfiles = [UserProfile.Developer, UserProfile.PowerUser];

    public BulkObservableCollection<TweakCardModel> Tweaks { get; } = [];
    public BulkObservableCollection<FontCardModel> InstalledFonts { get; } = [];
    public BulkObservableCollection<FontCardModel> NerdFonts { get; } = [];
    public BulkObservableCollection<FontFamilyGroupModel> FilteredInstalledFontGroups { get; } = [];
    public BulkObservableCollection<FontCardModel> FilteredNerdFonts { get; } = [];
    public BulkObservableCollection<TweakCategoryCardModel> Categories { get; } = [];
    public BulkObservableCollection<TweakCategoryCardModel> SubCategories { get; } = [];
    public BulkObservableCollection<StartupCardModel> StartupItems { get; } = [];
    public BulkObservableCollection<StartupCardModel> FilteredStartupItems { get; } = [];
    public BulkObservableCollection<string> AvailableProfileFilters { get; } = [];
    public BulkObservableCollection<CertificateCardModel> CertificateItems { get; } = [];
    public BulkObservableCollection<CertificateStoreGroupModel> FilteredCertificateGroups { get; } = [];
    public BulkObservableCollection<string> AvailableCertificateExpiryFilters { get; } = [];

    private List<FontFamilyGroupModel> _allInstalledFontGroups = [];
    private List<CertificateStoreGroupModel> _allCertificateGroups = [];

    public override bool ShowGrid => SelectedCategory is null;
    public override bool ShowDetail => SelectedCategory is not null;
    public bool ShowSubCategories => SelectedCategory == "System Tweaks";
    public bool ShowStartupDetail => string.Equals(SelectedCategory, "Startup", StringComparison.OrdinalIgnoreCase);
    public bool ShowFontDetail => string.Equals(SelectedCategory, "Fonts", StringComparison.OrdinalIgnoreCase);
    public bool ShowCertificateDetail => string.Equals(SelectedCategory, "Certificates", StringComparison.OrdinalIgnoreCase);

    public SystemTweaksViewModel(
        IGalleryDetectionService detectionService,
        ISettingsProvider settingsProvider,
        IStartupService startupService,
        ITweakService tweakService,
        ICertificateScanner certificateScanner,
        IPendingChangesService pendingChanges)
    {
        _detectionService = detectionService;
        _settingsProvider = settingsProvider;
        _startupService = startupService;
        _tweakService = tweakService;
        _certificateScanner = certificateScanner;
        _pendingChanges = pendingChanges;
    }

    protected override void OnSearchTextUpdated() => RebuildSubCategories();
    partial void OnFontSearchTextChanged(string value) => ApplyFontFilter();
    partial void OnStartupSearchTextChanged(string value) => ApplyStartupFilter();
    partial void OnCertificateSearchTextChanged(string value) => ApplyCertificateFilter();

    partial void OnSelectedCategoryChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(ShowDetail));
        OnPropertyChanged(nameof(ShowSubCategories));
        OnPropertyChanged(nameof(ShowStartupDetail));
        OnPropertyChanged(nameof(ShowFontDetail));
        OnPropertyChanged(nameof(ShowCertificateDetail));
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _userProfiles = await LoadProfilesAsync(_settingsProvider, cancellationToken);
            var tweaksTask = _detectionService.DetectTweaksAsync(_userProfiles, cancellationToken);
            var fontsTask = _detectionService.DetectFontsAsync(cancellationToken);
            var startupTask = _startupService.GetAllAsync(cancellationToken);
            var certsTask = _certificateScanner.ScanAsync(cancellationToken);

            await Task.WhenAll(tweaksTask, fontsTask, startupTask, certsTask);

            var tweakResult = tweaksTask.Result;
            foreach (var tweak in tweakResult.Tweaks)
                tweak.IsSuggested = tweak.MatchesProfile(_userProfiles);
            Tweaks.ReplaceAll(tweakResult.Tweaks);

            if (!tweakResult.Errors.IsEmpty)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Failed to detect {tweakResult.Errors.Length} tweak(s):");
                sb.AppendLine();
                foreach (var err in tweakResult.Errors)
                {
                    sb.AppendLine($"  Tweak: {err.TweakName} ({err.TweakId})");
                    if (err.RegistryKey is not null)
                        sb.AppendLine($"  Key:   {err.RegistryKey}");
                    if (err.SourceFile is not null)
                        sb.AppendLine($"  File:  {err.SourceFile}");
                    sb.AppendLine($"  Error: {err.ErrorMessage}");
                    sb.AppendLine();
                }
                ErrorMessage = sb.ToString().TrimEnd();
            }

            var fontResult = fontsTask.Result;
            UnsubscribeFontChanges();
            foreach (var f in fontResult.InstalledFonts)
                f.PropertyChanged += OnFontPropertyChanged;
            foreach (var f in fontResult.NerdFonts)
                f.PropertyChanged += OnFontPropertyChanged;
            InstalledFonts.ReplaceAll(fontResult.InstalledFonts);
            NerdFonts.ReplaceAll(fontResult.NerdFonts);

            StartupItems.ReplaceAll(startupTask.Result.Select(e => new StartupCardModel(e)));
            CertificateItems.ReplaceAll(certsTask.Result.Select(c => new CertificateCardModel(c)));

            BuildProfileFilters();
            BuildFontGroups();
            BuildCertificateGroups();
            RebuildCategories();
            ApplyStartupFilter();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load tweaks: {ex.Message}";
            RebuildCategories();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildProfileFilters()
    {
        var profileNames = Tweaks
            .SelectMany(t => t.Profiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        AvailableProfileFilters.ReplaceAll(
            new[] { "All", "Suggested" }.Concat(profileNames));
    }

    private void RebuildCategories()
    {
        var categories = new List<TweakCategoryCardModel>();

        if (StartupItems.Count > 0)
        {
            categories.Add(new TweakCategoryCardModel(
                "Startup",
                "Startup",
                "Programs that run at login",
                StartupItems.Count,
                0));
        }

        if (Tweaks.Count > 0)
        {
            categories.Add(new TweakCategoryCardModel(
                "System Tweaks",
                "System Tweaks",
                "Registry tweaks grouped by area",
                Tweaks.Count,
                Tweaks.Count(t => t.IsSelected)));
        }

        var fontCount = InstalledFonts.Count + NerdFonts.Count;
        if (fontCount > 0)
        {
            categories.Add(new TweakCategoryCardModel(
                "Fonts",
                "Fonts",
                "Detected & gallery nerd fonts",
                fontCount,
                InstalledFonts.Count(f => f.IsSelected) + NerdFonts.Count(f => f.IsSelected)));
        }

        if (CertificateItems.Count > 0)
        {
            categories.Add(new TweakCategoryCardModel(
                "Certificates",
                "Certificates",
                "CurrentUser certificate stores",
                CertificateItems.Count,
                0));
        }

        Categories.ReplaceAll(categories);
    }

    private void RebuildSubCategories()
    {
        var subCategories = GetProfileFilteredTweaks()
            .Where(t => t.MatchesSearch(SearchText))
            .GroupBy(t => t.BroadCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var items = g.ToList();
                var subGroups = items
                    .GroupBy(t => t.SubCategory, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(sg => sg.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(sg => new TweakSubCategoryGroup(sg.Key, sg.ToImmutableArray()))
                    .ToList();

                return new TweakCategoryCardModel(
                    g.Key,
                    g.Key,
                    description: null,
                    items.Count,
                    items.Count(t => t.IsSelected),
                    subGroups: subGroups);
            });

        SubCategories.ReplaceAll(subCategories);
    }

    private IEnumerable<TweakCardModel> GetProfileFilteredTweaks()
    {
        if (ActiveProfileFilter is null or "All")
            return Tweaks;

        if (ActiveProfileFilter == "Suggested")
            return Tweaks.Where(t => t.IsSuggested);

        return Tweaks.Where(t =>
            t.Profiles.Contains(ActiveProfileFilter, StringComparer.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        if (string.Equals(category, "Startup", StringComparison.OrdinalIgnoreCase))
            ApplyStartupFilter();

        if (string.Equals(category, "Certificates", StringComparison.OrdinalIgnoreCase))
        {
            ActiveCertificateExpiryFilter = "All";
            ApplyCertificateFilter();
        }

        if (string.Equals(category, "System Tweaks", StringComparison.OrdinalIgnoreCase))
        {
            ActiveProfileFilter = _userProfiles.Count > 0 ? "Suggested" : "All";
            RebuildSubCategories();
        }

        SelectedCategory = category;
    }

    [RelayCommand]
    private void SetProfileFilter(string filter)
    {
        ActiveProfileFilter = filter;
        RebuildSubCategories();
    }

    [RelayCommand]
    private void SetCertificateExpiryFilter(string filter)
    {
        ActiveCertificateExpiryFilter = filter;
        ApplyCertificateFilter();
    }

    [RelayCommand]
    private void BackToCategories()
    {
        SelectedCategory = null;
        RebuildCategories();
    }

    [RelayCommand]
    private void ToggleStartupEnabled(StartupCardModel card)
    {
        var newState = !card.IsEnabled;
        _pendingChanges.Add(new ToggleStartupChange(card, newState));
    }

    [RelayCommand]
    private async Task RemoveStartupItemAsync(StartupCardModel card)
    {
        await _startupService.RemoveAsync(card.Entry);
        StartupItems.Remove(card);
        FilteredStartupItems.Remove(card);
    }

    [RelayCommand]
    private void ApplyTweak(TweakCardModel card)
    {
        _pendingChanges.Remove(card.Id, PendingChangeKind.RevertTweak);
        _pendingChanges.Add(new ApplyTweakChange(card));
    }

    [RelayCommand]
    private void RevertTweak(TweakCardModel card)
    {
        _pendingChanges.Remove(card.Id, PendingChangeKind.ApplyTweak);
        _pendingChanges.Remove(card.Id, PendingChangeKind.RevertTweakToCaptured);
        _pendingChanges.Add(new RevertTweakChange(card));
    }

    [RelayCommand]
    private void RevertTweakToCaptured(TweakCardModel card)
    {
        _pendingChanges.Remove(card.Id, PendingChangeKind.ApplyTweak);
        _pendingChanges.Remove(card.Id, PendingChangeKind.RevertTweak);
        _pendingChanges.Add(new RevertTweakToCapturedChange(card));
    }

    [RelayCommand]
    private static void OpenRegedit(TweakCardModel card)
    {
        if (card.Registry.IsDefaultOrEmpty)
            return;

        RegeditLauncher.OpenAt(card.Registry[0].Key);
    }

    [RelayCommand]
    private async Task RemoveCertificateAsync(CertificateCardModel card, CancellationToken cancellationToken)
    {
        await _certificateScanner.RemoveAsync(card.Certificate, cancellationToken);
        CertificateItems.Remove(card);

        foreach (var group in _allCertificateGroups)
            group.Certificates.Remove(card);

        _allCertificateGroups.RemoveAll(g => g.Certificates.Count == 0);
        ApplyCertificateFilter();
    }

    [RelayCommand]
    private static void OpenCertificateManager()
    {
        Process.Start(new ProcessStartInfo("certmgr.msc") { UseShellExecute = true });
    }

    private void RefreshTweakCard(TweakCardModel card)
    {
        var detection = _tweakService.Detect(card.CatalogEntry);
        card.DetectedEntries = detection.Entries;
        card.AppliedCount = detection.Entries.Count(e => e.IsApplied);
        card.Status = detection.Status switch
        {
            TweakStatus.Applied => CardStatus.Detected,
            TweakStatus.Partial => CardStatus.Drifted,
            _ => CardStatus.Unmanaged,
        };
    }

    [RelayCommand]
    private void TrackAllNewStartupItems()
    {
        foreach (var item in FilteredStartupItems)
        {
            if (!item.IsTracked)
                item.IsTracked = true;
        }
    }

    [RelayCommand]
    private void TrackAllInstalledFonts()
    {
        bool allSelected = FilteredInstalledFontGroups.All(g => g.IsSelected);
        foreach (var group in FilteredInstalledFontGroups)
            group.IsSelected = !allSelected;
    }

    private void BuildFontGroups()
    {
        DisposeFontGroups();
        _allInstalledFontGroups = InstalledFonts
            .GroupBy(f => f.FamilyName ?? f.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FontFamilyGroupModel(g.Key, g))
            .ToList();

        ApplyFontFilter();
    }

    private void ApplyFontFilter()
    {
        var query = FontSearchText;

        FilteredInstalledFontGroups.ReplaceAll(
            _allInstalledFontGroups.Where(g => g.MatchesSearch(query)));

        FilteredNerdFonts.ReplaceAll(
            NerdFonts
                .Where(f => f.MatchesSearch(query))
                .OrderBy(f => f.Status == CardStatus.Detected ? 0 : 1)
                .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase));
    }

    private void ApplyStartupFilter()
    {
        FilteredStartupItems.ReplaceAll(
            StartupItems.Where(item => item.MatchesSearch(StartupSearchText)));
    }

    private void BuildCertificateGroups()
    {
        _allCertificateGroups = CertificateItems
            .GroupBy(c => c.Certificate.Store)
            .OrderBy(g => g.Key)
            .Select(g => new CertificateStoreGroupModel(g.Key,
                g.OrderBy(c => c.SubjectDisplayName, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var statuses = CertificateItems.Select(c => c.ExpiryStatus).ToHashSet();
        var filters = new List<string> { "All" };
        if (statuses.Contains(CertificateExpiryStatus.Valid))
            filters.Add("Valid");
        if (statuses.Contains(CertificateExpiryStatus.ExpiringSoon))
            filters.Add("Expiring Soon");
        if (statuses.Contains(CertificateExpiryStatus.Expired))
            filters.Add("Expired");
        AvailableCertificateExpiryFilters.ReplaceAll(filters);

        ApplyCertificateFilter();
    }

    private void ApplyCertificateFilter()
    {
        var query = CertificateSearchText;
        var expiryFilter = ActiveCertificateExpiryFilter;

        var filtered = new List<CertificateStoreGroupModel>();
        foreach (var group in _allCertificateGroups)
        {
            IEnumerable<CertificateCardModel> certs = group.Certificates;

            if (!string.IsNullOrWhiteSpace(query))
                certs = certs.Where(c => c.MatchesSearch(query));

            if (expiryFilter is not null and not "All")
            {
                var status = expiryFilter switch
                {
                    "Valid" => CertificateExpiryStatus.Valid,
                    "Expiring Soon" => CertificateExpiryStatus.ExpiringSoon,
                    "Expired" => CertificateExpiryStatus.Expired,
                    _ => (CertificateExpiryStatus?)null,
                };
                if (status is not null)
                    certs = certs.Where(c => c.ExpiryStatus == status);
            }

            var filteredGroup = new CertificateStoreGroupModel(group.Store, certs);
            if (filteredGroup.Certificates.Count > 0)
                filtered.Add(filteredGroup);
        }

        FilteredCertificateGroups.ReplaceAll(filtered);
    }

    private void OnFontPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FontCardModel.IsSelected) || sender is not FontCardModel font)
            return;

        if (font.IsSelected)
            _pendingChanges.Add(new OnboardFontChange(font));
        else
            _pendingChanges.Remove(font.Id, PendingChangeKind.OnboardFont);
    }

    private void UnsubscribeFontChanges()
    {
        foreach (var f in InstalledFonts)
            f.PropertyChanged -= OnFontPropertyChanged;
        foreach (var f in NerdFonts)
            f.PropertyChanged -= OnFontPropertyChanged;
    }

    private void DisposeFontGroups()
    {
        foreach (var group in _allInstalledFontGroups)
            group.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnsubscribeFontChanges();
            DisposeFontGroups();
        }
        base.Dispose(disposing);
    }
}
