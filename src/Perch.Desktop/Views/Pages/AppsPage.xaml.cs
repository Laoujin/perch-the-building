using System.Windows.Controls;

using Perch.Desktop.ViewModels;

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
}
