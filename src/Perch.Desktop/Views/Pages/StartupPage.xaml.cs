using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

using Perch.Desktop.Models;
using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Views.Pages;

public partial class StartupPage : Page
{
    public StartupViewModel ViewModel { get; }

    public StartupPage(StartupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private bool _isLoaded;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.FilteredItems.CollectionChanged += OnFilteredItemsChanged;

        if (_isLoaded)
            return;
        _isLoaded = true;

        if (ViewModel.RefreshCommand.CanExecute(null))
            ViewModel.RefreshCommand.Execute(null);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.FilteredItems.CollectionChanged -= OnFilteredItemsChanged;
    }

    private void OnFilteredItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateEmptyState();

    private void OnToggleChecked(object sender, RoutedEventArgs e)
    {
        if (GetCardModel(sender) is { IsEnabled: false } card)
            ViewModel.ToggleEnabledCommand.Execute(card);
    }

    private void OnToggleUnchecked(object sender, RoutedEventArgs e)
    {
        if (GetCardModel(sender) is { IsEnabled: true } card)
            ViewModel.ToggleEnabledCommand.Execute(card);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (GetCardModel(sender) is { } card)
            ViewModel.RemoveStartupItemCommand.Execute(card);
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = ViewModel.FilteredItems.Count == 0 && !ViewModel.IsLoading
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static StartupCardModel? GetCardModel(object sender) =>
        (sender as FrameworkElement)?.DataContext as StartupCardModel;
}
