using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;

using Perch.Core;
using Perch.Core.Catalog;
using Perch.Desktop.ViewModels;
using Perch.Desktop.Views;

namespace Perch.Desktop;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        BindingPlugins.DataValidators.RemoveAt(0);

        var services = new ServiceCollection();
        services.AddPerchCore();
        services.AddSingleton<CatalogParser>();
        services.AddTransient<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();
        var vm = provider.GetRequiredService<MainWindowViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
