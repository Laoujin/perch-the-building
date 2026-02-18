using System.Windows;
using System.Windows.Controls;

using Perch.Desktop.Models;
using Perch.Desktop.ViewModels;
using Perch.Desktop.Views.Controls;

namespace Perch.Desktop.Views.Pages;

public partial class DotfilesPage : Page
{
    public DotfilesViewModel ViewModel { get; }

    public DotfilesPage(DotfilesViewModel viewModel)
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

    private void OnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app && ViewModel.ToggleDotfileCommand.CanExecute(app))
            ViewModel.ToggleDotfileCommand.Execute(app);
    }

    private void OnConfigureClicked(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app)
            ViewModel.ConfigureAppCommand.Execute(app);
    }

    private static AppCardModel? GetAppModel(object sender) =>
        (sender as AppCard)?.DataContext as AppCardModel;
}
