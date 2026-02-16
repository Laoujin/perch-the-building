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
        var (text, fg, bg) = status switch
        {
            CardStatus.Linked => ("Linked", "#34D399", "rgba(52,211,153,0.15)"),
            CardStatus.Detected => ("Detected", "#F59E0B", "rgba(245,158,11,0.15)"),
            CardStatus.Selected => ("Selected", "#10B981", "rgba(16,185,129,0.15)"),
            CardStatus.Drift => ("Drift", "#F59E0B", "rgba(245,158,11,0.15)"),
            CardStatus.Broken => ("Broken", "#EF4444", "rgba(239,68,68,0.15)"),
            CardStatus.Error => ("Error", "#EF4444", "rgba(239,68,68,0.15)"),
            _ => ("Not installed", "#3B82F6", "rgba(59,130,246,0.15)"),
        };

        RibbonText.Text = text;
        RibbonText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
        RibbonBorder.Background = ParseRgba(bg);
    }

    private static SolidColorBrush ParseRgba(string rgba)
    {
        // Parse rgba(r,g,b,a) format
        var inner = rgba.Replace("rgba(", "").Replace(")", "");
        var parts = inner.Split(',');
        return new SolidColorBrush(Color.FromArgb(
            (byte)(double.Parse(parts[3].Trim()) * 255),
            byte.Parse(parts[0].Trim()),
            byte.Parse(parts[1].Trim()),
            byte.Parse(parts[2].Trim())));
    }
}
