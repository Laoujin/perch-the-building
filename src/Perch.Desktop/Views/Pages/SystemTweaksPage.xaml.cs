using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Perch.Desktop.Models;
using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Views.Pages;

public partial class SystemTweaksPage : Page
{
    public SystemTweaksViewModel ViewModel { get; }

    public SystemTweaksPage(SystemTweaksViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemTweaksViewModel.SelectedCategory))
                UpdateDetailPanelVisibility();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.RefreshCommand.CanExecute(null))
            ViewModel.RefreshCommand.Execute(null);
    }

    private void OnClearRequested(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearSelection();
    }

    private void OnCategoryCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCategoryCardModel card })
        {
            ViewModel.SelectCategoryCommand.Execute(card.Category);
        }
    }

    private void OnGroupExpandClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FontFamilyGroupModel group })
            group.IsExpanded = !group.IsExpanded;
    }

    private void UpdateDetailPanelVisibility()
    {
        var isFonts = string.Equals(ViewModel.SelectedCategory, "Fonts", StringComparison.OrdinalIgnoreCase);
        TweakDetailPanel.Visibility = ViewModel.SelectedCategory is not null && !isFonts
            ? Visibility.Visible : Visibility.Collapsed;
        FontDetailPanel.Visibility = isFonts
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
