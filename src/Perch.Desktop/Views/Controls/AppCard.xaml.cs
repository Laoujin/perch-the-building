using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

using Perch.Desktop.Models;

namespace Perch.Desktop.Views.Controls;

public partial class AppCard : UserControl
{
    public static readonly DependencyProperty DisplayLabelProperty =
        DependencyProperty.Register(nameof(DisplayLabel), typeof(string), typeof(AppCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CategoryProperty =
        DependencyProperty.Register(nameof(Category), typeof(string), typeof(AppCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(CardStatus), typeof(AppCard),
            new PropertyMetadata(CardStatus.NotInstalled));

    public static readonly DependencyProperty WebsiteProperty =
        DependencyProperty.Register(nameof(Website), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty GitHubProperty =
        DependencyProperty.Register(nameof(GitHub), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CanLinkProperty =
        DependencyProperty.Register(nameof(CanLink), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanUnlinkProperty =
        DependencyProperty.Register(nameof(CanUnlink), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanFixProperty =
        DependencyProperty.Register(nameof(CanFix), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly RoutedEvent LinkRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(LinkRequested), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(AppCard));

    public static readonly RoutedEvent UnlinkRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(UnlinkRequested), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(AppCard));

    public static readonly RoutedEvent FixRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(FixRequested), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(AppCard));

    public string DisplayLabel
    {
        get => (string)GetValue(DisplayLabelProperty);
        set => SetValue(DisplayLabelProperty, value);
    }

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Category
    {
        get => (string)GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    public CardStatus Status
    {
        get => (CardStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string? Website
    {
        get => (string?)GetValue(WebsiteProperty);
        set => SetValue(WebsiteProperty, value);
    }

    public string? GitHub
    {
        get => (string?)GetValue(GitHubProperty);
        set => SetValue(GitHubProperty, value);
    }

    public bool CanLink
    {
        get => (bool)GetValue(CanLinkProperty);
        set => SetValue(CanLinkProperty, value);
    }

    public bool CanUnlink
    {
        get => (bool)GetValue(CanUnlinkProperty);
        set => SetValue(CanUnlinkProperty, value);
    }

    public bool CanFix
    {
        get => (bool)GetValue(CanFixProperty);
        set => SetValue(CanFixProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public event RoutedEventHandler LinkRequested
    {
        add => AddHandler(LinkRequestedEvent, value);
        remove => RemoveHandler(LinkRequestedEvent, value);
    }

    public event RoutedEventHandler UnlinkRequested
    {
        add => AddHandler(UnlinkRequestedEvent, value);
        remove => RemoveHandler(UnlinkRequestedEvent, value);
    }

    public event RoutedEventHandler FixRequested
    {
        add => AddHandler(FixRequestedEvent, value);
        remove => RemoveHandler(FixRequestedEvent, value);
    }

    public AppCard()
    {
        InitializeComponent();
    }

    private void OnLinkClick(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(LinkRequestedEvent, this));

    private void OnUnlinkClick(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(UnlinkRequestedEvent, this));

    private void OnFixClick(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(FixRequestedEvent, this));

    private void OnWebsiteClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(Website))
            Process.Start(new ProcessStartInfo(Website) { UseShellExecute = true });
    }

    private void OnGitHubClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(GitHub))
            Process.Start(new ProcessStartInfo(GitHub) { UseShellExecute = true });
    }
}
