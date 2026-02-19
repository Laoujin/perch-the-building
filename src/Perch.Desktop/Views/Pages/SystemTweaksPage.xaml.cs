using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Perch.Desktop.Models;
using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Views.Pages;

public partial class SystemTweaksPage : Page
{
    public SystemTweaksViewModel ViewModel { get; }

    public SystemTweaksPage(SystemTweaksViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private bool _isLoaded;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
            return;
        _isLoaded = true;

        if (ViewModel.RefreshCommand.CanExecute(null))
            ViewModel.RefreshCommand.Execute(null);
    }

    private void OnCategoryCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCategoryCardModel card })
            ViewModel.SelectCategoryCommand.Execute(card.Category);
    }

    private void OnTweakCategoryClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCategoryCardModel card })
            card.IsExpanded = !card.IsExpanded;
    }

    private void OnProfileFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string filter })
            ViewModel.SetProfileFilterCommand.Execute(filter);
    }

    private void OnTweakExpandClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCardModel tweak })
            tweak.IsExpanded = !tweak.IsExpanded;
    }

    private void OnApplyTweakClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCardModel card })
            ViewModel.ApplyTweakCommand.Execute(card);
    }

    private void OnRevertTweakClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCardModel card })
            ViewModel.RevertTweakCommand.Execute(card);
    }

    private void OnRevertToCapturedClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCardModel card })
            ViewModel.RevertTweakToCapturedCommand.Execute(card);
    }

    private void OnOpenRegeditClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCardModel card })
            ViewModel.OpenRegeditCommand.Execute(card);
    }

    private void OnGroupExpandClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FontFamilyGroupModel group })
            group.IsExpanded = !group.IsExpanded;
    }

    private void OnOpenCertificateManagerClick(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenCertificateManagerCommand.Execute(null);
    }

    private void OnCertificateGroupExpandClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CertificateStoreGroupModel group })
            group.IsExpanded = !group.IsExpanded;
    }

    private void OnCertificateExpandClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CertificateCardModel cert })
            cert.IsExpanded = !cert.IsExpanded;
    }

    private void OnCertificateDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CertificateCardModel card })
            return;

        var result = System.Windows.MessageBox.Show(
            $"Delete certificate \"{card.SubjectDisplayName}\" from {card.Certificate.Store} store?\n\nThis cannot be undone.",
            "Delete Certificate",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
            ViewModel.RemoveCertificateCommand.Execute(card);
    }

    private void OnCertificateExpiryFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string filter })
            ViewModel.SetCertificateExpiryFilterCommand.Execute(filter);
    }

    private void OnStartupToggleEnabledClick(object sender, RoutedEventArgs e)
    {
        if (GetStartupCardModel(sender) is { } card)
            ViewModel.ToggleStartupEnabledCommand.Execute(card);
    }

    private void OnStartupRemoveClick(object sender, RoutedEventArgs e)
    {
        if (GetStartupCardModel(sender) is { } card)
            ViewModel.RemoveStartupItemCommand.Execute(card);
    }

    private static StartupCardModel? GetStartupCardModel(object sender) =>
        (sender as FrameworkElement)?.DataContext as StartupCardModel;
}
