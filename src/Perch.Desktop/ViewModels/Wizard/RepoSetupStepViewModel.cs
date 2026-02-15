using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class RepoSetupStepViewModel : WizardStepViewModel
{
    private readonly WizardState _state;

    [ObservableProperty]
    private RepoSetupMode _selectedMode;

    [ObservableProperty]
    private string _repoPath = string.Empty;

    [ObservableProperty]
    private string _cloneUrl = string.Empty;

    public override string Title => "Repo Setup";
    public override int StepNumber => 2;
    public override bool CanSkip => false;

    public RepoSetupStepViewModel(WizardState state)
    {
        _state = state;
        _selectedMode = state.RepoMode;
        _repoPath = state.RepoPath ?? string.Empty;
        _cloneUrl = state.CloneUrl ?? string.Empty;
    }

    partial void OnSelectedModeChanged(RepoSetupMode value) => _state.RepoMode = value;
    partial void OnRepoPathChanged(string value) => _state.RepoPath = value;
    partial void OnCloneUrlChanged(string value) => _state.CloneUrl = value;
}
