using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Perch.Desktop.Services;

public record ApplyChangesResult(int Applied, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;

    public void ShowSnackbar(ISnackbarService snackbarService)
    {
        if (Success)
        {
            snackbarService.Show("Applied",
                $"{Applied} change{(Applied == 1 ? "" : "s")} applied successfully",
                ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
        }
        else
        {
            snackbarService.Show("Errors",
                $"{Errors.Count} error{(Errors.Count == 1 ? "" : "s")}: {Errors[0]}",
                ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
        }
    }
}

public interface IApplyChangesService
{
    bool IsApplying { get; }
    Task<ApplyChangesResult> ApplyAsync(CancellationToken cancellationToken = default);
}
