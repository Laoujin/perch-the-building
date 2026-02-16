using System.Windows;
using System.Windows.Controls;

namespace Perch.Desktop.Views.Controls;

public partial class DriftHeroBanner : UserControl
{
    public static readonly DependencyProperty LinkedCountProperty =
        DependencyProperty.Register(nameof(LinkedCount), typeof(int), typeof(DriftHeroBanner),
            new PropertyMetadata(0, OnCountChanged));

    public static readonly DependencyProperty AttentionCountProperty =
        DependencyProperty.Register(nameof(AttentionCount), typeof(int), typeof(DriftHeroBanner),
            new PropertyMetadata(0, OnCountChanged));

    public static readonly DependencyProperty BrokenCountProperty =
        DependencyProperty.Register(nameof(BrokenCount), typeof(int), typeof(DriftHeroBanner),
            new PropertyMetadata(0, OnCountChanged));

    public static readonly DependencyProperty HealthPercentProperty =
        DependencyProperty.Register(nameof(HealthPercent), typeof(int), typeof(DriftHeroBanner),
            new PropertyMetadata(100));

    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(nameof(StatusMessage), typeof(string), typeof(DriftHeroBanner),
            new PropertyMetadata("Everything looks good"));

    public int LinkedCount
    {
        get => (int)GetValue(LinkedCountProperty);
        set => SetValue(LinkedCountProperty, value);
    }

    public int AttentionCount
    {
        get => (int)GetValue(AttentionCountProperty);
        set => SetValue(AttentionCountProperty, value);
    }

    public int BrokenCount
    {
        get => (int)GetValue(BrokenCountProperty);
        set => SetValue(BrokenCountProperty, value);
    }

    public int HealthPercent
    {
        get => (int)GetValue(HealthPercentProperty);
        set => SetValue(HealthPercentProperty, value);
    }

    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    public DriftHeroBanner()
    {
        InitializeComponent();
    }

    private static void OnCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DriftHeroBanner banner)
            banner.UpdateStatusMessage();
    }

    private void UpdateStatusMessage()
    {
        var total = LinkedCount + AttentionCount + BrokenCount;
        if (total == 0)
        {
            StatusMessage = "No configs detected yet";
            HealthPercent = 0;
            return;
        }

        HealthPercent = total > 0 ? (int)(LinkedCount * 100.0 / total) : 100;

        if (BrokenCount > 0)
            StatusMessage = $"{BrokenCount} broken config{(BrokenCount > 1 ? "s" : "")} need fixing";
        else if (AttentionCount > 0)
            StatusMessage = $"{AttentionCount} item{(AttentionCount > 1 ? "s" : "")} need attention";
        else
            StatusMessage = "Everything looks good";
    }
}
