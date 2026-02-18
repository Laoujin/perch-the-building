using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Perch.Desktop.Models;

namespace Perch.Desktop.Views.Controls;

public partial class StatusRibbon : UserControl
{
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(CardStatus), typeof(StatusRibbon),
            new PropertyMetadata(CardStatus.NotInstalled, OnStatusChanged));

    public CardStatus Status
    {
        get => (CardStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public StatusRibbon()
    {
        InitializeComponent();
        UpdateVisual(CardStatus.NotInstalled);
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusRibbon ribbon)
            ribbon.UpdateVisual((CardStatus)e.NewValue);
    }

    private void UpdateVisual(CardStatus status)
    {
        var (text, bg) = status switch
        {
            CardStatus.Linked => ("Adjusted", "#34D399"),
            CardStatus.Detected => ("Detected", "#B45309"),
            CardStatus.Selected => ("Selected", "#047857"),
            CardStatus.Drift => ("Drifted", "#F59E0B"),
            CardStatus.Broken => ("Broken", "#DC2626"),
            CardStatus.Error => ("Error", "#DC2626"),
            _ => ("System Default", "#3B82F6"),
        };

        RibbonText.Text = text;
        RibbonText.Foreground = new SolidColorBrush(Colors.White);
        RibbonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)!);
    }
}
