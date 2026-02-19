using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Perch.Desktop.Views.Controls;

public partial class ProfileCard : UserControl
{
    public static readonly DependencyProperty ProfileNameProperty =
        DependencyProperty.Register(nameof(ProfileName), typeof(string), typeof(ProfileCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TaglineProperty =
        DependencyProperty.Register(nameof(Tagline), typeof(string), typeof(ProfileCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ProfileCard),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty HeroImageSourceProperty =
        DependencyProperty.Register(nameof(HeroImageSource), typeof(ImageSource), typeof(ProfileCard),
            new PropertyMetadata(null, OnHeroImageSourceChanged));

    public static readonly RoutedEvent SelectionChangedEvent =
        EventManager.RegisterRoutedEvent(nameof(SelectionChanged), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(ProfileCard));

    public string ProfileName
    {
        get => (string)GetValue(ProfileNameProperty);
        set => SetValue(ProfileNameProperty, value);
    }

    public string Tagline
    {
        get => (string)GetValue(TaglineProperty);
        set => SetValue(TaglineProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ImageSource? HeroImageSource
    {
        get => (ImageSource?)GetValue(HeroImageSourceProperty);
        set => SetValue(HeroImageSourceProperty, value);
    }

    public event RoutedEventHandler SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    public ProfileCard()
    {
        InitializeComponent();
    }

    private void OnCardClick(object sender, MouseButtonEventArgs e)
    {
        IsSelected = !IsSelected;
        RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProfileCard card)
            card.UpdateBorder((bool)e.NewValue);
    }

    private static void OnHeroImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProfileCard card)
            card.HeroImage.Source = e.NewValue as ImageSource;
    }

    private void UpdateBorder(bool selected)
    {
        CardBorder.BorderBrush = (Brush)Application.Current.FindResource(
            selected ? "AccentBrush" : "CardBorderBrush");
    }
}
