using CommunityToolkit.Mvvm.ComponentModel;

using Wpf.Ui.Controls;

namespace Perch.Desktop.Models;

public partial class TweakCategoryCardModel : ObservableObject
{
    public string Category { get; }
    public string DisplayName { get; }
    public string? Description { get; }
    public SymbolRegular IconSymbol { get; }

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _isExpanded;

    public TweakCategoryCardModel(
        string category,
        string displayName,
        string? description,
        int itemCount,
        int selectedCount)
    {
        Category = category;
        DisplayName = displayName;
        Description = description;
        IconSymbol = GetIcon(category);
        ItemCount = itemCount;
        SelectedCount = selectedCount;
    }

    private static SymbolRegular GetIcon(string category) => category switch
    {
        "Startup" => SymbolRegular.Play24,
        "Explorer" => SymbolRegular.FolderOpen24,
        "Privacy" => SymbolRegular.Shield24,
        "Fonts" => SymbolRegular.TextFont24,
        "Taskbar" => SymbolRegular.AppsList24,
        "Performance" => SymbolRegular.TopSpeed24,
        "Input" => SymbolRegular.Keyboard24,
        "Appearance" => SymbolRegular.PaintBrush24,
        "Certificates" => SymbolRegular.ShieldLock24,
        _ => SymbolRegular.Wrench24,
    };
}
