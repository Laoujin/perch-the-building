using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Perch.Desktop.Models;
using Perch.Desktop.ViewModels;
using Perch.Desktop.Views.Controls;

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

    private void OnAppCategoryCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AppCategoryCardModel card })
        {
            ViewModel.SelectCategoryCommand.Execute(card.BroadCategory);
        }
    }

    private void OnLinkRequested(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app && ViewModel.LinkAppCommand.CanExecute(app))
            ViewModel.LinkAppCommand.Execute(app);
    }

    private void OnUnlinkRequested(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app && ViewModel.UnlinkAppCommand.CanExecute(app))
            ViewModel.UnlinkAppCommand.Execute(app);
    }

    private void OnFixRequested(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app && ViewModel.FixAppCommand.CanExecute(app))
            ViewModel.FixAppCommand.Execute(app);
    }

    private void OnConfigureRequested(object sender, RoutedEventArgs e)
    {
        if (GetAppModel(sender) is { } app && ViewModel.ConfigureAppCommand.CanExecute(app))
            ViewModel.ConfigureAppCommand.Execute(app);
    }

    private void OnDropZoneDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDropZoneDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            if (ViewModel.AddDroppedFilesCommand.CanExecute(files))
                ViewModel.AddDroppedFilesCommand.Execute(files);
        }
    }

    private void OnExternalLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url } && !string.IsNullOrEmpty(url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static AppCardModel? GetAppModel(object sender) =>
        (sender as AppCard)?.DataContext as AppCardModel;
}
