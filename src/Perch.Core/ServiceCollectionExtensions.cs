using Microsoft.Extensions.DependencyInjection;
using Perch.Core.Backup;
using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Diff;
using Perch.Core.Fonts;
using Perch.Core.Git;
using Perch.Core.Machines;
using Perch.Core.Modules;
using Perch.Core.Packages;
using Perch.Core.Registry;
using Perch.Core.Scanner;
using Perch.Core.Startup;
using Perch.Core.Status;
using Perch.Core.Symlinks;
using Perch.Core.Templates;
using Perch.Core.Tweaks;

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
            services.AddSingleton<IStartupService, WindowsStartupService>();
            services.AddSingleton<IPackageManagerProvider, ChocolateyPackageManagerProvider>();
            services.AddSingleton<IPackageManagerProvider, WingetPackageManagerProvider>();
        }
        else
        {
            services.AddSingleton<ISymlinkProvider, UnixSymlinkProvider>();
            services.AddSingleton<IFileLockDetector, UnixFileLockDetector>();
            services.AddSingleton<IRegistryProvider, NoOpRegistryProvider>();
            services.AddSingleton<IStartupService, NoOpStartupService>();
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
        services.AddSingleton<IAppLinkService, AppLinkService>();
        services.AddSingleton<IHookRunner, HookRunner>();
        services.AddSingleton<IGlobalPackageInstaller, GlobalPackageInstaller>();
        services.AddSingleton<IVscodeExtensionInstaller, VscodeExtensionInstaller>();
        services.AddSingleton<IPsModuleInstaller, PsModuleInstaller>();
        services.AddSingleton<IMachineProfileService, MachineProfileService>();
        services.AddSingleton<ICleanFilterService, CleanFilterService>();
        services.AddSingleton<ISubmoduleService, SubmoduleService>();
        services.AddSingleton<IRegistryCaptureService, RegistryCaptureService>();
        services.AddSingleton<IDiffSnapshotService, DiffSnapshotService>();
        services.AddSingleton<ISystemPackageInstaller, SystemPackageInstaller>();
        services.AddSingleton<IDeployService, DeployService>();
        services.AddSingleton<IStatusService, StatusService>();
        services.AddSingleton<ISettingsProvider, YamlSettingsProvider>();
        services.AddSingleton<PackageManifestParser>();
        services.AddSingleton<InstallManifestParser>();
        services.AddSingleton<IInstallResolver, InstallResolver>();
        services.AddSingleton<IProcessRunner, DefaultProcessRunner>();
        services.AddSingleton<ITemplateProcessor, TemplateProcessor>();
        services.AddSingleton<IReferenceResolver, OnePasswordResolver>();
        services.AddSingleton<IVariableResolver, MachineVariableResolver>();
        services.AddSingleton<IContentFilterProcessor, ContentFilterProcessor>();
        services.AddSingleton<IAppScanService, AppScanService>();
        services.AddSingleton<ITweakService, TweakService>();

        services.AddSingleton<CatalogParser>();
        services.AddSingleton(new System.Net.Http.HttpClient());
        services.AddSingleton<ICatalogFetcher, SettingsAwareCatalogFetcher>();
        services.AddSingleton<ICatalogCache>(_ => new FileCatalogCache(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "perch", "cache", "catalog")));
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<IGalleryOverlayService, GalleryOverlayService>();
        services.AddSingleton<IFontScanner, FontScanner>();
        services.AddSingleton<IFontOnboardingService, FontOnboardingService>();
        services.AddSingleton<IVsCodeService, VsCodeService>();
        services.AddSingleton<ISystemScanner, SystemScanner>();
        return services;
    }
}
