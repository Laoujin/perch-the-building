using CommunityToolkit.Mvvm.ComponentModel;

using Wpf.Ui.Controls;

namespace Perch.Desktop.Models;

public partial class AppCategoryCardModel : ObservableObject
{
    public string BroadCategory { get; }
    public string DisplayName { get; }
    public SymbolRegular IconSymbol { get; }

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _detectedCount;

    [ObservableProperty]
    private int _suggestedCount;

    [ObservableProperty]
    private bool _isExpanded;

    public AppCategoryCardModel(
        string broadCategory,
        string displayName,
        int itemCount,
        int selectedCount,
        int detectedCount = 0,
        int suggestedCount = 0)
    {
        BroadCategory = broadCategory;
        DisplayName = displayName;
        IconSymbol = GetIcon(broadCategory);
        ItemCount = itemCount;
        SelectedCount = selectedCount;
        DetectedCount = detectedCount;
        SuggestedCount = suggestedCount;
    }

    private static SymbolRegular GetIcon(string category) => category switch
    {
        "Development" => SymbolRegular.Code24,
        "Browsers" => SymbolRegular.Globe24,
        "System" => SymbolRegular.Desktop24,
        "Communication" => SymbolRegular.Chat24,
        "Media" => SymbolRegular.MusicNote224,
        "Gaming" => SymbolRegular.Games24,
        "Utilities" => SymbolRegular.Toolbox24,
        _ => SymbolRegular.Apps24,
    };
}
