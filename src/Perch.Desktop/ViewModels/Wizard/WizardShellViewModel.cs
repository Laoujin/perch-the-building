using System.Collections.ObjectModel;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Core.Deploy;

using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class WizardShellViewModel : ViewModelBase
{
    private readonly IDeployService _deployService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IGalleryDetectionService _detectionService;

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
    private bool _isInitializing = true;

    [ObservableProperty]
    private string _configRepoPath = string.Empty;

    [ObservableProperty]
    private bool _configIsGitRepo;

    [ObservableProperty]
    private bool _configPathNotGitWarning;

    [ObservableProperty]
    private string _crashErrorMessage = string.Empty;

    [ObservableProperty]
    private string _crashStackTrace = string.Empty;

    [ObservableProperty]
    private bool _hasCrashed;

    private readonly HashSet<UserProfile> _selectedProfiles = [];

    public ObservableCollection<string> StepNames { get; } = [];

    [ObservableProperty]
    private bool _isDeveloper = true;

    [ObservableProperty]
    private bool _isPowerUser;

    [ObservableProperty]
    private bool _isGamer;

    [ObservableProperty]
    private bool _isCasual;

    public ObservableCollection<DotfileCardModel> Dotfiles { get; } = [];
    public ObservableCollection<AppCardModel> YourApps { get; } = [];
    public ObservableCollection<AppCardModel> SuggestedApps { get; } = [];
    public ObservableCollection<AppCardModel> OtherApps { get; } = [];
    public ObservableCollection<TweakCardModel> Tweaks { get; } = [];
    public ObservableCollection<DeployResultItemViewModel> DeployResults { get; } = [];

    [ObservableProperty]
    private int _selectedAppCount;

    [ObservableProperty]
    private int _selectedDotfileCount;

    [ObservableProperty]
    private int _selectedTweakCount;

    public int TotalSelectedCount => SelectedAppCount + SelectedDotfileCount + SelectedTweakCount;

    public bool ShowDotfilesStep => IsDeveloper || IsPowerUser;

    public WizardShellViewModel(
        IDeployService deployService,
        ISettingsProvider settingsProvider,
        IGalleryDetectionService detectionService)
    {
        _deployService = deployService;
        _settingsProvider = settingsProvider;
        _detectionService = detectionService;

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

        StepNames.Clear();
        StepNames.Add("Profile");
        StepNames.Add("Config");
        if (ShowDotfilesStep) StepNames.Add("Dotfiles");
        StepNames.Add("Apps");
        StepNames.Add("System Tweaks");
        StepNames.Add("Review");
        StepNames.Add("Deploy");

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
        finally
        {
            IsInitializing = false;
        }
    }

    private void ValidateConfigPath()
    {
        if (string.IsNullOrWhiteSpace(ConfigRepoPath))
        {
            ConfigIsGitRepo = false;
            ConfigPathNotGitWarning = false;
            return;
        }

        var gitDir = Path.Combine(ConfigRepoPath, ".git");
        ConfigIsGitRepo = Directory.Exists(gitDir) || File.Exists(gitDir);
        ConfigPathNotGitWarning = Directory.Exists(ConfigRepoPath) && !ConfigIsGitRepo;
    }

    partial void OnConfigRepoPathChanged(string value) => ValidateConfigPath();

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

        try
        {
            var stepName = GetCurrentStepName();

            // Save config when leaving Config step
            if (stepName == "Config")
            {
                if (string.IsNullOrWhiteSpace(ConfigRepoPath))
                    return;

                var settings = await _settingsProvider.LoadAsync(cancellationToken);
                if (!string.Equals(settings.ConfigRepoPath, ConfigRepoPath, StringComparison.Ordinal))
                    await _settingsProvider.SaveAsync(settings with { ConfigRepoPath = ConfigRepoPath }, cancellationToken);

                await RunDetectionAsync(cancellationToken);
            }

            CurrentStepIndex++;
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

    private async Task RunDetectionAsync(CancellationToken cancellationToken)
    {
        IsLoadingDetection = true;

        try
        {
            var appsTask = _detectionService.DetectAppsAsync(_selectedProfiles, cancellationToken);
            var tweaksTask = _detectionService.DetectTweaksAsync(_selectedProfiles, cancellationToken);
            var dotfilesTask = _detectionService.DetectDotfilesAsync(cancellationToken);

            await Task.WhenAll(appsTask, tweaksTask, dotfilesTask);

            var appResult = appsTask.Result;
            var tweakResult = tweaksTask.Result;
            var dotfileResult = dotfilesTask.Result;

            YourApps.Clear();
            SuggestedApps.Clear();
            OtherApps.Clear();
            Tweaks.Clear();
            Dotfiles.Clear();

            foreach (var app in appResult.YourApps) { app.IsSelected = true; YourApps.Add(app); }
            foreach (var app in appResult.Suggested) SuggestedApps.Add(app);
            foreach (var app in appResult.OtherApps) OtherApps.Add(app);
            foreach (var tweak in tweakResult) Tweaks.Add(tweak);
            foreach (var df in dotfileResult) { df.IsSelected = df.IsSymlink; Dotfiles.Add(df); }
        }
        finally
        {
            IsLoadingDetection = false;
        }

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
        OnPropertyChanged(nameof(TotalSelectedCount));
    }

    public string GetCurrentStepName()
    {
        if (CurrentStepIndex >= 0 && CurrentStepIndex < StepNames.Count)
            return StepNames[CurrentStepIndex];
        return string.Empty;
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
