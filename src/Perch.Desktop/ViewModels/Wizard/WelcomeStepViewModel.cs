namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class WelcomeStepViewModel : WizardStepViewModel
{
    public override string Title => "Welcome";
    public override int StepNumber => 1;
    public override bool CanSkip => false;
    public override bool CanGoBack => false;
}
