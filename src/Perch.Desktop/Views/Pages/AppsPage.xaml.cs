using System.Windows;
using System.Windows.Controls;

using Perch.Desktop.Models;
using Perch.Desktop.ViewModels;
using Perch.Desktop.Views.Controls;

namespace Perch.Desktop.Views.Pages;

public partial class AppsPage : Page
{
    public AppsViewModel ViewModel { get; }

    public AppsPage(AppsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.RefreshCommand.CanExecute(null))
            ViewModel.RefreshCommand.Execute(null);
    }

    private void OnLinkRequested(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app && ViewModel.LinkAppCommand.CanExecute(app))
            ViewModel.LinkAppCommand.Execute(app);
    }

    private void OnUnlinkRequested(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app && ViewModel.UnlinkAppCommand.CanExecute(app))
            ViewModel.UnlinkAppCommand.Execute(app);
    }

    private void OnFixRequested(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app && ViewModel.FixAppCommand.CanExecute(app))
            ViewModel.FixAppCommand.Execute(app);
    }

    private static AppCardModel? GetAppModel(object sender) =>
        (sender as AppCard)?.DataContext as AppCardModel;
}
