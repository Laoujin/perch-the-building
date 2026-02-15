using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class WelcomeStepViewModel : WizardStepViewModel
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

    [ObservableProperty]
    private string _tagline = "Set up your machine, your way.";

    public override string Title => "Welcome";
    public override int StepNumber => 1;
    public override bool CanSkip => false;
    public override bool CanGoBack => false;

    public WelcomeStepViewModel(WizardState state)
    {
        _state = state;
        _isDeveloper = state.SelectedProfiles.HasFlag(UserProfile.Developer);
        _isCreative = state.SelectedProfiles.HasFlag(UserProfile.Creative);
        _isPowerUser = state.SelectedProfiles.HasFlag(UserProfile.PowerUser);
        _isGamer = state.SelectedProfiles.HasFlag(UserProfile.Gamer);
        _isMinimal = state.SelectedProfiles.HasFlag(UserProfile.Minimal);
        UpdateTagline();
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
        UpdateTagline();
    }

    private void UpdateTagline()
    {
        int count = 0;
        if (IsDeveloper) count++;
        if (IsCreative) count++;
        if (IsPowerUser) count++;
        if (IsGamer) count++;
        if (IsMinimal) count++;

        Tagline = count switch
        {
            0 => "Set up your machine, your way.",
            _ when count > 1 => "Your complete setup, one click away.",
            _ when IsDeveloper => "Your dev environment, anywhere.",
            _ when IsCreative => "Your creative toolkit, perfectly configured.",
            _ when IsPowerUser => "Complete control over every detail.",
            _ when IsGamer => "Optimized for performance and play.",
            _ when IsMinimal => "Clean, simple, just the essentials.",
            _ => "Set up your machine, your way.",
        };
    }
}
