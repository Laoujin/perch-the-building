using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Perch.Desktop.Models;

namespace Perch.Desktop.Views.Controls;

public partial class StatusRibbon : UserControl
{
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(CardStatus), typeof(StatusRibbon),
            new PropertyMetadata(CardStatus.Unmanaged, OnStatusChanged));

    public CardStatus Status
    {
        get => (CardStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public StatusRibbon()
    {
        InitializeComponent();
        UpdateVisual(CardStatus.Unmanaged);
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusRibbon ribbon)
            ribbon.UpdateVisual((CardStatus)e.NewValue);
    }

    private void UpdateVisual(CardStatus status)
    {
        if (status == CardStatus.Unmanaged)
        {
            RibbonBorder.Visibility = Visibility.Collapsed;
            return;
        }

        RibbonBorder.Visibility = Visibility.Visible;

        var (text, bg) = status switch
        {
            CardStatus.Detected => ("Detected", "#3B82F6"),
            CardStatus.PendingAdd => ("Pending", "#047857"),
            CardStatus.PendingRemove => ("Pending", "#DC2626"),
            CardStatus.Synced => ("Synced", "#34D399"),
            CardStatus.Drifted => ("Drifted", "#F59E0B"),
            _ => ("Unknown", "#6B7280"),
        };

        RibbonText.Text = text;
        RibbonText.Foreground = new SolidColorBrush(Colors.White);
        RibbonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)!);
    }
}
