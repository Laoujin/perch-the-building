using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Perch.Desktop.Models;
using Perch.Desktop.ViewModels;
using Perch.Desktop.Views.Controls;

namespace Perch.Desktop.Views.Pages;

public partial class LanguagesPage : Page
{
    public LanguagesViewModel ViewModel { get; }

    public LanguagesPage(LanguagesViewModel viewModel)
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

    private void OnEcosystemCardClicked(object sender, RoutedEventArgs e)
    {
        if (sender is EcosystemCard { DataContext: EcosystemCardModel ecosystem })
            ViewModel.SelectEcosystemCommand.Execute(ecosystem);
    }

    private void OnEcosystemBreadcrumbClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.BackToEcosystemCommand.CanExecute(null))
            ViewModel.BackToEcosystemCommand.Execute(null);
    }

    private void OnConfigureClicked(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app)
            ViewModel.SelectItemCommand.Execute(app);
    }

    private void OnActionClicked(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app && ViewModel.ToggleAppCommand.CanExecute(app))
            ViewModel.ToggleAppCommand.Execute(app);
    }

    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url } && !string.IsNullOrEmpty(url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static AppCardModel? GetAppModel(object sender) =>
        (sender as AppCard)?.DataContext as AppCardModel;
}
