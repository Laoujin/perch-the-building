using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;

using Perch.Core;
using Perch.Core.Config;
using Perch.Desktop.Services;
using Perch.Desktop.ViewModels;
using Perch.Desktop.ViewModels.Wizard;
using Perch.Desktop.Views;
using Perch.Desktop.Views.Pages;

namespace Perch.Desktop;

public partial class App : Application
{
    public static string? DevBranch { get; private set; }

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
            services.AddSingleton<IPendingChangesService, PendingChangesService>();
            services.AddSingleton<IGalleryDetectionService, GalleryDetectionService>();
            services.AddSingleton<IDotfileDetailService, DotfileDetailService>();
            services.AddSingleton<IAppDetailService, AppDetailService>();

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
            services.AddSingleton<StartupPage>();
            services.AddSingleton<StartupViewModel>();
            services.AddSingleton<SettingsPage>();
            services.AddSingleton<SettingsViewModel>();

            services.AddTransient<WizardShellViewModel>();
            services.AddTransient<WizardWindow>();
        })
        .Build();

    public static IServiceProvider Services => _host.Services;

    private Window? _splash;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var branchIdx = Array.IndexOf(e.Args, "--branch");
        if (branchIdx >= 0 && branchIdx + 1 < e.Args.Length)
            DevBranch = e.Args[branchIdx + 1];

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            ShowSplash();

            EventManager.RegisterClassHandler(typeof(ScrollViewer),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnScrollViewerPreviewMouseWheel));

            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            ApplicationAccentColorManager.Apply(
                System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81),
                ApplicationTheme.Dark);

            await _host.StartAsync();

            _ = Services.GetRequiredService<IGalleryDetectionService>().WarmUpAsync();

            var settings = await Services.GetRequiredService<ISettingsProvider>().LoadAsync();
            var isFirstRun = string.IsNullOrWhiteSpace(settings.ConfigRepoPath);

            CloseSplash();
            ShutdownMode = ShutdownMode.OnLastWindowClose;

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
        catch (Exception ex)
        {
            CloseSplash();
            var crashWindow = new CrashWindow(ex);
            crashWindow.ShowDialog();
            Shutdown(1);
        }
    }

    private void ShowSplash()
    {
        var image = new BitmapImage(new Uri("pack://application:,,,/Assets/startpage.png"));
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        var maxWidth = screenWidth * 0.8;
        var maxHeight = screenHeight * 0.8;

        var width = image.PixelWidth;
        var height = image.PixelHeight;
        var scale = Math.Min(1.0, Math.Min(maxWidth / width, maxHeight / height));

        _splash = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            SizeToContent = SizeToContent.WidthAndHeight,
            Topmost = true,
            ShowInTaskbar = false,
            Content = new System.Windows.Controls.Image
            {
                Source = image,
                Width = width * scale,
                Height = height * scale,
            },
        };
        _splash.Show();
    }

    private void CloseSplash()
    {
        _splash?.Close();
        _splash = null;
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

    private static void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv && !e.Handled)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        foreach (var window in Current.Windows)
        {
            if (window is Views.WizardWindow wizard)
            {
                wizard.ViewModel.ShowCrash(e.Exception);
                e.Handled = true;
                return;
            }
        }

        var crashWindow = new CrashWindow(e.Exception);
        crashWindow.ShowDialog();
        e.Handled = true;
    }
}
