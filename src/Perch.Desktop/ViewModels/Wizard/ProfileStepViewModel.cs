using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class ProfileStepViewModel : WizardStepViewModel
{
    private readonly WizardState _state;

    [ObservableProperty]
    private bool _isDeveloper;

    [ObservableProperty]
    private bool _isCreative;

    [ObservableProperty]
    private bool _isPowerUser;

    [ObservableProperty]
    private bool _isGamer;

    [ObservableProperty]
    private bool _isMinimal;

    public override string Title => "Profile";
    public override int StepNumber => 4;

    public ProfileStepViewModel(WizardState state)
    {
        _state = state;
        _isDeveloper = state.SelectedProfiles.HasFlag(UserProfile.Developer);
        _isCreative = state.SelectedProfiles.HasFlag(UserProfile.Creative);
        _isPowerUser = state.SelectedProfiles.HasFlag(UserProfile.PowerUser);
        _isGamer = state.SelectedProfiles.HasFlag(UserProfile.Gamer);
        _isMinimal = state.SelectedProfiles.HasFlag(UserProfile.Minimal);
    }

    partial void OnIsDeveloperChanged(bool value) => UpdateProfiles();
    partial void OnIsCreativeChanged(bool value) => UpdateProfiles();
    partial void OnIsPowerUserChanged(bool value) => UpdateProfiles();
    partial void OnIsGamerChanged(bool value) => UpdateProfiles();
    partial void OnIsMinimalChanged(bool value) => UpdateProfiles();

    private void UpdateProfiles()
    {
        var profiles = UserProfile.None;
        if (IsDeveloper) profiles |= UserProfile.Developer;
        if (IsCreative) profiles |= UserProfile.Creative;
        if (IsPowerUser) profiles |= UserProfile.PowerUser;
        if (IsGamer) profiles |= UserProfile.Gamer;
        if (IsMinimal) profiles |= UserProfile.Minimal;
        _state.SelectedProfiles = profiles;
    }
}
