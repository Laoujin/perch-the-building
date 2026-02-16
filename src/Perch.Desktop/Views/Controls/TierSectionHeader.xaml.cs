using System.Windows;
using System.Windows.Controls;

namespace Perch.Desktop.Views.Controls;

public partial class TierSectionHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(TierSectionHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemCountProperty =
        DependencyProperty.Register(nameof(ItemCount), typeof(int), typeof(TierSectionHeader),
            new PropertyMetadata(0));

    public static readonly DependencyProperty IsCollapsedProperty =
        DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(TierSectionHeader),
            new PropertyMetadata(false));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public int ItemCount
    {
        get => (int)GetValue(ItemCountProperty);
        set => SetValue(ItemCountProperty, value);
    }

    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public TierSectionHeader()
    {
        InitializeComponent();
    }

    private void OnCollapseToggle(object sender, RoutedEventArgs e)
    {
        IsCollapsed = !IsCollapsed;
    }
}
