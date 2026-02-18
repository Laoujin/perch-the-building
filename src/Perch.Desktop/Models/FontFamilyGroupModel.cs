using System.Collections.ObjectModel;
using System.Collections.Specialized;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Perch.Desktop.Models;

public partial class FontFamilyGroupModel : ObservableObject
{
    private static readonly string[] FallbackSpecimens =
    [
        "Sphinx of black quartz, judge my vow",
        "Pack my box with five dozen liquor jugs",
        "How vexingly quick daft zebras jump",
        "Amazingly few discotheques provide jukeboxes",
        "The five boxing wizards jump quickly",
        "Jackdaws love my big sphinx of quartz",
        "Grumpy wizards make toxic brew for the evil queen and jack",
        "A wizard's job is to vex chumps quickly in fog",
    ];

    public string FamilyName { get; }
    public string SpecimenPhrase { get; }
    public ObservableCollection<FontCardModel> Fonts { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    private bool _suppressPropagation;

    public FontFamilyGroupModel(string familyName, IEnumerable<FontCardModel> fonts)
    {
        FamilyName = familyName;
        Fonts = new ObservableCollection<FontCardModel>(fonts);
        SpecimenPhrase = Fonts.FirstOrDefault(f => f.PreviewText is not null)?.PreviewText
            ?? FallbackSpecimens[Math.Abs(familyName.GetHashCode()) % FallbackSpecimens.Length];

        foreach (var font in Fonts)
            font.PropertyChanged += OnChildPropertyChanged;

        Fonts.CollectionChanged += OnFontsCollectionChanged;
        SyncFromChildren();
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (_suppressPropagation)
            return;

        _suppressPropagation = true;
        foreach (var font in Fonts)
            font.IsSelected = value;
        _suppressPropagation = false;
    }

    private void OnChildPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FontCardModel.IsSelected))
            SyncFromChildren();
    }

    private void SyncFromChildren()
    {
        if (_suppressPropagation)
            return;

        _suppressPropagation = true;
        IsSelected = Fonts.Count > 0 && Fonts.All(f => f.IsSelected);
        _suppressPropagation = false;
    }

    private void OnFontsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (FontCardModel font in e.OldItems)
                font.PropertyChanged -= OnChildPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (FontCardModel font in e.NewItems)
                font.PropertyChanged += OnChildPropertyChanged;
        }

        SyncFromChildren();
    }

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return FamilyName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Fonts.Any(f => f.MatchesSearch(query));
    }
}
