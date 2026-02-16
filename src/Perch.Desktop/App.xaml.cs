using System.Windows.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;

using Perch.Core;
using Perch.Desktop.ViewModels;
using Perch.Desktop.Views;
using Perch.Desktop.Views.Pages;

namespace Perch.Desktop;

public partial class App : Application
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureServices((_, services) =>
        {
            services.AddNavigationViewPageProvider();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<ISnackbarService, SnackbarService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();

            services.AddPerchCore();

            services.AddSingleton<INavigationWindow, MainWindow>();
            services.AddSingleton<MainWindowViewModel>();

            services.AddSingleton<DashboardPage>();
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<DotfilesPage>();
            services.AddSingleton<DotfilesViewModel>();
            services.AddSingleton<AppsPage>();
            services.AddSingleton<AppsViewModel>();
            services.AddSingleton<SystemTweaksPage>();
            services.AddSingleton<SystemTweaksViewModel>();
            services.AddSingleton<SettingsPage>();
            services.AddSingleton<SettingsViewModel>();
        })
        .Build();

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<INavigationWindow>();
        mainWindow.ShowWindow();
        mainWindow.Navigate(typeof(DashboardPage));

        ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81),
            ApplicationTheme.Dark);

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
    }
}
