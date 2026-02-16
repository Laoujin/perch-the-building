using System.Windows.Controls;

using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LoadCommand.CanExecute(null))
            ViewModel.LoadCommand.Execute(null);
    }

    private void OnLaunchWizard(object sender, RoutedEventArgs e)
    {
        App.ShowWizard();
    }
}
