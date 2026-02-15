namespace Perch.Core.Deploy;

public interface IDeployConfirmation
{
    DeployConfirmationChoice Confirm(string moduleName);
}

public enum DeployConfirmationChoice
{
    Yes,
    No,
    All,
    Quit,
}
