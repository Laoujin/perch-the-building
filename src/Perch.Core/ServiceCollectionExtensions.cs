using Microsoft.Extensions.DependencyInjection;
using Perch.Core.Backup;
using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Symlinks;

namespace Perch.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPerchCore(this IServiceCollection services)
    {
        services.AddSingleton<ManifestParser>();
        services.AddSingleton<IGlobResolver, GlobResolver>();
        services.AddSingleton<IModuleDiscoveryService, ModuleDiscoveryService>();
        services.AddSingleton<IPlatformDetector, PlatformDetector>();
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ISymlinkProvider, WindowsSymlinkProvider>();
        }
        else
        {
            services.AddSingleton<ISymlinkProvider, UnixSymlinkProvider>();
        }
        services.AddSingleton<IFileBackupProvider, FileBackupProvider>();
        services.AddSingleton<SymlinkOrchestrator>();
        services.AddSingleton<IDeployService, DeployService>();
        services.AddSingleton<ISettingsProvider, YamlSettingsProvider>();
        return services;
    }
}
