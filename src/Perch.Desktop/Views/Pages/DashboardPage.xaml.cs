using System.Windows.Controls;

using Perch.Desktop.Services;
using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Views.Pages;

public partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage(DashboardViewModel viewModel)
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

    private void OnLaunchWizard(object sender, RoutedEventArgs e)
    {
        App.ShowWizard();
    }

    private void OnDiscardChange(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PendingChange change })
            ViewModel.DiscardChangeCommand.Execute(change);
    }

    private void OnPendingToggle(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PendingChange change })
            ViewModel.TogglePendingChangeCommand.Execute(change);
    }
}
