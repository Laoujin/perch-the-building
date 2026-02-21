using System.Collections.Immutable;
using Perch.Core.Backup;
using Perch.Core.EnvPath;
using Perch.Core.Fonts;
using Perch.Core.Git;
using Perch.Core.Machines;
using Perch.Core.Modules;
using Perch.Core.Packages;
using Perch.Core.Registry;
using Perch.Core.Symlinks;
using Perch.Core.Templates;

namespace Perch.Core.Deploy;

public sealed class DeployService : IDeployService
{
    private readonly IModuleDiscoveryService _discoveryService;
    private readonly SymlinkOrchestrator _orchestrator;
    private readonly IPlatformDetector _platformDetector;
    private readonly IGlobResolver _globResolver;
    private readonly ISnapshotProvider _snapshotProvider;
    private readonly IHookRunner _hookRunner;
    private readonly IMachineProfileService _machineProfileService;
    private readonly IRegistryProvider _registryProvider;
    private readonly IGlobalPackageInstaller _globalPackageInstaller;
    private readonly IVscodeExtensionInstaller _vscodeExtensionInstaller;
    private readonly IPsModuleInstaller _psModuleInstaller;
    private readonly PackageManifestParser _packageManifestParser;
    private readonly InstallManifestParser _installManifestParser;
    private readonly IInstallResolver _installResolver;
    private readonly ISystemPackageInstaller _systemPackageInstaller;
    private readonly ITemplateProcessor _templateProcessor;
    private readonly IReferenceResolver _referenceResolver;
    private readonly IVariableResolver _variableResolver;
    private readonly ICleanFilterService _cleanFilterService;
    private readonly FontManifestParser _fontManifestParser;
    private readonly IPathService _pathService;
    private readonly IInstalledAppChecker _installedAppChecker;

    public DeployService(IModuleDiscoveryService discoveryService, SymlinkOrchestrator orchestrator, IPlatformDetector platformDetector, IGlobResolver globResolver, ISnapshotProvider snapshotProvider, IHookRunner hookRunner, IMachineProfileService machineProfileService, IRegistryProvider registryProvider, IGlobalPackageInstaller globalPackageInstaller, IVscodeExtensionInstaller vscodeExtensionInstaller, IPsModuleInstaller psModuleInstaller, PackageManifestParser packageManifestParser, InstallManifestParser installManifestParser, IInstallResolver installResolver, ISystemPackageInstaller systemPackageInstaller, ITemplateProcessor templateProcessor, IReferenceResolver referenceResolver, IVariableResolver variableResolver, ICleanFilterService cleanFilterService, FontManifestParser fontManifestParser, IPathService pathService, IInstalledAppChecker installedAppChecker)
    {
        _discoveryService = discoveryService;
        _orchestrator = orchestrator;
        _platformDetector = platformDetector;
        _globResolver = globResolver;
        _snapshotProvider = snapshotProvider;
        _hookRunner = hookRunner;
        _machineProfileService = machineProfileService;
        _registryProvider = registryProvider;
        _globalPackageInstaller = globalPackageInstaller;
        _vscodeExtensionInstaller = vscodeExtensionInstaller;
        _psModuleInstaller = psModuleInstaller;
        _packageManifestParser = packageManifestParser;
        _installManifestParser = installManifestParser;
        _installResolver = installResolver;
        _systemPackageInstaller = systemPackageInstaller;
        _templateProcessor = templateProcessor;
        _referenceResolver = referenceResolver;
        _variableResolver = variableResolver;
        _cleanFilterService = cleanFilterService;
        _fontManifestParser = fontManifestParser;
        _pathService = pathService;
        _installedAppChecker = installedAppChecker;
    }

