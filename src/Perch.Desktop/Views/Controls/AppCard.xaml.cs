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

    public static readonly DependencyProperty IsManagedProperty =
        DependencyProperty.Register(nameof(IsManaged), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanToggleProperty =
        DependencyProperty.Register(nameof(CanToggle), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly RoutedEvent ToggleChangedEvent =
        EventManager.RegisterRoutedEvent(nameof(ToggleChanged), RoutingStrategy.Bubble,
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

    public bool IsManaged
    {
        get => (bool)GetValue(IsManagedProperty);
        set => SetValue(IsManagedProperty, value);
    }

    public bool CanToggle
    {
        get => (bool)GetValue(CanToggleProperty);
        set => SetValue(CanToggleProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public event RoutedEventHandler ToggleChanged
    {
        add => AddHandler(ToggleChangedEvent, value);
        remove => RemoveHandler(ToggleChangedEvent, value);
    }

    public AppCard()
    {
        InitializeComponent();
    }

    private void OnToggleChanged(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(ToggleChangedEvent, this));

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
