using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

using Perch.Desktop.ViewModels;

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
}