    public async Task<int> DeployAsync(string configRepoPath, DeployOptions? options = null, CancellationToken cancellationToken = default)
    {
        bool dryRun = options?.DryRun ?? false;
        IProgress<DeployResult>? progress = options?.Progress;
        var beforeModule = options?.BeforeModule;
        var beforeSection = options?.BeforeSection;

        DiscoveryResult discovery = await _discoveryService.DiscoverAsync(configRepoPath, cancellationToken).ConfigureAwait(false);

        bool hasErrors = ReportDiscoveryErrors(discovery, progress);
        Platform currentPlatform = _platformDetector.CurrentPlatform;
        MachineProfile? machineProfile = await _machineProfileService.LoadAsync(configRepoPath, cancellationToken).ConfigureAwait(false);
        IReadOnlySet<string> installedPackages = await _installedAppChecker.GetInstalledPackageIdsAsync(cancellationToken).ConfigureAwait(false);

        var eligibleModules = FilterEligibleModules(discovery.Modules, currentPlatform, machineProfile, installedPackages, progress);

        IReadOnlyDictionary<string, string>? variables = machineProfile?.Variables;

        if (!dryRun)
        {
            IReadOnlyList<string> allTargetPaths = CollectTargetPaths(discovery.Modules, currentPlatform, variables);
            _snapshotProvider.CreateSnapshot(allTargetPaths, cancellationToken);
        }

        foreach (AppModule module in eligibleModules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (beforeModule != null)
            {
                IReadOnlyList<DeployResult> preview = await CollectModulePreviewAsync(module, currentPlatform, variables, configRepoPath, !dryRun, cancellationToken).ConfigureAwait(false);

                ModuleAction action = await beforeModule(module, preview).ConfigureAwait(false);
                if (action == ModuleAction.Skip)
                {
                    progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, "Skipped (user)", DeployEventType.ModuleSkipped));
                    continue;
                }

                if (action == ModuleAction.Abort)
                {
                    return 0;
                }
            }

            progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, "", DeployEventType.ModuleStarted));

            bool moduleHadErrors = await DeployModuleAsync(module, currentPlatform, variables, configRepoPath, dryRun, progress, cancellationToken).ConfigureAwait(false);

            ResultLevel completionLevel = moduleHadErrors ? ResultLevel.Error : ResultLevel.Ok;
            progress?.Report(new DeployResult(module.DisplayName, "", "", completionLevel, "", DeployEventType.ModuleCompleted));

            if (moduleHadErrors)
            {
                hasErrors = true;
            }
        }

        string machineName = Environment.MachineName;
        var (pkgErrors, pkgAborted) = await ProcessSystemPackagesAsync(configRepoPath, machineName, currentPlatform, dryRun, progress, beforeSection, cancellationToken).ConfigureAwait(false);
        if (pkgAborted)
        {
            return 0;
        }
        if (pkgErrors)
        {
            hasErrors = true;
        }

        var (fontErrors, fontAborted) = await ProcessFontsAsync(configRepoPath, currentPlatform, dryRun, progress, beforeSection, cancellationToken).ConfigureAwait(false);
        if (fontAborted)
        {
            return 0;
        }
        if (fontErrors)
        {
            hasErrors = true;
        }

        if (!dryRun && await SetupCleanFiltersAsync(configRepoPath, discovery.Modules, progress, cancellationToken).ConfigureAwait(false))
        {
            hasErrors = true;
        }

        return hasErrors ? 1 : 0;
    }

    private static string? GetSkipReason(AppModule module, Platform currentPlatform, MachineProfile? machineProfile, IReadOnlySet<string> installedPackages)
    {
        if (!module.Enabled)
        {
            return "Skipped (disabled)";
        }

        if (module.Platforms.Length > 0 && !module.Platforms.Contains(currentPlatform))
        {
            return $"Skipped (not for {currentPlatform})";
        }

        if (machineProfile?.IncludeModules.Length > 0 && !machineProfile.IncludeModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
        {
            return "Skipped (not in machine profile)";
        }

        if (machineProfile?.ExcludeModules.Length > 0 && machineProfile.ExcludeModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
        {
            return "Skipped (excluded by machine profile)";
        }

        if (module.Install?.Winget != null && !installedPackages.Contains(module.Install.Winget))
        {
            return "Skipped (not installed)";
        }

        return null;
    }

    private static bool ReportDiscoveryErrors(DiscoveryResult discovery, IProgress<DeployResult>? progress)
    {
        foreach (string error in discovery.Errors)
        {
            progress?.Report(new DeployResult("discovery", "", "", ResultLevel.Error, error));
        }

        return discovery.Errors.Length > 0;
    }

    private static List<AppModule> FilterEligibleModules(System.Collections.Immutable.ImmutableArray<AppModule> modules, Platform currentPlatform, MachineProfile? machineProfile, IReadOnlySet<string> installedPackages, IProgress<DeployResult>? progress)
    {
        var eligibleModules = new List<AppModule>();
        foreach (AppModule module in modules)
        {
            string? skipReason = GetSkipReason(module, currentPlatform, machineProfile, installedPackages);
            if (skipReason != null)
            {
                progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, skipReason, DeployEventType.ModuleSkipped));
            }
            else
            {
                progress?.Report(new DeployResult(module.DisplayName, "", "", ResultLevel.Ok, "", DeployEventType.ModuleDiscovered));
                eligibleModules.Add(module);
            }
        }

        return eligibleModules;
    }

    private async Task<IReadOnlyList<DeployResult>> CollectModulePreviewAsync(AppModule module, Platform currentPlatform, IReadOnlyDictionary<string, string>? variables, string configRepoPath, bool includePackages, CancellationToken cancellationToken)
    {
        var results = new List<DeployResult>();
        var previewProgress = new SynchronousProgress<DeployResult>(results.Add);
        await ProcessModuleLinksAsync(module, currentPlatform, variables, configRepoPath, true, previewProgress, cancellationToken).ConfigureAwait(false);
        ProcessModuleRegistry(module, true, previewProgress);
        if (includePackages)
        {
            await ProcessModuleGlobalPackagesAsync(module, true, previewProgress, cancellationToken).ConfigureAwait(false);
            ProcessModulePathEntries(module, currentPlatform, true, previewProgress);
            await ProcessListAsync(module.VscodeExtensions, id => _vscodeExtensionInstaller.InstallAsync(module.DisplayName, id, true, cancellationToken), previewProgress, cancellationToken).ConfigureAwait(false);
            await ProcessListAsync(module.PsModules, name => _psModuleInstaller.InstallAsync(module.DisplayName, name, true, cancellationToken), previewProgress, cancellationToken).ConfigureAwait(false);
        }
        return results;
    }

    private async Task<bool> DeployModuleAsync(AppModule module, Platform currentPlatform, IReadOnlyDictionary<string, string>? variables, string configRepoPath, bool dryRun, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
    {
        bool hasErrors = false;

        if (!dryRun && module.Hooks?.PreDeploy != null)
        {
            string preDeployPath = Path.GetFullPath(Path.Combine(module.ModulePath, module.Hooks.PreDeploy));
            DeployResult hookResult = await _hookRunner.RunAsync(module.DisplayName, preDeployPath, module.ModulePath, cancellationToken).ConfigureAwait(false);
            progress?.Report(hookResult);
            if (hookResult.Level == ResultLevel.Error)
            {
                return true;
            }
        }

        bool moduleHadErrors = await ProcessModuleLinksAsync(module, currentPlatform, variables, configRepoPath, dryRun, progress, cancellationToken).ConfigureAwait(false);
        ProcessModuleRegistry(module, dryRun, progress);
        if (await ProcessModuleGlobalPackagesAsync(module, dryRun, progress, cancellationToken).ConfigureAwait(false))
        {
            moduleHadErrors = true;
        }
        ProcessModulePathEntries(module, currentPlatform, dryRun, progress);
        if (await ProcessListAsync(module.VscodeExtensions, id => _vscodeExtensionInstaller.InstallAsync(module.DisplayName, id, dryRun, cancellationToken), progress, cancellationToken).ConfigureAwait(false))
        {
            moduleHadErrors = true;
        }
        if (await ProcessListAsync(module.PsModules, name => _psModuleInstaller.InstallAsync(module.DisplayName, name, dryRun, cancellationToken), progress, cancellationToken).ConfigureAwait(false))
        {
            moduleHadErrors = true;
        }
        hasErrors = moduleHadErrors;

        if (!dryRun && module.Hooks?.PostDeploy != null && !moduleHadErrors)
        {
            string postDeployPath = Path.GetFullPath(Path.Combine(module.ModulePath, module.Hooks.PostDeploy));
            DeployResult hookResult = await _hookRunner.RunAsync(module.DisplayName, postDeployPath, module.ModulePath, cancellationToken).ConfigureAwait(false);
            progress?.Report(hookResult);
            if (hookResult.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        return hasErrors;
    }

    private async Task<(bool hasErrors, bool aborted)> ProcessSystemPackagesAsync(string configRepoPath, string machineName, Platform currentPlatform, bool dryRun, IProgress<DeployResult>? progress, Func<string, IReadOnlyList<DeployResult>, Task<ModuleAction>>? beforeSection, CancellationToken cancellationToken)
    {
        string installPath = Path.Combine(configRepoPath, "install.yaml");
        if (File.Exists(installPath))
        {
            return await ProcessInstallYamlAsync(installPath, machineName, currentPlatform, dryRun, progress, beforeSection, cancellationToken).ConfigureAwait(false);
        }

        string packagesPath = Path.Combine(configRepoPath, "packages.yaml");
        if (File.Exists(packagesPath))
        {
            return await ProcessPackagesYamlAsync(packagesPath, currentPlatform, dryRun, progress, beforeSection, cancellationToken).ConfigureAwait(false);
        }

        return (false, false);
    }

    private async Task<(bool hasErrors, bool aborted)> ProcessFontsAsync(string configRepoPath, Platform currentPlatform, bool dryRun, IProgress<DeployResult>? progress, Func<string, IReadOnlyList<DeployResult>, Task<ModuleAction>>? beforeSection, CancellationToken cancellationToken)
    {
        string fontsPath = Path.Combine(configRepoPath, "fonts.yaml");
        if (!File.Exists(fontsPath))
        {
            return (false, false);
        }

        string yaml = await File.ReadAllTextAsync(fontsPath, cancellationToken).ConfigureAwait(false);
        FontManifestParseResult parseResult = _fontManifestParser.Parse(yaml);

        if (!parseResult.IsSuccess)
        {
            progress?.Report(new DeployResult("Fonts", "", "", ResultLevel.Error, parseResult.Error!));
            return (true, false);
        }

        if (parseResult.FontIds.Length == 0)
        {
            return (false, false);
        }

        InstallResolution resolution = await _installResolver.ResolveFontsAsync(parseResult.FontIds, currentPlatform, cancellationToken).ConfigureAwait(false);

        if (beforeSection != null)
        {
            var preview = await CollectFontsPreviewAsync(resolution, cancellationToken).ConfigureAwait(false);
            ModuleAction action = await beforeSection("Fonts", preview).ConfigureAwait(false);
            if (action == ModuleAction.Skip)
            {
                progress?.Report(new DeployResult("Fonts", "", "", ResultLevel.Ok, "Skipped (user)", DeployEventType.ModuleSkipped));
                return (false, false);
            }
            if (action == ModuleAction.Abort)
            {
                return (false, true);
            }
        }

        progress?.Report(new DeployResult("Fonts", "", "", ResultLevel.Ok, "", DeployEventType.ModuleStarted));

        bool hasErrors = false;
        foreach (string error in resolution.Errors)
        {
            progress?.Report(new DeployResult("Fonts", "", "", ResultLevel.Error, error));
            hasErrors = true;
        }

        foreach (PackageDefinition package in resolution.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeployResult result = await _systemPackageInstaller.InstallAsync(package.Name, package.Manager, dryRun, cancellationToken).ConfigureAwait(false);
            progress?.Report(result);
            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        progress?.Report(new DeployResult("Fonts", "", "", hasErrors ? ResultLevel.Error : ResultLevel.Ok, "", DeployEventType.ModuleCompleted));

        return (hasErrors, false);
    }

    private async Task<IReadOnlyList<DeployResult>> CollectFontsPreviewAsync(InstallResolution resolution, CancellationToken cancellationToken)
    {
        var results = new List<DeployResult>();
        foreach (string error in resolution.Errors)
        {
            results.Add(new DeployResult("Fonts", "", "", ResultLevel.Error, error));
        }
        foreach (PackageDefinition package in resolution.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeployResult result = await _systemPackageInstaller.InstallAsync(package.Name, package.Manager, true, cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }
        return results;
    }

    private async Task<(bool hasErrors, bool aborted)> ProcessInstallYamlAsync(string installPath, string machineName, Platform currentPlatform, bool dryRun, IProgress<DeployResult>? progress, Func<string, IReadOnlyList<DeployResult>, Task<ModuleAction>>? beforeSection, CancellationToken cancellationToken)
    {
        string yaml = await File.ReadAllTextAsync(installPath, cancellationToken).ConfigureAwait(false);
        InstallManifestParseResult parseResult = _installManifestParser.Parse(yaml);

        if (!parseResult.IsSuccess)
        {
            progress?.Report(new DeployResult("System Packages", "", "", ResultLevel.Error, parseResult.Error!));
            return (true, false);
        }

        InstallResolution resolution = await _installResolver.ResolveAsync(parseResult.Manifest!, machineName, currentPlatform, cancellationToken).ConfigureAwait(false);

        if (beforeSection != null)
        {
            var preview = await CollectInstallPreviewAsync(resolution, cancellationToken).ConfigureAwait(false);
            if (preview.Count == 0)
            {
                return (false, false);
            }
            ModuleAction action = await beforeSection("System Packages", preview).ConfigureAwait(false);
            if (action == ModuleAction.Skip)
            {
                progress?.Report(new DeployResult("System Packages", "", "", ResultLevel.Ok, "Skipped (user)", DeployEventType.ModuleSkipped));
                return (false, false);
            }
            if (action == ModuleAction.Abort)
            {
                return (false, true);
            }
        }

        progress?.Report(new DeployResult("System Packages", "", "", ResultLevel.Ok, "", DeployEventType.ModuleStarted));

        bool hasErrors = false;
        foreach (string error in resolution.Errors)
        {
            progress?.Report(new DeployResult("System Packages", "", "", ResultLevel.Error, error));
            hasErrors = true;
        }

        foreach (PackageDefinition package in resolution.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeployResult result = await _systemPackageInstaller.InstallAsync(package.Name, package.Manager, dryRun, cancellationToken).ConfigureAwait(false);
            progress?.Report(result);
            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        progress?.Report(new DeployResult("System Packages", "", "", hasErrors ? ResultLevel.Error : ResultLevel.Ok, "", DeployEventType.ModuleCompleted));

        return (hasErrors, false);
    }

    private async Task<IReadOnlyList<DeployResult>> CollectInstallPreviewAsync(InstallResolution resolution, CancellationToken cancellationToken)
    {
        var results = new List<DeployResult>();
        foreach (string error in resolution.Errors)
        {
            results.Add(new DeployResult("System Packages", "", "", ResultLevel.Error, error));
        }
        foreach (PackageDefinition package in resolution.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeployResult result = await _systemPackageInstaller.InstallAsync(package.Name, package.Manager, true, cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }
        return results;
    }

    private async Task<(bool hasErrors, bool aborted)> ProcessPackagesYamlAsync(string packagesPath, Platform currentPlatform, bool dryRun, IProgress<DeployResult>? progress, Func<string, IReadOnlyList<DeployResult>, Task<ModuleAction>>? beforeSection, CancellationToken cancellationToken)
    {
        string yaml = await File.ReadAllTextAsync(packagesPath, cancellationToken).ConfigureAwait(false);
        PackageManifestParseResult parseResult = _packageManifestParser.Parse(yaml);

        if (beforeSection != null)
        {
            var preview = await CollectPackagesPreviewAsync(parseResult, currentPlatform, cancellationToken).ConfigureAwait(false);
            if (preview.Count == 0 && parseResult.Errors.Length == 0)
            {
                return (false, false);
            }
            ModuleAction action = await beforeSection("System Packages", preview).ConfigureAwait(false);
            if (action == ModuleAction.Skip)
            {
                progress?.Report(new DeployResult("System Packages", "", "", ResultLevel.Ok, "Skipped (user)", DeployEventType.ModuleSkipped));
                return (false, false);
            }
            if (action == ModuleAction.Abort)
            {
                return (false, true);
            }
        }

        progress?.Report(new DeployResult("System Packages", "", "", ResultLevel.Ok, "", DeployEventType.ModuleStarted));

        foreach (string error in parseResult.Errors)
        {
            progress?.Report(new DeployResult("System Packages", "", "", ResultLevel.Error, error));
        }

        bool hasErrors = parseResult.Errors.Length > 0;

        foreach (PackageDefinition package in parseResult.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!package.Manager.IsPlatformMatch(currentPlatform))
            {
                continue;
            }

            DeployResult result = await _systemPackageInstaller.InstallAsync(package.Name, package.Manager, dryRun, cancellationToken).ConfigureAwait(false);
            progress?.Report(result);

            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        progress?.Report(new DeployResult("System Packages", "", "", hasErrors ? ResultLevel.Error : ResultLevel.Ok, "", DeployEventType.ModuleCompleted));

        return (hasErrors, false);
    }

    private async Task<IReadOnlyList<DeployResult>> CollectPackagesPreviewAsync(PackageManifestParseResult parseResult, Platform currentPlatform, CancellationToken cancellationToken)
    {
        var results = new List<DeployResult>();
        foreach (string error in parseResult.Errors)
        {
            results.Add(new DeployResult("System Packages", "", "", ResultLevel.Error, error));
        }
        foreach (PackageDefinition package in parseResult.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!package.Manager.IsPlatformMatch(currentPlatform))
            {
                continue;
            }
            DeployResult result = await _systemPackageInstaller.InstallAsync(package.Name, package.Manager, true, cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }
        return results;
    }

    private async Task<bool> SetupCleanFiltersAsync(string configRepoPath, System.Collections.Immutable.ImmutableArray<AppModule> modules, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
    {
        var results = await _cleanFilterService.SetupAsync(configRepoPath, modules, cancellationToken).ConfigureAwait(false);

        bool hasErrors = false;
        foreach (CleanFilterResult result in results)
        {
            progress?.Report(new DeployResult(result.ModuleName, "", "", result.Level, result.Message));
            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        return hasErrors;
    }

    private async Task<bool> ProcessModuleLinksAsync(AppModule module, Platform currentPlatform, IReadOnlyDictionary<string, string>? variables, string configRepoPath, bool dryRun, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
    {
        bool hasErrors = false;

        foreach (LinkEntry link in module.Links)
        {
            string? target = link.GetTargetForPlatform(currentPlatform);
            if (target == null)
            {
                progress?.Report(new DeployResult(module.DisplayName, link.Source, "", ResultLevel.Ok,
                    $"Skipped (no target for {currentPlatform})"));
                continue;
            }

            string expandedTarget = EnvironmentExpander.Expand(target, variables);
            string sourcePath = Path.GetFullPath(Path.Combine(module.ModulePath, link.Source));

            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                progress?.Report(new DeployResult(module.DisplayName, sourcePath, expandedTarget, ResultLevel.Warning,
                    "Source not found (skipped)"));
                continue;
            }

            IReadOnlyList<string> resolvedTargets = _globResolver.Resolve(expandedTarget);
            if (resolvedTargets.Count == 0)
            {
                progress?.Report(new DeployResult(module.DisplayName, sourcePath, expandedTarget, ResultLevel.Warning,
                    "No matches for glob pattern"));
                continue;
            }

            foreach (string resolvedTarget in resolvedTargets)
            {
                if (link.IsTemplate)
                {
                    DeployResult templateResult = await ProcessTemplateAsync(module.Name, module.DisplayName, sourcePath, resolvedTarget, variables, configRepoPath, link.LinkType, dryRun, cancellationToken).ConfigureAwait(false);
                    progress?.Report(templateResult);
                    if (templateResult.Level == ResultLevel.Error)
                    {
                        hasErrors = true;
                    }
                    continue;
                }

                DeployResult result = _orchestrator.ProcessLink(module.DisplayName, sourcePath, resolvedTarget, link.LinkType, dryRun);
                progress?.Report(result);

                if (result.Level == ResultLevel.Error)
                {
                    hasErrors = true;
                }
            }
        }

        return hasErrors;
    }

    private async Task<DeployResult> ProcessTemplateAsync(string moduleKey, string moduleName, string sourcePath, string targetPath, IReadOnlyDictionary<string, string>? variables, string configRepoPath, LinkType linkType, bool dryRun, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Error,
                "Template source file not found");
        }

        string content = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> references = _templateProcessor.FindReferences(content);
        IReadOnlyList<string> variableNames = _templateProcessor.FindVariables(content);
        string generatedPath = Path.Combine(configRepoPath, ".generated", moduleKey, Path.GetFileName(sourcePath));

        if (dryRun)
        {
            return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Ok,
                $"Would resolve {references.Count} reference(s) and {variableNames.Count} variable(s), generate to {generatedPath}");
        }

        var resolvedValues = new Dictionary<string, string>();
        var errors = new List<string>();

        foreach (string reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReferenceResolveResult result = await _referenceResolver.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
            if (result.Value != null)
            {
                resolvedValues[reference] = result.Value;
            }
            else
            {
                errors.Add($"{reference}: {result.Error}");
            }
        }

        var warnings = new List<string>();
        foreach (string variable in variableNames)
        {
            string? resolved = _variableResolver.Resolve(variable, variables);
            if (resolved != null)
            {
                resolvedValues[variable] = resolved;
            }
            else
            {
                warnings.Add($"{{{{{variable}}}}}: unknown variable");
            }
        }

        if (errors.Count > 0)
        {
            return new DeployResult(moduleName, sourcePath, targetPath, ResultLevel.Error,
                $"Failed to resolve: {string.Join("; ", errors)}");
        }

        string resolvedContent = _templateProcessor.ReplacePlaceholders(content, resolvedValues);

        string? generatedDir = Path.GetDirectoryName(generatedPath);
        if (generatedDir != null)
        {
            Directory.CreateDirectory(generatedDir);
        }

        await File.WriteAllTextAsync(generatedPath, resolvedContent, cancellationToken).ConfigureAwait(false);

        DeployResult linkResult = _orchestrator.ProcessLink(moduleName, generatedPath, targetPath, linkType, dryRun);
        if (warnings.Count > 0)
        {
            string warningMessage = $"{linkResult.Message}; unresolved: {string.Join(", ", warnings)}";
            return linkResult with { Level = ResultLevel.Warning, Message = warningMessage };
        }

        return linkResult;
    }

    private void ProcessModuleRegistry(AppModule module, bool dryRun, IProgress<DeployResult>? progress)
    {
        if (module.Registry.IsDefault || module.Registry.Length == 0)
        {
            return;
        }

        foreach (RegistryEntryDefinition entry in module.Registry)
        {
            if (dryRun)
            {
                progress?.Report(new DeployResult(module.DisplayName, "", $"{entry.Key}\\{entry.Name}",
                    ResultLevel.Ok, $"Would set {entry.Key}\\{entry.Name} to {entry.Value}"));
                continue;
            }

            object? currentValue = _registryProvider.GetValue(entry.Key, entry.Name);
            if (Equals(currentValue, entry.Value))
            {
                progress?.Report(new DeployResult(module.DisplayName, "", $"{entry.Key}\\{entry.Name}",
                    ResultLevel.Ok, $"Registry {entry.Name} already set to {entry.Value}"));
                continue;
            }

            if (entry.Value == null)
            {
                _registryProvider.DeleteValue(entry.Key, entry.Name);
                progress?.Report(new DeployResult(module.DisplayName, "", $"{entry.Key}\\{entry.Name}",
                    ResultLevel.Ok, $"Deleted {entry.Name}"));
            }
            else
            {
                _registryProvider.SetValue(entry.Key, entry.Name, entry.Value, entry.Kind);
                progress?.Report(new DeployResult(module.DisplayName, "", $"{entry.Key}\\{entry.Name}",
                    ResultLevel.Ok, $"Set {entry.Name} to {entry.Value}"));
            }
        }
    }

    private void ProcessModulePathEntries(AppModule module, Platform currentPlatform, bool dryRun, IProgress<DeployResult>? progress)
    {
        if (module.PathEntries.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (PathEntry entry in module.PathEntries)
        {
            string? path = entry.GetPathForPlatform(currentPlatform);
            if (path == null)
            {
                continue;
            }

            string expandedPath = System.Environment.ExpandEnvironmentVariables(path);

            if (_pathService.Contains(expandedPath))
            {
                progress?.Report(new DeployResult(module.DisplayName, "", expandedPath,
                    ResultLevel.Ok, $"PATH already contains {expandedPath}"));
                continue;
            }

            if (dryRun)
            {
                progress?.Report(new DeployResult(module.DisplayName, "", expandedPath,
                    ResultLevel.Ok, $"Would add {expandedPath} to PATH"));
                continue;
            }

            if (_pathService.Add(expandedPath))
            {
                progress?.Report(new DeployResult(module.DisplayName, "", expandedPath,
                    ResultLevel.Ok, $"Added {expandedPath} to PATH"));
            }
            else
            {
                progress?.Report(new DeployResult(module.DisplayName, "", expandedPath,
                    ResultLevel.Error, $"Failed to add {expandedPath} to PATH"));
            }
        }
    }

    private async Task<bool> ProcessModuleGlobalPackagesAsync(AppModule module, bool dryRun, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
    {
        if (module.GlobalPackages == null || module.GlobalPackages.Packages.IsEmpty)
        {
            return false;
        }

        bool hasErrors = false;
        foreach (string package in module.GlobalPackages.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeployResult result = await _globalPackageInstaller.InstallAsync(module.DisplayName, module.GlobalPackages.Manager, package, dryRun, cancellationToken).ConfigureAwait(false);
            progress?.Report(result);
            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        return hasErrors;
    }

    private static async Task<bool> ProcessListAsync(System.Collections.Immutable.ImmutableArray<string> items, Func<string, Task<DeployResult>> action, IProgress<DeployResult>? progress, CancellationToken cancellationToken)
    {
        if (items.IsDefaultOrEmpty)
        {
            return false;
        }

        bool hasErrors = false;
        foreach (string item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeployResult result = await action(item).ConfigureAwait(false);
            progress?.Report(result);
            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        return hasErrors;
    }

    private IReadOnlyList<string> CollectTargetPaths(System.Collections.Immutable.ImmutableArray<AppModule> modules, Platform currentPlatform, IReadOnlyDictionary<string, string>? variables)
    {
        var targets = new List<string>();
        foreach (AppModule module in modules)
        {
            if (module.Platforms.Length > 0 && !module.Platforms.Contains(currentPlatform))
            {
                continue;
            }

            foreach (LinkEntry link in module.Links)
            {
                string? target = link.GetTargetForPlatform(currentPlatform);
                if (target == null)
                {
                    continue;
                }

                string expandedTarget = EnvironmentExpander.Expand(target, variables);
                IReadOnlyList<string> resolvedTargets = _globResolver.Resolve(expandedTarget);
                targets.AddRange(resolvedTargets);
            }
        }

        return targets;
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
