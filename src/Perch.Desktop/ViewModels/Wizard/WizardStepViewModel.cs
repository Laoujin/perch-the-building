namespace Perch.Desktop.ViewModels.Wizard;

public abstract class WizardStepViewModel : ViewModelBase
{
    public abstract string Title { get; }
    public abstract int StepNumber { get; }
    public virtual bool CanSkip => true;
    public virtual bool CanGoBack => StepNumber > 1;
}
