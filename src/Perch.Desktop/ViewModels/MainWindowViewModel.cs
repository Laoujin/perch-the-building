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

    public MainWindowViewModel(ISystemScanner scanner, ICatalogService? catalogService = null)
    {
        _scanner = scanner;
        _catalogService = catalogService;

        _steps =
        [
            new WelcomeStepViewModel(),
            new RepoSetupStepViewModel(_state),
            new SystemScanStepViewModel(scanner, _state),
            new ProfileStepViewModel(_state),
            new DotfilesStepViewModel(_state),
            CreateAppCatalogStep(),
            CreateFontsStep(),
            new VsCodeExtensionsStepViewModel(_state),
            CreateTweaksStep(),
            new ReviewStepViewModel(_state),
            new DeployStepViewModel(_state),
            new DashboardStepViewModel(_state),
        ];

        TotalSteps = _steps.Count;
        _currentPage = _steps[0];
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
        }
    }

    private Task OnLeavingStepAsync(int _) => Task.CompletedTask;

    private async Task OnEnteringStepAsync(int index)
    {
        switch (_steps[index])
        {
            case SystemScanStepViewModel scan:
                if (!scan.ScanComplete)
                {
                    await scan.RunScanAsync().ConfigureAwait(false);
                }
                break;

            case DotfilesStepViewModel dotfiles:
                dotfiles.LoadFromScanResult();
                break;

            case AppCatalogStepViewModel catalog when catalog.Apps.Count == 0:
                await catalog.LoadCatalogAsync().ConfigureAwait(false);
                break;

            case FontsStepViewModel fonts when fonts.Fonts.Count == 0:
                await fonts.LoadFontsAsync().ConfigureAwait(false);
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

    private FontsStepViewModel CreateFontsStep() =>
        _catalogService != null
            ? new FontsStepViewModel(_catalogService, _state)
            : new FontsStepViewModel(new NoOpCatalogService(), _state);

    private WindowsTweaksStepViewModel CreateTweaksStep() =>
        _catalogService != null
            ? new WindowsTweaksStepViewModel(_catalogService, _state)
            : new WindowsTweaksStepViewModel(new NoOpCatalogService(), _state);
}
