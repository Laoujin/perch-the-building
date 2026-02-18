using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using Perch.Desktop.Models;

namespace Perch.Desktop.Views.Controls;

public partial class AppCard : UserControl
{
    public static readonly DependencyProperty DisplayLabelProperty =
        DependencyProperty.Register(nameof(DisplayLabel), typeof(string), typeof(AppCard),
            new PropertyMetadata(string.Empty, OnDisplayLabelChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CategoryProperty =
        DependencyProperty.Register(nameof(Category), typeof(string), typeof(AppCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(CardStatus), typeof(AppCard),
            new PropertyMetadata(CardStatus.NotInstalled));

    public static readonly DependencyProperty GitHubProperty =
        DependencyProperty.Register(nameof(GitHub), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty WebsiteProperty =
        DependencyProperty.Register(nameof(Website), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DocsProperty =
        DependencyProperty.Register(nameof(Docs), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LicenseProperty =
        DependencyProperty.Register(nameof(License), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsManagedProperty =
        DependencyProperty.Register(nameof(IsManaged), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanToggleProperty =
        DependencyProperty.Register(nameof(CanToggle), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty LogoUrlProperty =
        DependencyProperty.Register(nameof(LogoUrl), typeof(string), typeof(AppCard),
            new PropertyMetadata(null, OnLogoUrlChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsSuggestedProperty =
        DependencyProperty.Register(nameof(IsSuggested), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty KindBadgeProperty =
        DependencyProperty.Register(nameof(KindBadge), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty GitHubStarsFormattedProperty =
        DependencyProperty.Register(nameof(GitHubStarsFormatted), typeof(string), typeof(AppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsTopPickProperty =
        DependencyProperty.Register(nameof(IsTopPick), typeof(bool), typeof(AppCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty TagsProperty =
        DependencyProperty.Register(nameof(Tags), typeof(ImmutableArray<string>), typeof(AppCard),
            new PropertyMetadata(ImmutableArray<string>.Empty));

    public static readonly RoutedEvent ToggleChangedEvent =
        EventManager.RegisterRoutedEvent(nameof(ToggleChanged), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(AppCard));

    public static readonly RoutedEvent ConfigureClickedEvent =
        EventManager.RegisterRoutedEvent(nameof(ConfigureClicked), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(AppCard));

    public static readonly RoutedEvent TagClickedEvent =
        EventManager.RegisterRoutedEvent(nameof(TagClicked), RoutingStrategy.Bubble,
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

    public string? GitHub
    {
        get => (string?)GetValue(GitHubProperty);
        set => SetValue(GitHubProperty, value);
    }

    public string? Website
    {
        get => (string?)GetValue(WebsiteProperty);
        set => SetValue(WebsiteProperty, value);
    }

    public string? Docs
    {
        get => (string?)GetValue(DocsProperty);
        set => SetValue(DocsProperty, value);
    }

    public string? License
    {
        get => (string?)GetValue(LicenseProperty);
        set => SetValue(LicenseProperty, value);
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

    public string? LogoUrl
    {
        get => (string?)GetValue(LogoUrlProperty);
        set => SetValue(LogoUrlProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsSuggested
    {
        get => (bool)GetValue(IsSuggestedProperty);
        set => SetValue(IsSuggestedProperty, value);
    }

    public string? KindBadge
    {
        get => (string?)GetValue(KindBadgeProperty);
        set => SetValue(KindBadgeProperty, value);
    }

    public string? GitHubStarsFormatted
    {
        get => (string?)GetValue(GitHubStarsFormattedProperty);
        set => SetValue(GitHubStarsFormattedProperty, value);
    }

    public bool IsTopPick
    {
        get => (bool)GetValue(IsTopPickProperty);
        set => SetValue(IsTopPickProperty, value);
    }

    public ImmutableArray<string> Tags
    {
        get => (ImmutableArray<string>)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public event RoutedEventHandler ConfigureClicked
    {
        add => AddHandler(ConfigureClickedEvent, value);
        remove => RemoveHandler(ConfigureClickedEvent, value);
    }

    public event RoutedEventHandler ToggleChanged
    {
        add => AddHandler(ToggleChangedEvent, value);
        remove => RemoveHandler(ToggleChangedEvent, value);
    }

    public event RoutedEventHandler TagClicked
    {
        add => AddHandler(TagClickedEvent, value);
        remove => RemoveHandler(TagClickedEvent, value);
    }

    public AppCard()
    {
        InitializeComponent();
    }

    private void OnConfigureClick(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(ConfigureClickedEvent, this));

    private void OnToggleChanged(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(ToggleChangedEvent, this));

    private static void OnDisplayLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AppCard card && e.NewValue is string label && label.Length > 0)
            card.FallbackInitial.Text = label[..1].ToUpperInvariant();
    }

    private static void OnLogoUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not AppCard card)
            return;

        if (e.NewValue is not string url || string.IsNullOrEmpty(url))
        {
            card.LogoImage.Source = null;
            card.FallbackIcon.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(url, UriKind.Absolute);
            bitmap.DecodePixelWidth = 48;
            bitmap.EndInit();

            if (bitmap.IsDownloading)
            {
                bitmap.DownloadCompleted += (_, _) => card.FallbackIcon.Visibility = Visibility.Collapsed;
                bitmap.DownloadFailed += (_, _) =>
                {
                    card.LogoImage.Source = null;
                    card.FallbackIcon.Visibility = Visibility.Visible;
                };
            }
            else
            {
                card.FallbackIcon.Visibility = Visibility.Collapsed;
            }

            card.LogoImage.Source = bitmap;
        }
        catch
        {
            card.LogoImage.Source = null;
            card.FallbackIcon.Visibility = Visibility.Visible;
        }
    }

    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url } && !string.IsNullOrEmpty(url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void OnTagClick(object sender, MouseButtonEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(TagClickedEvent, this));
}
