using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Views;

public partial class MainWindow : INavigationWindow
{
    private readonly INavigationService _navigationService;

    public MainWindow(
        INavigationService navigationService,
        ISnackbarService snackbarService,
        MainWindowViewModel viewModel)
    {
        _navigationService = navigationService;
        DataContext = viewModel;

        InitializeComponent();

        _navigationService.SetNavigationControl(RootNavigation);
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
    }

    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    public void SetServiceProvider(IServiceProvider serviceProvider) { }

    public void SetPageService(INavigationViewPageProvider pageProvider)
    {
        RootNavigation.SetPageProviderService(pageProvider);
    }

    public void ShowWindow() => Show();

    public void CloseWindow() => Close();

    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        RootNavigation.IsPaneOpen = !RootNavigation.IsPaneOpen;
    }
}
