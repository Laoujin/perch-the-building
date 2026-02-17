using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

using Wpf.Ui.Controls;

using Perch.Desktop.Models;
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

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateStepVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WizardShellViewModel.CurrentStepIndex)
            or nameof(WizardShellViewModel.IsInitializing)
            or nameof(WizardShellViewModel.HasCrashed))
        {
            UpdateStepVisibility();
            ViewModel.NotifySelectionCounts();

            if (e.PropertyName == nameof(WizardShellViewModel.CurrentStepIndex))
                StepListBox.SelectedIndex = ViewModel.CurrentStepIndex;
        }

        if (e.PropertyName == nameof(WizardShellViewModel.SelectedTweakCategory))
        {
            UpdateTweakDetailPanelVisibility();
        }
    }

    private void UpdateStepVisibility()
    {
        if (ViewModel.IsInitializing || ViewModel.HasCrashed)
        {
            ProfileStep.Visibility = Visibility.Collapsed;
            ConfigStep.Visibility = Visibility.Collapsed;
            DotfilesStep.Visibility = Visibility.Collapsed;
            AppsStep.Visibility = Visibility.Collapsed;
            TweaksStep.Visibility = Visibility.Collapsed;
            ReviewStep.Visibility = Visibility.Collapsed;
            DeployStep.Visibility = Visibility.Collapsed;
            return;
        }

        var stepName = ViewModel.GetCurrentStepName();

        ProfileStep.Visibility = stepName == "Profile" ? Visibility.Visible : Visibility.Collapsed;
        ConfigStep.Visibility = stepName == "Config" ? Visibility.Visible : Visibility.Collapsed;
        DotfilesStep.Visibility = stepName == "Dotfiles" ? Visibility.Visible : Visibility.Collapsed;
        AppsStep.Visibility = stepName == "Apps" ? Visibility.Visible : Visibility.Collapsed;
        TweaksStep.Visibility = stepName == "System Tweaks" ? Visibility.Visible : Visibility.Collapsed;
        ReviewStep.Visibility = stepName == "Review" ? Visibility.Visible : Visibility.Collapsed;
        DeployStep.Visibility = stepName == "Deploy" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateTweakDetailPanelVisibility()
    {
        var isFonts = string.Equals(ViewModel.SelectedTweakCategory, "Fonts", StringComparison.OrdinalIgnoreCase);
        WizardTweakDetailPanel.Visibility = ViewModel.SelectedTweakCategory is not null && !isFonts
            ? Visibility.Visible : Visibility.Collapsed;
        WizardFontDetailPanel.Visibility = isFonts
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnTweakCategoryCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCategoryCardModel card })
        {
            ViewModel.SelectTweakCategoryCommand.Execute(card.Category);
        }
    }

    private void OnOpenDashboard(object sender, RoutedEventArgs e)
    {
        WizardCompleted?.Invoke();
        Close();
    }

    private async void OnStepSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox lb)
            return;

        if (lb.SelectedIndex < 0 || lb.SelectedIndex == ViewModel.CurrentStepIndex)
            return;

        var targetIndex = lb.SelectedIndex;

        if (!ViewModel.CanNavigateToStep(targetIndex))
        {
            lb.SelectedIndex = ViewModel.CurrentStepIndex;
            return;
        }

        // Forward past Config: save settings and run detection first
        if (targetIndex > ViewModel.CurrentStepIndex)
        {
            var navigated = await ViewModel.NavigateToStepAsync(targetIndex);
            if (!navigated)
                lb.SelectedIndex = ViewModel.CurrentStepIndex;
        }
        else
        {
            ViewModel.CurrentStepIndex = targetIndex;
        }
    }
}
