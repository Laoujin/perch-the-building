using System.Collections.ObjectModel;
using System.Collections.Specialized;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Perch.Desktop.Models;

public partial class FontFamilyGroupModel : ObservableObject
{
    public string FamilyName { get; }
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
