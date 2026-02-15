using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class DeployStepViewModel : WizardStepViewModel
{
    private readonly WizardState _state;

    [ObservableProperty]
    private bool _isDeploying;

    [ObservableProperty]
    private bool _deployComplete;

    [ObservableProperty]
    private string _currentPhase = string.Empty;

    [ObservableProperty]
    private int _completedPhases;

    [ObservableProperty]
    private int _totalPhases = 7;

    public ObservableCollection<DeployPhaseViewModel> Phases { get; } = [];

    public override string Title => "Deploy";
    public override int StepNumber => 11;
    public override bool CanSkip => false;
    public override bool CanGoBack => !IsDeploying;

    public DeployStepViewModel(WizardState state)
    {
        _state = state;
    }

    public void Initialize()
    {
        Phases.Clear();
        Phases.Add(new DeployPhaseViewModel("Repository Setup"));
        Phases.Add(new DeployPhaseViewModel("Installing Applications"));
        Phases.Add(new DeployPhaseViewModel("Installing Fonts"));
        Phases.Add(new DeployPhaseViewModel("Adopting Config Files"));
        Phases.Add(new DeployPhaseViewModel("Creating Symlinks"));
        Phases.Add(new DeployPhaseViewModel("Syncing VS Code Extensions"));
        Phases.Add(new DeployPhaseViewModel("Applying Windows Tweaks"));
        TotalPhases = Phases.Count;
    }

    public async Task RunDeployAsync(CancellationToken cancellationToken = default)
    {
        IsDeploying = true;
        DeployComplete = false;
        CompletedPhases = 0;

        foreach (var phase in Phases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CurrentPhase = phase.Name;
            phase.Status = PhaseStatus.InProgress;

            // Each phase will be implemented with real logic in a future iteration.
            // For now, mark as done to complete the wizard flow.
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            phase.Status = PhaseStatus.Done;
            CompletedPhases++;
        }

        IsDeploying = false;
        DeployComplete = true;
    }
}

public enum PhaseStatus
{
    Pending,
    InProgress,
    Done,
    Error,
}

public sealed partial class DeployPhaseViewModel : ViewModelBase
{
    [ObservableProperty]
    private PhaseStatus _status = PhaseStatus.Pending;

    public string Name { get; }

    public DeployPhaseViewModel(string name)
    {
        Name = name;
    }
}
