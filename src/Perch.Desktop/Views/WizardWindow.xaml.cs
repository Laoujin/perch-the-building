using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using Perch.Desktop.ViewModels.Wizard;

namespace Perch.Desktop.Views;

public partial class WizardWindow : FluentWindow
{
    public WizardShellViewModel ViewModel { get; }

    public event Action? WizardCompleted;

    public WizardWindow(WizardShellViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
    }

    private void OnProfileCardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string profile)
        {
            switch (profile)
            {
                case "Developer":
                    ViewModel.ProfileSelection.IsDeveloper = !ViewModel.ProfileSelection.IsDeveloper;
                    break;
                case "PowerUser":
                    ViewModel.ProfileSelection.IsPowerUser = !ViewModel.ProfileSelection.IsPowerUser;
                    break;
                case "Gamer":
                    ViewModel.ProfileSelection.IsGamer = !ViewModel.ProfileSelection.IsGamer;
                    break;
                case "Casual":
                    ViewModel.ProfileSelection.IsCasual = !ViewModel.ProfileSelection.IsCasual;
                    break;
            }
        }
    }

    private void OnOpenDashboard(object sender, RoutedEventArgs e)
    {
        WizardCompleted?.Invoke();
        Close();
    }
}
