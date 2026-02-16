using System.Windows;
using System.Windows.Controls;

namespace Perch.Desktop.Views.Controls;

public partial class DeployBar : UserControl
{
    public static readonly DependencyProperty SelectedCountProperty =
        DependencyProperty.Register(nameof(SelectedCount), typeof(int), typeof(DeployBar),
            new PropertyMetadata(0, OnSelectedCountChanged));

    public static readonly DependencyProperty IsDeployingProperty =
        DependencyProperty.Register(nameof(IsDeploying), typeof(bool), typeof(DeployBar),
            new PropertyMetadata(false));

    public static readonly RoutedEvent DeployRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(DeployRequested), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(DeployBar));

    public static readonly RoutedEvent ClearRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(ClearRequested), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(DeployBar));

    public int SelectedCount
    {
        get => (int)GetValue(SelectedCountProperty);
        set => SetValue(SelectedCountProperty, value);
    }

    public bool IsDeploying
    {
        get => (bool)GetValue(IsDeployingProperty);
        set => SetValue(IsDeployingProperty, value);
    }

    public event RoutedEventHandler DeployRequested
    {
        add => AddHandler(DeployRequestedEvent, value);
        remove => RemoveHandler(DeployRequestedEvent, value);
    }

    public event RoutedEventHandler ClearRequested
    {
        add => AddHandler(ClearRequestedEvent, value);
        remove => RemoveHandler(ClearRequestedEvent, value);
    }

    public DeployBar()
    {
        InitializeComponent();
    }

    private void OnDeployClick(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(DeployRequestedEvent, this));
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(ClearRequestedEvent, this));
    }

    private static void OnSelectedCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DeployBar bar)
            bar.Visibility = (int)e.NewValue > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
