using Microsoft.Extensions.DependencyInjection;
using Perch.Core.Backup;
using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Diff;
using Perch.Core.Git;
using Perch.Core.Machines;
using Perch.Core.Modules;
using Perch.Core.Packages;
using Perch.Core.Registry;
using Perch.Core.Status;
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
            services.AddSingleton<IFileLockDetector, WindowsFileLockDetector>();
            services.AddSingleton<IRegistryProvider, WindowsRegistryProvider>();
            services.AddSingleton<IPackageManagerProvider, ChocolateyPackageManagerProvider>();
            services.AddSingleton<IPackageManagerProvider, WingetPackageManagerProvider>();
        }
        else
        {
            services.AddSingleton<ISymlinkProvider, UnixSymlinkProvider>();
            services.AddSingleton<IFileLockDetector, UnixFileLockDetector>();
            services.AddSingleton<IRegistryProvider, NoOpRegistryProvider>();
        }
        if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IPackageManagerProvider, AptPackageManagerProvider>();
        }
        if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IPackageManagerProvider, BrewPackageManagerProvider>();
        }
        services.AddSingleton<IPackageManagerProvider, NpmPackageManagerProvider>();
        services.AddSingleton<IPackageManagerProvider, VsCodeExtensionProvider>();
        services.AddSingleton<IFileBackupProvider, FileBackupProvider>();
        services.AddSingleton<ISnapshotProvider, SnapshotProvider>();
        services.AddSingleton<SymlinkOrchestrator>();
        services.AddSingleton<IHookRunner, HookRunner>();
        services.AddSingleton<IGlobalPackageInstaller, GlobalPackageInstaller>();
        services.AddSingleton<IVscodeExtensionInstaller, VscodeExtensionInstaller>();
        services.AddSingleton<IPsModuleInstaller, PsModuleInstaller>();
        services.AddSingleton<IMachineProfileService, MachineProfileService>();
        services.AddSingleton<ICleanFilterService, CleanFilterService>();
        services.AddSingleton<IDiffSnapshotService, DiffSnapshotService>();
        services.AddSingleton<ISystemPackageInstaller, SystemPackageInstaller>();
        services.AddSingleton<IDeployService, DeployService>();
        services.AddSingleton<IStatusService, StatusService>();
        services.AddSingleton<ISettingsProvider, YamlSettingsProvider>();
        services.AddSingleton<PackageManifestParser>();
        services.AddSingleton<IProcessRunner, DefaultProcessRunner>();
        services.AddSingleton<IAppScanService, AppScanService>();
        return services;
    }
}
