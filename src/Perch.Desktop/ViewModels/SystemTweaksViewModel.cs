using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels;

public sealed partial class SystemTweaksViewModel : ViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _fontSearchText = string.Empty;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private string? _selectedCategory;

    public ObservableCollection<TweakCardModel> Tweaks { get; } = [];
    public ObservableCollection<TweakCardModel> FilteredTweaks { get; } = [];
    public ObservableCollection<FontCardModel> InstalledFonts { get; } = [];
    public ObservableCollection<FontCardModel> NerdFonts { get; } = [];
    public ObservableCollection<FontFamilyGroupModel> FilteredInstalledFontGroups { get; } = [];
    public ObservableCollection<FontCardModel> FilteredNerdFonts { get; } = [];
    public ObservableCollection<TweakCategoryCardModel> Categories { get; } = [];

    private List<FontFamilyGroupModel> _allInstalledFontGroups = [];

    public bool ShowCategories => SelectedCategory is null;
    public bool ShowDetail => SelectedCategory is not null;

    public SystemTweaksViewModel(IGalleryDetectionService detectionService)
    {
        _detectionService = detectionService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFontSearchTextChanged(string value) => ApplyFontFilter();

    partial void OnSelectedCategoryChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowCategories));
        OnPropertyChanged(nameof(ShowDetail));
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            var profiles = new HashSet<UserProfile> { UserProfile.Developer, UserProfile.PowerUser };
            var tweaksTask = _detectionService.DetectTweaksAsync(profiles, cancellationToken);
            var fontsTask = _detectionService.DetectFontsAsync(cancellationToken);

            await Task.WhenAll(tweaksTask, fontsTask);

            Tweaks.Clear();
            foreach (var tweak in tweaksTask.Result)
                Tweaks.Add(tweak);

            var fontResult = fontsTask.Result;
            InstalledFonts.Clear();
            NerdFonts.Clear();
            foreach (var f in fontResult.InstalledFonts) InstalledFonts.Add(f);
            foreach (var f in fontResult.NerdFonts) NerdFonts.Add(f);

            BuildFontGroups();
            RebuildCategories();
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            // Detection failure is non-fatal -- show whatever loaded
            RebuildCategories();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RebuildCategories()
    {
        Categories.Clear();

        var groups = Tweaks.GroupBy(t => t.Category, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var items = group.ToList();
            Categories.Add(new TweakCategoryCardModel(
                group.Key,
                group.Key,
                description: null,
                items.Count,
                items.Count(t => t.IsSelected)));
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

    [RelayCommand]
    private void SelectCategory(string category)
    {
        FilteredTweaks.Clear();
        if (!string.Equals(category, "Fonts", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var tweak in Tweaks.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)))
                FilteredTweaks.Add(tweak);
        }

        SelectedCategory = category;
    }

    [RelayCommand]
    private void BackToCategories()
    {
        SelectedCategory = null;
        RebuildCategories();
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

        UpdateSelectedCount();
    }

    private void ApplyFilter()
    {
        UpdateSelectedCount();
    }

    public void UpdateSelectedCount()
    {
        SelectedCount = Tweaks.Count(t => t.IsSelected)
            + InstalledFonts.Count(f => f.IsSelected)
            + NerdFonts.Count(f => f.IsSelected);
    }

    public void ClearSelection()
    {
        foreach (var tweak in Tweaks)
            tweak.IsSelected = false;
        foreach (var font in InstalledFonts)
            font.IsSelected = false;
        foreach (var font in NerdFonts)
            font.IsSelected = false;
        SelectedCount = 0;
        RebuildCategories();
    }
}
