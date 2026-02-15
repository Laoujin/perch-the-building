using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class DashboardStepViewModel : WizardStepViewModel
{
    private readonly WizardState _state;

    [ObservableProperty]
    private string _summary = string.Empty;

    public override string Title => "Dashboard";
    public override int StepNumber => 12;
    public override bool CanSkip => false;
    public override bool CanGoBack => false;

    public DashboardStepViewModel(WizardState state)
    {
        _state = state;
    }

    public void Refresh()
    {
        Summary = $"Setup complete. Managing configs at {_state.RepoPath} on {_state.MachineName}.";
    }
}
