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
            if (e.PropertyName is nameof(SystemTweaksViewModel.SelectedCategory)
                or nameof(SystemTweaksViewModel.SelectedSubCategory))
                UpdateDetailPanelVisibility();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.RefreshCommand.CanExecute(null))
            ViewModel.RefreshCommand.Execute(null);
    }

    private void OnCategoryCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCategoryCardModel card })
            ViewModel.SelectCategoryCommand.Execute(card.Category);
    }

    private void OnSubCategoryCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCategoryCardModel card })
            ViewModel.SelectSubCategoryCommand.Execute(card.Category);
    }

    private void OnProfileFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string filter })
            ViewModel.SetProfileFilterCommand.Execute(filter);
    }

    private void OnTweakExpandClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCardModel tweak })
            tweak.IsExpanded = !tweak.IsExpanded;
    }

    private void OnApplyTweakClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCardModel card })
            ViewModel.ApplyTweakCommand.Execute(card);
    }

    private void OnRevertTweakClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCardModel card })
            ViewModel.RevertTweakCommand.Execute(card);
    }

    private void OnOpenRegeditClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakCardModel card })
            ViewModel.OpenRegeditCommand.Execute(card);
    }

    private void OnGroupExpandClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FontFamilyGroupModel group })
            group.IsExpanded = !group.IsExpanded;
    }

    private void OnStartupToggleEnabledClick(object sender, RoutedEventArgs e)
    {
        if (GetStartupCardModel(sender) is { } card)
            ViewModel.ToggleStartupEnabledCommand.Execute(card);
    }

    private void OnStartupRemoveClick(object sender, RoutedEventArgs e)
    {
        if (GetStartupCardModel(sender) is { } card)
            ViewModel.RemoveStartupItemCommand.Execute(card);
    }

    private void UpdateDetailPanelVisibility()
    {
        var category = ViewModel.SelectedCategory;
        var subCategory = ViewModel.SelectedSubCategory;
        var isFonts = string.Equals(category, "Fonts", StringComparison.OrdinalIgnoreCase);
        var isStartup = string.Equals(category, "Startup", StringComparison.OrdinalIgnoreCase);
        var isSystemTweaks = string.Equals(category, "System Tweaks", StringComparison.OrdinalIgnoreCase);

        SubCategoryPanel.Visibility = isSystemTweaks && subCategory is null
            ? Visibility.Visible : Visibility.Collapsed;
        TweakDetailPanel.Visibility = isSystemTweaks && subCategory is not null
            ? Visibility.Visible : Visibility.Collapsed;
        FontDetailPanel.Visibility = isFonts
            ? Visibility.Visible : Visibility.Collapsed;
        StartupDetailPanel.Visibility = isStartup
            ? Visibility.Visible : Visibility.Collapsed;

        UpdateBackButton();
        UpdateHeaderText();
    }

    private void UpdateBackButton()
    {
        BackButton.Command = null;
        BackButton.Click -= OnBackButtonClick;
        BackButton.Click += OnBackButtonClick;
    }

    private void OnBackButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSubCategory is not null)
            ViewModel.BackToSubCategoriesCommand.Execute(null);
        else
            ViewModel.BackToCategoriesCommand.Execute(null);
    }

    private void UpdateHeaderText()
    {
        if (ViewModel.SelectedSubCategory is not null)
            DetailHeaderText.Text = $"System Tweaks > {ViewModel.SelectedSubCategory}";
        else
            DetailHeaderText.Text = ViewModel.SelectedCategory ?? string.Empty;
    }

    private static StartupCardModel? GetStartupCardModel(object sender) =>
        (sender as FrameworkElement)?.DataContext as StartupCardModel;
}
