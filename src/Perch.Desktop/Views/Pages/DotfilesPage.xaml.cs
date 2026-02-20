using System.Diagnostics;
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

    private bool _isLoaded;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
            return;
        _isLoaded = true;

        if (ViewModel.RefreshCommand.CanExecute(null))
            ViewModel.RefreshCommand.Execute(null);
    }

    private void OnOpenFileLocationClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path } && !string.IsNullOrEmpty(path))
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
    }

    private void OnActionClicked(object sender, RoutedEventArgs e)
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
