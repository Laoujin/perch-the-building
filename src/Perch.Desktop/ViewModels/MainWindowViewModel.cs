using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Catalog;
using Perch.Core.Scanner;
using Perch.Core.Wizard;

using Perch.Desktop.ViewModels.Wizard;

namespace Perch.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly WizardState _state = new();
    private readonly ISystemScanner _scanner;
    private readonly ICatalogService? _catalogService;
    private readonly List<WizardStepViewModel> _steps;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private int _currentStepIndex;

    [ObservableProperty]
    private int _totalSteps;

    [ObservableProperty]
    private bool _canGoNext = true;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private string _stepLabel = string.Empty;

    [ObservableProperty]
    private string _currentStepTitle = "Welcome";

    public List<StepHeaderItem> StepHeaders { get; } = [];

    public MainWindowViewModel(ISystemScanner scanner, ICatalogService? catalogService = null)
    {
        _scanner = scanner;
        _catalogService = catalogService;

        var scanStep = new SystemScanStepViewModel(_state);

        _steps =
        [
            new WelcomeStepViewModel(_state),
            new RepoSetupStepViewModel(_state),
            scanStep,
            new DotfilesStepViewModel(_state),
            CreateAppCatalogStep(),
            new VsCodeExtensionsStepViewModel(_state),
            CreateTweaksStep(),
            new ReviewStepViewModel(_state),
            new DeployStepViewModel(_state),
            new DashboardStepViewModel(_state),
        ];

        TotalSteps = _steps.Count;
        _currentPage = _steps[0];
        _stepLabel = $"Step 1 of {TotalSteps}";

        for (int i = 0; i < _steps.Count; i++)
        {
            StepHeaders.Add(new StepHeaderItem(i, _steps[i].Title));
        }
        StepHeaders[0].IsCurrent = true;
        StepHeaders[0].IsVisited = true;

        scanStep.BeginScan(scanner);
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        await OnLeavingStepAsync(CurrentStepIndex).ConfigureAwait(true);

        if (CurrentStepIndex < _steps.Count - 1)
        {
            CurrentStepIndex++;
            CurrentPage = _steps[CurrentStepIndex];
            CanGoBack = CurrentPage is WizardStepViewModel ws && ws.CanGoBack;
            UpdateNavigationState();
            await OnEnteringStepAsync(CurrentStepIndex).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
            CurrentPage = _steps[CurrentStepIndex];
            CanGoBack = CurrentPage is WizardStepViewModel ws && ws.CanGoBack;
            UpdateNavigationState();
        }
    }

    [RelayCommand]
    private void GoToStep(int stepIndex)
    {
        if (stepIndex >= 0 && stepIndex < _steps.Count)
        {
            CurrentStepIndex = stepIndex;
            CurrentPage = _steps[CurrentStepIndex];
            CanGoBack = CurrentPage is WizardStepViewModel ws && ws.CanGoBack;
            UpdateNavigationState();
        }
    }

    private void UpdateNavigationState()
    {
        StepLabel = $"Step {CurrentStepIndex + 1} of {TotalSteps}";
        CurrentStepTitle = _steps[CurrentStepIndex].Title;

        foreach (var header in StepHeaders)
        {
            header.IsCurrent = header.Index == CurrentStepIndex;
        }

        StepHeaders[CurrentStepIndex].IsVisited = true;
    }

    private Task OnLeavingStepAsync(int _) => Task.CompletedTask;

    private async Task OnEnteringStepAsync(int index)
    {
        switch (_steps[index])
        {
            case SystemScanStepViewModel scan:
                await scan.WaitForScanAsync().ConfigureAwait(false);
                break;

            case DotfilesStepViewModel dotfiles:
                dotfiles.LoadFromScanResult();
                break;

            case AppCatalogStepViewModel catalog when catalog.Apps.Count == 0:
                await catalog.LoadCatalogAsync().ConfigureAwait(false);
                break;

            case VsCodeExtensionsStepViewModel extensions:
                extensions.LoadFromScanResult();
                break;

            case WindowsTweaksStepViewModel tweaks when tweaks.Tweaks.Count == 0:
                await tweaks.LoadTweaksAsync().ConfigureAwait(false);
                break;

            case ReviewStepViewModel review:
                review.Refresh();
                break;

            case DeployStepViewModel deploy:
                deploy.Initialize();
                await deploy.RunDeployAsync().ConfigureAwait(false);
                break;

            case DashboardStepViewModel dashboard:
                dashboard.Refresh();
                break;
        }
    }

    private AppCatalogStepViewModel CreateAppCatalogStep() =>
        _catalogService != null
            ? new AppCatalogStepViewModel(_catalogService, _state)
            : new AppCatalogStepViewModel(new NoOpCatalogService(), _state);

    private WindowsTweaksStepViewModel CreateTweaksStep() =>
        _catalogService != null
            ? new WindowsTweaksStepViewModel(_catalogService, _state)
            : new WindowsTweaksStepViewModel(new NoOpCatalogService(), _state);
}

public sealed partial class StepHeaderItem : ViewModelBase
{
    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isVisited;

    public int Index { get; }
    public string Title { get; }

    public StepHeaderItem(int index, string title)
    {
        Index = index;
        Title = title;
    }
}
