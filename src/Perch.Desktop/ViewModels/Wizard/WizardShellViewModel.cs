using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Fonts;

using Perch.Desktop.Models;
using Perch.Desktop.Services;
using Perch.Desktop.ViewModels;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class WizardShellViewModel : ViewModelBase
{
    private readonly IDeployService _deployService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IGalleryDetectionService _detectionService;
    private readonly IFontOnboardingService _fontOnboardingService;

    [ObservableProperty]
    private int _currentStepIndex;

    [ObservableProperty]
    private bool _isDeploying;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private bool _hasErrors;

    [ObservableProperty]
    private string _deployStatusMessage = string.Empty;

    [ObservableProperty]
    private int _deployedCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private bool _isLoadingDetection;

    [ObservableProperty]
    private string _configRepoPath = string.Empty;

    [ObservableProperty]
    private bool _configIsGitRepo;

    [ObservableProperty]
    private bool _configPathNotGitWarning;

    [ObservableProperty]
    private bool _configPathNotExistWarning;

    [ObservableProperty]
    private string _cloneUrl = string.Empty;

    [ObservableProperty]
    private bool _isCloning;

    [ObservableProperty]
    private string _crashErrorMessage = string.Empty;

    [ObservableProperty]
    private string _crashStackTrace = string.Empty;

    [ObservableProperty]
    private bool _hasCrashed;

    private readonly HashSet<UserProfile> _selectedProfiles = [];
    private readonly List<string> _stepKeys = [];
    private bool _detectionRun;

    public ObservableCollection<string> StepNames { get; } = [];

    [ObservableProperty]
    private bool _isDeveloper = true;

    [ObservableProperty]
    private bool _isPowerUser;

    [ObservableProperty]
    private bool _isGamer;

    [ObservableProperty]
    private bool _isCasual;

    public ObservableCollection<DotfileGroupCardModel> Dotfiles { get; } = [];
    public ObservableCollection<AppCardModel> YourApps { get; } = [];
    public ObservableCollection<AppCardModel> SuggestedApps { get; } = [];
    public ObservableCollection<AppCardModel> OtherApps { get; } = [];
    public ObservableCollection<TweakCardModel> Tweaks { get; } = [];
    public ObservableCollection<TweakCardModel> FilteredTweaks { get; } = [];
    public ObservableCollection<FontCardModel> InstalledFonts { get; } = [];
    public ObservableCollection<FontCardModel> NerdFonts { get; } = [];
    public ObservableCollection<AppCategoryCardModel> AppCategories { get; } = [];
    public ObservableCollection<AppCategoryGroup> FilteredAppsByCategory { get; } = [];
    public ObservableCollection<TweakCategoryCardModel> TweakCategories { get; } = [];
    public ObservableCollection<DeployResultItemViewModel> DeployResults { get; } = [];

    [ObservableProperty]
    private int _selectedAppCount;

    [ObservableProperty]
    private int _selectedDotfileCount;

    [ObservableProperty]
    private int _selectedTweakCount;

    [ObservableProperty]
    private int _selectedFontCount;

    [ObservableProperty]
    private string? _selectedAppCategory;

    [ObservableProperty]
    private string? _selectedTweakCategory;

    public bool ShowAppCategories => SelectedAppCategory is null;
    public bool ShowAppDetail => SelectedAppCategory is not null;

    public bool ShowTweakCategories => SelectedTweakCategory is null;
    public bool ShowTweakDetail => SelectedTweakCategory is not null;

    public int TotalSelectedCount => SelectedAppCount + SelectedDotfileCount + SelectedTweakCount + SelectedFontCount;

    public bool ShowDotfilesStep => IsDeveloper || IsPowerUser;

    public WizardShellViewModel(
        IDeployService deployService,
        ISettingsProvider settingsProvider,
        IGalleryDetectionService detectionService,
        IFontOnboardingService fontOnboardingService)
    {
        _deployService = deployService;
        _settingsProvider = settingsProvider;
        _detectionService = detectionService;
        _fontOnboardingService = fontOnboardingService;

        RebuildSteps();
        _ = InitializeAsync();
    }

    public bool CanGoBack => CurrentStepIndex > 0 && !IsDeploying && !IsComplete && !HasCrashed;
    public bool CanGoNext => CurrentStepIndex < StepNames.Count - 2 && !IsDeploying && !IsComplete && !HasCrashed;
    public bool ShowDeploy => CurrentStepIndex == StepNames.Count - 2 && !IsDeploying && !IsComplete && !HasCrashed;

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowDeploy));
    }

    partial void OnSelectedAppCategoryChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowAppCategories));
        OnPropertyChanged(nameof(ShowAppDetail));
    }

    partial void OnSelectedTweakCategoryChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowTweakCategories));
        OnPropertyChanged(nameof(ShowTweakDetail));
    }

    partial void OnIsDeveloperChanged(bool value) => RebuildSteps();
    partial void OnIsPowerUserChanged(bool value) => RebuildSteps();
    partial void OnIsGamerChanged(bool value) => RebuildSteps();
    partial void OnIsCasualChanged(bool value) => RebuildSteps();

    private void RebuildSteps()
    {
        _selectedProfiles.Clear();
        if (IsDeveloper) _selectedProfiles.Add(UserProfile.Developer);
        if (IsPowerUser) _selectedProfiles.Add(UserProfile.PowerUser);
        if (IsGamer) _selectedProfiles.Add(UserProfile.Gamer);
        if (IsCasual) _selectedProfiles.Add(UserProfile.Casual);

        _stepKeys.Clear();
        _stepKeys.Add("Profile");
        _stepKeys.Add("Config");
        if (ShowDotfilesStep) _stepKeys.Add("Dotfiles");
        _stepKeys.Add("Apps");
        _stepKeys.Add("Windows Tweaks");
        _stepKeys.Add("Review");
        _stepKeys.Add("Deploy");

        StepNames.Clear();
        for (var i = 0; i < _stepKeys.Count; i++)
            StepNames.Add($"{i + 1}. {_stepKeys[i]}");

        OnPropertyChanged(nameof(ShowDotfilesStep));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowDeploy));
    }

    private async Task InitializeAsync()
    {
        try
        {
            var settings = await _settingsProvider.LoadAsync();
            ConfigRepoPath = settings.ConfigRepoPath ?? string.Empty;
            ValidateConfigPath();
        }
        catch
        {
            // Settings load failure is non-fatal
        }
    }

    private void ValidateConfigPath()
    {
        if (string.IsNullOrWhiteSpace(ConfigRepoPath))
        {
            ConfigIsGitRepo = false;
            ConfigPathNotGitWarning = false;
            ConfigPathNotExistWarning = false;
            return;
        }

        var exists = Directory.Exists(ConfigRepoPath);
        ConfigPathNotExistWarning = !exists;

        var gitDir = Path.Combine(ConfigRepoPath, ".git");
        ConfigIsGitRepo = exists && (Directory.Exists(gitDir) || File.Exists(gitDir));
        ConfigPathNotGitWarning = exists && !ConfigIsGitRepo;
    }

    partial void OnConfigRepoPathChanged(string value)
    {
        ValidateConfigPath();
        _detectionRun = false;
    }

    [RelayCommand]
    private void BrowseConfigRepo()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select your perch-config repository",
        };

        if (dialog.ShowDialog() == true)
            ConfigRepoPath = dialog.FolderName;
    }

    [RelayCommand]
    private async Task CloneConfigRepoAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CloneUrl))
            return;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select folder to clone into",
        };

        if (dialog.ShowDialog() != true)
            return;

        var repoName = ExtractRepoName(CloneUrl);
        var targetDir = Path.Combine(dialog.FolderName, repoName);

        IsCloning = true;
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", $"clone \"{CloneUrl}\" \"{targetDir}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
                ConfigRepoPath = targetDir;
        }
        finally
        {
            IsCloning = false;
        }
    }

    private static string ExtractRepoName(string url)
    {
        var name = url.TrimEnd('/');
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        var lastSep = Math.Max(name.LastIndexOf('/'), name.LastIndexOf(':'));
        return lastSep >= 0 ? name[(lastSep + 1)..] : "perch-config";
    }

    [RelayCommand]
    private void SelectAppCategory(string broadCategory)
    {
        RebuildAppCategoryDetail(broadCategory);
        SelectedAppCategory = broadCategory;
    }

    [RelayCommand]
    private void BackToAppCategories()
    {
        SelectedAppCategory = null;
        RebuildAppCategories();
    }

    private void RebuildAppCategories()
    {
        AppCategories.Clear();

        var allApps = YourApps.Concat(SuggestedApps).Concat(OtherApps);
        var groups = allApps
            .GroupBy(a => a.BroadCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var items = group.ToList();
            AppCategories.Add(new AppCategoryCardModel(
                group.Key,
                group.Key,
                items.Count,
                items.Count(a => a.IsSelected)));
        }
    }

    private void RebuildAppCategoryDetail(string broadCategory)
    {
        FilteredAppsByCategory.Clear();

        var allApps = YourApps.Concat(SuggestedApps).Concat(OtherApps)
            .Where(a => string.Equals(a.BroadCategory, broadCategory, StringComparison.OrdinalIgnoreCase));

        var subGroups = allApps
            .GroupBy(a => a.SubCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in subGroups)
        {
            FilteredAppsByCategory.Add(new AppCategoryGroup(
                group.Key,
                new ObservableCollection<AppCardModel>(
                    group.OrderBy(a => a.DisplayLabel, StringComparer.OrdinalIgnoreCase))));
        }
    }

    [RelayCommand]
    private void SelectTweakCategory(string category)
    {
        FilteredTweaks.Clear();
        if (!string.Equals(category, "Fonts", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var tweak in Tweaks.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)))
                FilteredTweaks.Add(tweak);
        }

        SelectedTweakCategory = category;
    }

    [RelayCommand]
    private void BackToTweakCategories()
    {
        SelectedTweakCategory = null;
        RebuildTweakCategories();
    }

    private void RebuildTweakCategories()
    {
        TweakCategories.Clear();

        var groups = Tweaks.GroupBy(t => t.Category, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var items = group.ToList();
            TweakCategories.Add(new TweakCategoryCardModel(
                group.Key,
                group.Key,
                description: null,
                items.Count,
                items.Count(t => t.IsSelected)));
        }

        var fontCount = InstalledFonts.Count + NerdFonts.Count;
        if (fontCount > 0)
        {
            TweakCategories.Add(new TweakCategoryCardModel(
                "Fonts",
                "Fonts",
                "Detected & gallery nerd fonts",
                fontCount,
                InstalledFonts.Count(f => f.IsSelected) + NerdFonts.Count(f => f.IsSelected)));
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CanGoBack)
            CurrentStepIndex--;
    }

    [RelayCommand]
    private async Task GoNextAsync(CancellationToken cancellationToken)
    {
        if (!CanGoNext)
            return;

        var target = CurrentStepIndex + 1;

        try
        {
            var configIndex = _stepKeys.IndexOf("Config");
            if (CurrentStepIndex <= configIndex && target > configIndex)
            {
                if (string.IsNullOrWhiteSpace(ConfigRepoPath))
                    return;

                var profileNames = _selectedProfiles.Select(p => p.ToString()).ToList();
                var settings = await _settingsProvider.LoadAsync(cancellationToken);
                await _settingsProvider.SaveAsync(settings with { ConfigRepoPath = ConfigRepoPath, Profiles = profileNames }, cancellationToken);

                if (!Directory.Exists(ConfigRepoPath))
                    Directory.CreateDirectory(ConfigRepoPath);

                await RunDetectionAsync(cancellationToken);
            }

            CurrentStepIndex = target;
        }
        catch (OperationCanceledException)
        {
            // cancelled — don't show crash page
        }
        catch (Exception ex)
        {
            ShowCrash(ex);
        }
    }

    public async Task<bool> NavigateToStepAsync(int targetIndex, CancellationToken cancellationToken = default)
    {
        if (!CanNavigateToStep(targetIndex) || targetIndex <= CurrentStepIndex)
            return false;

        try
        {
            var configIndex = _stepKeys.IndexOf("Config");
            if (CurrentStepIndex <= configIndex && targetIndex > configIndex)
            {
                if (string.IsNullOrWhiteSpace(ConfigRepoPath))
                    return false;

                var profileNames = _selectedProfiles.Select(p => p.ToString()).ToList();
                var settings = await _settingsProvider.LoadAsync(cancellationToken);
                await _settingsProvider.SaveAsync(settings with { ConfigRepoPath = ConfigRepoPath, Profiles = profileNames }, cancellationToken);

                if (!Directory.Exists(ConfigRepoPath))
                    Directory.CreateDirectory(ConfigRepoPath);

                await RunDetectionAsync(cancellationToken);
            }

            CurrentStepIndex = targetIndex;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ShowCrash(ex);
            return false;
        }
    }

    private async Task RunDetectionAsync(CancellationToken cancellationToken)
    {
        IsLoadingDetection = true;

        try
        {
            var appsTask = _detectionService.DetectAppsAsync(_selectedProfiles, cancellationToken);
            var tweaksTask = _detectionService.DetectTweaksAsync(_selectedProfiles, cancellationToken);
            var dotfilesTask = _detectionService.DetectDotfilesAsync(cancellationToken);
            var fontsTask = _detectionService.DetectFontsAsync(cancellationToken);

            await Task.WhenAll(appsTask, tweaksTask, dotfilesTask, fontsTask);

            var appResult = appsTask.Result;
            var tweakResult = tweaksTask.Result;
            var dotfileResult = dotfilesTask.Result;
            var fontResult = fontsTask.Result;

            YourApps.Clear();
            SuggestedApps.Clear();
            OtherApps.Clear();
            Tweaks.Clear();
            Dotfiles.Clear();
            InstalledFonts.Clear();
            NerdFonts.Clear();

            foreach (var app in appResult.YourApps) { app.IsSelected = true; YourApps.Add(app); }
            foreach (var app in appResult.Suggested) SuggestedApps.Add(app);
            foreach (var app in appResult.OtherApps) OtherApps.Add(app);
            foreach (var tweak in tweakResult) Tweaks.Add(tweak);
            foreach (var df in dotfileResult) { df.IsSelected = df.Status == CardStatus.Linked; Dotfiles.Add(df); }
            foreach (var f in fontResult.InstalledFonts) InstalledFonts.Add(f);
            foreach (var f in fontResult.NerdFonts) NerdFonts.Add(f);

            RebuildTweakCategories();
            RebuildAppCategories();
        }
        finally
        {
            IsLoadingDetection = false;
        }

        _detectionRun = true;
        NotifySelectionCounts();
    }

    public void ShowCrash(Exception ex)
    {
        CrashErrorMessage = ex.Message;
        CrashStackTrace = ex.ToString();
        HasCrashed = true;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowDeploy));
    }

    [RelayCommand]
    private async Task DeployAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ConfigRepoPath))
        {
            DeployStatusMessage = "No config repository configured.";
            HasErrors = true;
            IsComplete = true;
            return;
        }

        IsDeploying = true;
        DeployResults.Clear();
        DeployedCount = 0;
        ErrorCount = 0;
        DeployStatusMessage = "Deploying...";

        var selectedModules = YourApps.Concat(SuggestedApps).Concat(OtherApps)
            .Where(a => a.IsSelected && a.Config is not null)
            .Select(a => a.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var progress = new Progress<DeployResult>(result =>
        {
            if (result.EventType == DeployEventType.Action)
            {
                DeployResults.Add(new DeployResultItemViewModel(result));
                if (result.Level == ResultLevel.Error)
                    ErrorCount++;
                else
                    DeployedCount++;

                DeployStatusMessage = $"Deployed {DeployedCount} items...";
            }
        });

        try
        {
            var selectedFontPaths = InstalledFonts
                .Where(f => f.IsSelected && f.FullPath is not null)
                .Select(f => f.FullPath!)
                .ToList();

            if (selectedFontPaths.Count > 0)
            {
                var fontResult = await _fontOnboardingService.OnboardAsync(
                    selectedFontPaths, settings.ConfigRepoPath, cancellationToken);
                DeployedCount += fontResult.CopiedFiles.Length;
                ErrorCount += fontResult.Errors.Length;
            }

            await _deployService.DeployAsync(
                settings.ConfigRepoPath,
                new DeployOptions
                {
                    Progress = progress,
                    BeforeModule = (module, _) =>
                    {
                        var action = selectedModules.Contains(module.Name)
                            ? ModuleAction.Proceed
                            : ModuleAction.Skip;
                        return Task.FromResult(action);
                    },
                },
                cancellationToken);

            HasErrors = ErrorCount > 0;
        }
        catch (OperationCanceledException)
        {
            DeployStatusMessage = "Deploy cancelled.";
            HasErrors = true;
        }
        catch (Exception ex)
        {
            DeployStatusMessage = $"Deploy failed: {ex.Message}";
            HasErrors = true;
        }

        IsDeploying = false;
        IsComplete = true;

        DeployStatusMessage = HasErrors
            ? $"Completed with {ErrorCount} error{(ErrorCount == 1 ? "" : "s")}. {DeployedCount} items deployed."
            : $"All done! {DeployedCount} configs linked successfully.";

        CurrentStepIndex = StepNames.Count - 1;

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowDeploy));
    }

    public void NotifySelectionCounts()
    {
        SelectedAppCount = YourApps.Concat(SuggestedApps).Concat(OtherApps).Count(a => a.IsSelected);
        SelectedDotfileCount = Dotfiles.Count(d => d.IsSelected);
        SelectedTweakCount = Tweaks.Count(t => t.IsSelected);
        SelectedFontCount = InstalledFonts.Count(f => f.IsSelected) + NerdFonts.Count(f => f.IsSelected);
        OnPropertyChanged(nameof(TotalSelectedCount));
    }

    public string GetCurrentStepName()
    {
        if (CurrentStepIndex >= 0 && CurrentStepIndex < _stepKeys.Count)
            return _stepKeys[CurrentStepIndex];
        return string.Empty;
    }

    public bool CanNavigateToStep(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _stepKeys.Count)
            return false;
        if (IsDeploying || IsComplete || HasCrashed)
            return false;

        // Always allow going backward
        if (targetIndex <= CurrentStepIndex)
            return true;

        // Forward: Config step requires a non-empty path
        var configIndex = _stepKeys.IndexOf("Config");
        if (targetIndex > configIndex && string.IsNullOrWhiteSpace(ConfigRepoPath))
            return false;

        // Forward past Config: detection must have run
        if (targetIndex > configIndex && !_detectionRun)
            return false;

        return true;
    }
}

public sealed class DeployResultItemViewModel
{
    public string ModuleName { get; }
    public string Message { get; }
    public ResultLevel Level { get; }
    public string SourcePath { get; }
    public string TargetPath { get; }

    public DeployResultItemViewModel(DeployResult result)
    {
        ModuleName = result.ModuleName;
        Message = result.Message;
        Level = result.Level;
        SourcePath = result.SourcePath;
        TargetPath = result.TargetPath;
    }
}
