using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Perch.Desktop.Views.Controls;

public partial class EcosystemCard : UserControl
{
    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(EcosystemCard),
            new PropertyMetadata(string.Empty, OnDisplayNameChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(EcosystemCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LogoUrlProperty =
        DependencyProperty.Register(nameof(LogoUrl), typeof(string), typeof(EcosystemCard),
            new PropertyMetadata(null, OnLogoUrlChanged));

    public static readonly DependencyProperty SyncedCountProperty =
        DependencyProperty.Register(nameof(SyncedCount), typeof(int), typeof(EcosystemCard),
            new PropertyMetadata(0));

    public static readonly DependencyProperty DriftedCountProperty =
        DependencyProperty.Register(nameof(DriftedCount), typeof(int), typeof(EcosystemCard),
            new PropertyMetadata(0));

    public static readonly DependencyProperty DetectedCountProperty =
        DependencyProperty.Register(nameof(DetectedCount), typeof(int), typeof(EcosystemCard),
            new PropertyMetadata(0));

    public static readonly RoutedEvent CardClickedEvent =
        EventManager.RegisterRoutedEvent(nameof(CardClicked), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(EcosystemCard));

    public string DisplayName
    {
        get => (string)GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string? LogoUrl
    {
        get => (string?)GetValue(LogoUrlProperty);
        set => SetValue(LogoUrlProperty, value);
    }

    public int SyncedCount
    {
        get => (int)GetValue(SyncedCountProperty);
        set => SetValue(SyncedCountProperty, value);
    }

    public int DriftedCount
    {
        get => (int)GetValue(DriftedCountProperty);
        set => SetValue(DriftedCountProperty, value);
    }

    public int DetectedCount
    {
        get => (int)GetValue(DetectedCountProperty);
        set => SetValue(DetectedCountProperty, value);
    }

    public event RoutedEventHandler CardClicked
    {
        add => AddHandler(CardClickedEvent, value);
        remove => RemoveHandler(CardClickedEvent, value);
    }

    public EcosystemCard()
    {
        InitializeComponent();
    }

    private void OnCardClick(object sender, MouseButtonEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(CardClickedEvent, this));

    private static void OnDisplayNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EcosystemCard card && e.NewValue is string label && label.Length > 0)
            card.FallbackInitial.Text = label[..1].ToUpperInvariant();
    }

    private static void OnLogoUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not EcosystemCard card)
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
            bitmap.DecodePixelWidth = 64;
            bitmap.EndInit();

            if (bitmap.IsDownloading)
            {
                void OnCompleted(object? s, EventArgs _)
                {
                    bitmap.DownloadCompleted -= OnCompleted;
                    bitmap.DownloadFailed -= OnFailed;
                    card.FallbackIcon.Visibility = Visibility.Collapsed;
                }

                void OnFailed(object? s, ExceptionEventArgs _)
                {
                    bitmap.DownloadCompleted -= OnCompleted;
                    bitmap.DownloadFailed -= OnFailed;
                    card.LogoImage.Source = null;
                    card.FallbackIcon.Visibility = Visibility.Visible;
                }

                bitmap.DownloadCompleted += OnCompleted;
                bitmap.DownloadFailed += OnFailed;
            }
            else
            {
                card.FallbackIcon.Visibility = Visibility.Collapsed;
            }

            card.LogoImage.Source = bitmap;
        }
        catch (Exception ex) when (ex is UriFormatException or IOException or NotSupportedException or InvalidOperationException)
        {
            Debug.WriteLine($"Logo load failed for {url}: {ex.Message}");
            card.LogoImage.Source = null;
            card.FallbackIcon.Visibility = Visibility.Visible;
        }
    }
}
