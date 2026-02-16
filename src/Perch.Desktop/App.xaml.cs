using System.Windows.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;

using Perch.Core;
using Perch.Core.Config;
using Perch.Desktop.ViewModels;
using Perch.Desktop.ViewModels.Wizard;
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

            services.AddTransient<WizardShellViewModel>();
            services.AddTransient<WizardWindow>();
        })
        .Build();

    public static IServiceProvider Services => _host.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81),
            ApplicationTheme.Dark);

        await _host.StartAsync();

        var settings = await Services.GetRequiredService<ISettingsProvider>().LoadAsync();
        var isFirstRun = string.IsNullOrWhiteSpace(settings.ConfigRepoPath);

        if (isFirstRun)
        {
            ShowWizard();
        }
        else
        {
            ShowMainWindow();
        }

        base.OnStartup(e);
    }

    public static void ShowWizard()
    {
        var wizard = Services.GetRequiredService<WizardWindow>();
        wizard.WizardCompleted += () => ShowMainWindow();
        wizard.Show();
    }

    public static void ShowMainWindow()
    {
        var mainWindow = Services.GetRequiredService<INavigationWindow>();
        mainWindow.ShowWindow();
        mainWindow.Navigate(typeof(DashboardPage));
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
