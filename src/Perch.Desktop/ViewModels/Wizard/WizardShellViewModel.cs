using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Modules;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class WizardShellViewModel : ViewModelBase
{
    private readonly IDeployService _deployService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IModuleDiscoveryService _discoveryService;

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

    public ObservableCollection<string> StepNames { get; } = [];

    public ProfileSelectionViewModel ProfileSelection { get; }

    public ObservableCollection<DeployResultItemViewModel> DeployResults { get; } = [];

    public WizardShellViewModel(
        IDeployService deployService,
        ISettingsProvider settingsProvider,
        IModuleDiscoveryService discoveryService)
    {
        _deployService = deployService;
        _settingsProvider = settingsProvider;
        _discoveryService = discoveryService;

        ProfileSelection = new ProfileSelectionViewModel();

        StepNames.Add("Welcome");
        StepNames.Add("Review");
        StepNames.Add("Deploy");
    }

    public bool CanGoBack => CurrentStepIndex > 0 && !IsDeploying && !IsComplete;
    public bool CanGoNext => CurrentStepIndex < StepNames.Count - 1 && !IsDeploying && !IsComplete;
    public bool ShowDeploy => CurrentStepIndex == StepNames.Count - 1 && !IsDeploying && !IsComplete;

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowDeploy));
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CanGoBack)
            CurrentStepIndex--;
    }

    [RelayCommand]
    private void GoNext()
    {
        if (CanGoNext)
            CurrentStepIndex++;
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
            var exitCode = await _deployService.DeployAsync(
                settings.ConfigRepoPath,
                new DeployOptions { Progress = progress },
                cancellationToken);

            HasErrors = exitCode != 0 || ErrorCount > 0;
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

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowDeploy));
    }
}

public sealed partial class ProfileSelectionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDeveloper = true;

    [ObservableProperty]
    private bool _isPowerUser;

    [ObservableProperty]
    private bool _isGamer;

    [ObservableProperty]
    private bool _isCasual;
}

public sealed class DeployResultItemViewModel
{
    public string ModuleName { get; }
    public string Message { get; }
    public ResultLevel Level { get; }
    public string SourcePath { get; }
    public string TargetPath { get; }

    public string LevelDisplay => Level switch
    {
        ResultLevel.Ok => "OK",
        ResultLevel.Warning => "Warning",
        ResultLevel.Error => "Error",
        _ => "",
    };

    public DeployResultItemViewModel(DeployResult result)
    {
        ModuleName = result.ModuleName;
        Message = result.Message;
        Level = result.Level;
        SourcePath = result.SourcePath;
        TargetPath = result.TargetPath;
    }
}
