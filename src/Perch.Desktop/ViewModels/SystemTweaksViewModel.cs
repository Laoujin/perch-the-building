using System.Collections.ObjectModel;
using System.Diagnostics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
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

    private const int MinSubCategorySize = 3;

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
    private string? _selectedCategory;

    [ObservableProperty]
    private string? _selectedSubCategory;

    [ObservableProperty]
    private string? _activeProfileFilter;

    private HashSet<UserProfile> _userProfiles = [UserProfile.Developer, UserProfile.PowerUser];

    public ObservableCollection<TweakCardModel> Tweaks { get; } = [];
    public ObservableCollection<TweakCardModel> FilteredTweaks { get; } = [];
    public ObservableCollection<FontCardModel> InstalledFonts { get; } = [];
    public ObservableCollection<FontCardModel> NerdFonts { get; } = [];
    public ObservableCollection<FontFamilyGroupModel> FilteredInstalledFontGroups { get; } = [];
    public ObservableCollection<FontCardModel> FilteredNerdFonts { get; } = [];
    public ObservableCollection<TweakCategoryCardModel> Categories { get; } = [];
    public ObservableCollection<TweakCategoryCardModel> SubCategories { get; } = [];
    public ObservableCollection<StartupCardModel> StartupItems { get; } = [];
    public ObservableCollection<StartupCardModel> FilteredStartupItems { get; } = [];
    public ObservableCollection<string> AvailableProfileFilters { get; } = [];

    private List<FontFamilyGroupModel> _allInstalledFontGroups = [];

    public bool ShowCategories => SelectedCategory is null;
    public bool ShowDetail => SelectedCategory is not null;
    public bool ShowSubCategories => SelectedCategory == "System Tweaks" && SelectedSubCategory is null;
    public bool ShowTweakCards => SelectedCategory == "System Tweaks" && SelectedSubCategory is not null;

    public SystemTweaksViewModel(
        IGalleryDetectionService detectionService,
        ISettingsProvider settingsProvider,
        IStartupService startupService,
        ITweakService tweakService)
    {
        _detectionService = detectionService;
        _settingsProvider = settingsProvider;
        _startupService = startupService;
        _tweakService = tweakService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFontSearchTextChanged(string value) => ApplyFontFilter();
    partial void OnStartupSearchTextChanged(string value) => ApplyStartupFilter();

    partial void OnSelectedCategoryChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowCategories));
        OnPropertyChanged(nameof(ShowDetail));
        OnPropertyChanged(nameof(ShowSubCategories));
        OnPropertyChanged(nameof(ShowTweakCards));
    }

    partial void OnSelectedSubCategoryChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowSubCategories));
        OnPropertyChanged(nameof(ShowTweakCards));
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

            await Task.WhenAll(tweaksTask, fontsTask, startupTask);

            Tweaks.Clear();
            foreach (var tweak in tweaksTask.Result)
            {
                tweak.IsSuggested = tweak.MatchesProfile(_userProfiles);
                Tweaks.Add(tweak);
            }

            var fontResult = fontsTask.Result;
            InstalledFonts.Clear();
            NerdFonts.Clear();
            foreach (var f in fontResult.InstalledFonts) InstalledFonts.Add(f);
            foreach (var f in fontResult.NerdFonts) NerdFonts.Add(f);

            StartupItems.Clear();
            foreach (var entry in startupTask.Result)
                StartupItems.Add(new StartupCardModel(entry));

            BuildProfileFilters();
            BuildFontGroups();
            RebuildCategories();
            ApplyFilter();
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
    }

    private void RebuildSubCategories()
    {
        SubCategories.Clear();

        var tweaksForFilter = GetProfileFilteredTweaks();
        var groups = tweaksForFilter
            .GroupBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var smallGroups = groups.Where(g => g.Count() < MinSubCategorySize).ToList();
        var normalGroups = groups.Where(g => g.Count() >= MinSubCategorySize).ToList();

        foreach (var group in normalGroups)
        {
            SubCategories.Add(new TweakCategoryCardModel(
                group.Key,
                group.Key,
                description: null,
                group.Count(),
                group.Count(t => t.IsSelected)));
        }

        if (smallGroups.Count > 0)
        {
            var otherCount = smallGroups.Sum(g => g.Count());
            var otherSelected = smallGroups.Sum(g => g.Count(t => t.IsSelected));
            SubCategories.Add(new TweakCategoryCardModel(
                "Other",
                "Other",
                description: null,
                otherCount,
                otherSelected));
        }
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
        SelectedSubCategory = null;
        FilteredTweaks.Clear();

        if (string.Equals(category, "Startup", StringComparison.OrdinalIgnoreCase))
            ApplyStartupFilter();

        if (string.Equals(category, "System Tweaks", StringComparison.OrdinalIgnoreCase))
        {
            ActiveProfileFilter = _userProfiles.Count > 0 ? "Suggested" : "All";
            RebuildSubCategories();
        }

        SelectedCategory = category;
    }

    [RelayCommand]
    private void SelectSubCategory(string subCategory)
    {
        FilteredTweaks.Clear();

        var tweaksForFilter = GetProfileFilteredTweaks();

        if (string.Equals(subCategory, "Other", StringComparison.OrdinalIgnoreCase))
        {
            var normalCategoryNames = SubCategories
                .Where(c => !string.Equals(c.Category, "Other", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Category)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var tweak in tweaksForFilter.Where(t => !normalCategoryNames.Contains(t.Category)))
                FilteredTweaks.Add(tweak);
        }
        else
        {
            foreach (var tweak in tweaksForFilter.Where(t => string.Equals(t.Category, subCategory, StringComparison.OrdinalIgnoreCase)))
                FilteredTweaks.Add(tweak);
        }

        SelectedSubCategory = subCategory;
    }

    [RelayCommand]
    private void SetProfileFilter(string filter)
    {
        ActiveProfileFilter = filter;
        RebuildSubCategories();

        if (SelectedSubCategory is not null)
            SelectSubCategory(SelectedSubCategory);
    }

    [RelayCommand]
    private void BackToCategories()
    {
        SelectedCategory = null;
        SelectedSubCategory = null;
        RebuildCategories();
    }

    [RelayCommand]
    private void BackToSubCategories()
    {
        SelectedSubCategory = null;
        RebuildSubCategories();
    }

    [RelayCommand]
    private async Task ToggleStartupEnabledAsync(StartupCardModel card)
    {
        var newState = !card.IsEnabled;
        await _startupService.SetEnabledAsync(card.Entry, newState);
        card.IsEnabled = newState;
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
        var result = _tweakService.Apply(card.CatalogEntry);
        RefreshTweakCard(card);
    }

    [RelayCommand]
    private void RevertTweak(TweakCardModel card)
    {
        var result = _tweakService.Revert(card.CatalogEntry);
        RefreshTweakCard(card);
    }

    [RelayCommand]
    private static void OpenRegedit(TweakCardModel card)
    {
        if (card.Registry.IsDefaultOrEmpty)
            return;

        var key = card.Registry[0].Key;
        Process.Start("regedit", $"/m \"{key}\"");
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
        foreach (var group in FilteredInstalledFontGroups)
        {
            if (!group.IsSelected)
                group.IsSelected = true;
        }
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
        foreach (var font in NerdFonts)
        {
            if (font.MatchesSearch(query))
                FilteredNerdFonts.Add(font);
        }
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

    private void ApplyFilter()
    {
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
