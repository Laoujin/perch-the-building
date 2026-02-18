using System.Collections.Immutable;
using System.Collections.ObjectModel;
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

public sealed partial class SystemTweaksViewModel : ViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IStartupService _startupService;
    private readonly ITweakService _tweakService;
    private readonly ICertificateScanner _certificateScanner;
    private readonly IPendingChangesService _pendingChanges;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchText = string.Empty;

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

    public ObservableCollection<TweakCardModel> Tweaks { get; } = [];
    public ObservableCollection<FontCardModel> InstalledFonts { get; } = [];
    public ObservableCollection<FontCardModel> NerdFonts { get; } = [];
    public ObservableCollection<FontFamilyGroupModel> FilteredInstalledFontGroups { get; } = [];
    public ObservableCollection<FontCardModel> FilteredNerdFonts { get; } = [];
    public ObservableCollection<TweakCategoryCardModel> Categories { get; } = [];
    public ObservableCollection<TweakCategoryCardModel> SubCategories { get; } = [];
    public ObservableCollection<StartupCardModel> StartupItems { get; } = [];
    public ObservableCollection<StartupCardModel> FilteredStartupItems { get; } = [];
    public ObservableCollection<string> AvailableProfileFilters { get; } = [];
    public ObservableCollection<CertificateCardModel> CertificateItems { get; } = [];
    public ObservableCollection<CertificateStoreGroupModel> FilteredCertificateGroups { get; } = [];
    public ObservableCollection<string> AvailableCertificateExpiryFilters { get; } = [];

    private List<FontFamilyGroupModel> _allInstalledFontGroups = [];
    private List<CertificateStoreGroupModel> _allCertificateGroups = [];

    public bool ShowCategories => SelectedCategory is null;
    public bool ShowDetail => SelectedCategory is not null;
    public bool ShowSubCategories => SelectedCategory == "System Tweaks";

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

    partial void OnSearchTextChanged(string value) => RebuildSubCategories();
    partial void OnFontSearchTextChanged(string value) => ApplyFontFilter();
    partial void OnStartupSearchTextChanged(string value) => ApplyStartupFilter();
    partial void OnCertificateSearchTextChanged(string value) => ApplyCertificateFilter();

    partial void OnSelectedCategoryChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowCategories));
        OnPropertyChanged(nameof(ShowDetail));
        OnPropertyChanged(nameof(ShowSubCategories));
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _userProfiles = await LoadProfilesAsync(cancellationToken);
            var tweaksTask = _detectionService.DetectTweaksAsync(_userProfiles, cancellationToken);
            var fontsTask = _detectionService.DetectFontsAsync(cancellationToken);
            var startupTask = _startupService.GetAllAsync(cancellationToken);
            var certsTask = _certificateScanner.ScanAsync(cancellationToken);

            await Task.WhenAll(tweaksTask, fontsTask, startupTask, certsTask);

            var tweakResult = tweaksTask.Result;
            Tweaks.Clear();
            foreach (var tweak in tweakResult.Tweaks)
            {
                tweak.IsSuggested = tweak.MatchesProfile(_userProfiles);
                Tweaks.Add(tweak);
            }

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
            InstalledFonts.Clear();
            NerdFonts.Clear();
            foreach (var f in fontResult.InstalledFonts)
            {
                f.PropertyChanged += OnFontPropertyChanged;
                InstalledFonts.Add(f);
            }
            foreach (var f in fontResult.NerdFonts)
            {
                f.PropertyChanged += OnFontPropertyChanged;
                NerdFonts.Add(f);
            }

            StartupItems.Clear();
            foreach (var entry in startupTask.Result)
                StartupItems.Add(new StartupCardModel(entry));

            CertificateItems.Clear();
            foreach (var cert in certsTask.Result)
                CertificateItems.Add(new CertificateCardModel(cert));

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
        AvailableProfileFilters.Clear();
        AvailableProfileFilters.Add("All");
        AvailableProfileFilters.Add("Suggested");

        var profileNames = Tweaks
            .SelectMany(t => t.Profiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profileNames)
            AvailableProfileFilters.Add(profile);
    }

    private void RebuildCategories()
    {
        Categories.Clear();

        if (StartupItems.Count > 0)
        {
            Categories.Add(new TweakCategoryCardModel(
                "Startup",
                "Startup",
                "Programs that run at login",
                StartupItems.Count,
                0));
        }

        if (Tweaks.Count > 0)
        {
            Categories.Add(new TweakCategoryCardModel(
                "System Tweaks",
                "System Tweaks",
                "Registry tweaks grouped by area",
                Tweaks.Count,
                Tweaks.Count(t => t.IsSelected)));
        }

        var fontCount = InstalledFonts.Count + NerdFonts.Count;
        if (fontCount > 0)
        {
            Categories.Add(new TweakCategoryCardModel(
                "Fonts",
                "Fonts",
                "Detected & gallery nerd fonts",
                fontCount,
                InstalledFonts.Count(f => f.IsSelected) + NerdFonts.Count(f => f.IsSelected)));
        }

        if (CertificateItems.Count > 0)
        {
            Categories.Add(new TweakCategoryCardModel(
                "Certificates",
                "Certificates",
                "CurrentUser certificate stores",
                CertificateItems.Count,
                0));
        }
    }

    private void RebuildSubCategories()
    {
        SubCategories.Clear();

        var filtered = GetProfileFilteredTweaks()
            .Where(t => t.MatchesSearch(SearchText));

        var groups = filtered
            .GroupBy(t => t.BroadCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var items = group.ToList();
            SubCategories.Add(new TweakCategoryCardModel(
                group.Key,
                group.Key,
                description: null,
                items.Count,
                items.Count(t => t.IsSelected)));
        }
    }

    public IEnumerable<TweakSubCategoryGroup> GetCategorySubGroups(string broadCategory)
    {
        return GetProfileFilteredTweaks()
            .Where(t => t.MatchesSearch(SearchText))
            .Where(t => string.Equals(t.BroadCategory, broadCategory, StringComparison.OrdinalIgnoreCase))
            .GroupBy(t => t.SubCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TweakSubCategoryGroup(g.Key, g.ToImmutableArray()));
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
            TweakStatus.Partial => CardStatus.Drift,
            _ => CardStatus.NotInstalled,
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

        FilteredInstalledFontGroups.Clear();
        foreach (var group in _allInstalledFontGroups)
        {
            if (group.MatchesSearch(query))
                FilteredInstalledFontGroups.Add(group);
        }

        FilteredNerdFonts.Clear();
        var sortedNerdFonts = NerdFonts
            .Where(f => f.MatchesSearch(query))
            .OrderBy(f => f.Status == CardStatus.Detected ? 0 : 1)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var font in sortedNerdFonts)
            FilteredNerdFonts.Add(font);
    }

    private void ApplyStartupFilter()
    {
        FilteredStartupItems.Clear();
        foreach (var item in StartupItems)
        {
            if (item.MatchesSearch(StartupSearchText))
                FilteredStartupItems.Add(item);
        }
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
        AvailableCertificateExpiryFilters.Clear();
        AvailableCertificateExpiryFilters.Add("All");
        if (statuses.Contains(CertificateExpiryStatus.Valid))
            AvailableCertificateExpiryFilters.Add("Valid");
        if (statuses.Contains(CertificateExpiryStatus.ExpiringSoon))
            AvailableCertificateExpiryFilters.Add("Expiring Soon");
        if (statuses.Contains(CertificateExpiryStatus.Expired))
            AvailableCertificateExpiryFilters.Add("Expired");

        ApplyCertificateFilter();
    }

    private void ApplyCertificateFilter()
    {
        var query = CertificateSearchText;
        var expiryFilter = ActiveCertificateExpiryFilter;
        FilteredCertificateGroups.Clear();

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

            var filtered = new CertificateStoreGroupModel(group.Store, certs);
            if (filtered.Certificates.Count > 0)
                FilteredCertificateGroups.Add(filtered);
        }
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

public sealed record TweakSubCategoryGroup(string SubCategory, ImmutableArray<TweakCardModel> Tweaks);
