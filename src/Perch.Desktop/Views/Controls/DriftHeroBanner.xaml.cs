using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Perch.Desktop.Views.Controls;

public enum HeroBannerState
{
    Empty,
    Healthy,
    Attention,
}

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

    public static readonly DependencyProperty BannerStateProperty =
        DependencyProperty.Register(nameof(BannerState), typeof(HeroBannerState), typeof(DriftHeroBanner),
            new PropertyMetadata(HeroBannerState.Empty, OnBannerStateChanged));

    public static readonly DependencyProperty LinkedAppsCountProperty =
        DependencyProperty.Register(nameof(LinkedAppsCount), typeof(int), typeof(DriftHeroBanner),
            new PropertyMetadata(0));

    public static readonly DependencyProperty LinkedDotfilesCountProperty =
        DependencyProperty.Register(nameof(LinkedDotfilesCount), typeof(int), typeof(DriftHeroBanner),
            new PropertyMetadata(0));

    public static readonly DependencyProperty LinkedTweaksCountProperty =
        DependencyProperty.Register(nameof(LinkedTweaksCount), typeof(int), typeof(DriftHeroBanner),
            new PropertyMetadata(0));

    public static readonly RoutedEvent LaunchWizardRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(LaunchWizardRequested), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(DriftHeroBanner));

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

    public HeroBannerState BannerState
    {
        get => (HeroBannerState)GetValue(BannerStateProperty);
        set => SetValue(BannerStateProperty, value);
    }

    public int LinkedAppsCount
    {
        get => (int)GetValue(LinkedAppsCountProperty);
        set => SetValue(LinkedAppsCountProperty, value);
    }

    public int LinkedDotfilesCount
    {
        get => (int)GetValue(LinkedDotfilesCountProperty);
        set => SetValue(LinkedDotfilesCountProperty, value);
    }

    public int LinkedTweaksCount
    {
        get => (int)GetValue(LinkedTweaksCountProperty);
        set => SetValue(LinkedTweaksCountProperty, value);
    }

    public event RoutedEventHandler LaunchWizardRequested
    {
        add => AddHandler(LaunchWizardRequestedEvent, value);
        remove => RemoveHandler(LaunchWizardRequestedEvent, value);
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

    private static void OnBannerStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DriftHeroBanner banner)
            banner.ApplyBannerState((HeroBannerState)e.NewValue);
    }

    private void UpdateStatusMessage()
    {
        var total = LinkedCount + AttentionCount + BrokenCount;
        if (total == 0)
        {
            StatusMessage = "No configs detected yet";
            HealthPercent = 100;
            BannerState = HeroBannerState.Empty;
            return;
        }

        HealthPercent = total > 0 ? (int)(LinkedCount * 100.0 / total) : 100;

        if (AttentionCount + BrokenCount > 0)
        {
            var issues = AttentionCount + BrokenCount;
            StatusMessage = $"{issues} item{(issues > 1 ? "s" : "")} need attention";
            BannerState = HeroBannerState.Attention;
        }
        else
        {
            StatusMessage = "Everything looks good";
            BannerState = HeroBannerState.Healthy;
        }
    }

    private void ApplyBannerState(HeroBannerState state)
    {
        var imageUri = state switch
        {
            HeroBannerState.Healthy => "/Assets/wizard-success.png",
            HeroBannerState.Attention => "/Assets/wizard-error.png",
            _ => "/Assets/wizard-empty.png",
        };

        RootBorder.Background = new ImageBrush
        {
            ImageSource = new BitmapImage(new Uri(imageUri, UriKind.Relative)),
            Stretch = Stretch.UniformToFill,
            Opacity = 0.15,
        };

        if (state == HeroBannerState.Empty)
        {
            EmptyPanel.Visibility = Visibility.Visible;
            StatusPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            EmptyPanel.Visibility = Visibility.Collapsed;
            StatusPanel.Visibility = Visibility.Visible;
        }
    }

    private void OnWizardButtonClick(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(LaunchWizardRequestedEvent, this));
    }

    private void OnEmptyPanelClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(LaunchWizardRequestedEvent, this));
    }
}
