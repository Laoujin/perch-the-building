using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class ReviewStepViewModel : WizardStepViewModel
{
    private readonly WizardState _state;

    [ObservableProperty]
    private string _repoSummary = string.Empty;

    [ObservableProperty]
    private int _dotfileCount;

    [ObservableProperty]
    private int _appsToInstallCount;

    [ObservableProperty]
    private int _configsToAdoptCount;

    [ObservableProperty]
    private int _fontsToInstallCount;

    [ObservableProperty]
    private int _extensionsToSyncCount;

    [ObservableProperty]
    private int _tweaksToApplyCount;

    [ObservableProperty]
    private string _machineName = string.Empty;

    public override string Title => "Review";
    public override int StepNumber => 10;
    public override bool CanSkip => false;

    public ReviewStepViewModel(WizardState state)
    {
        _state = state;
    }

    public void Refresh()
    {
        RepoSummary = _state.RepoMode switch
        {
            RepoSetupMode.StartFresh => $"New repository at {_state.RepoPath}",
            RepoSetupMode.CloneFromGitHub => $"Clone {_state.CloneUrl} to {_state.RepoPath}",
            RepoSetupMode.LinkExisting => $"Link existing repo at {_state.RepoPath}",
            RepoSetupMode.LocalOnly => $"Local only at {_state.RepoPath}",
            _ => string.Empty,
        };

        DotfileCount = _state.SelectedDotfiles.Count;
        AppsToInstallCount = _state.AppsToInstall.Count;
        ConfigsToAdoptCount = _state.ConfigsToAdopt.Count;
        FontsToInstallCount = _state.FontsToInstall.Count;
        ExtensionsToSyncCount = _state.ExtensionsToSync.Count;
        TweaksToApplyCount = _state.TweaksToApply.Count;
        MachineName = _state.MachineName;
    }
}
