using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Desktop.Services;

using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Perch.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IPendingChangesService _pendingChanges;
    private readonly IApplyChangesService _applyChangesService;
    private readonly ISnackbarService _snackbarService;

    [ObservableProperty]
    private string _applicationTitle = "Perch";

    [ObservableProperty]
    private int _pendingChangeCount;

    [ObservableProperty]
    private bool _hasPendingChanges;

    [ObservableProperty]
    private bool _isDeploying;

    public MainWindowViewModel(
        INavigationService navigationService,
        IPendingChangesService pendingChanges,
        IApplyChangesService applyChangesService,
        ISnackbarService snackbarService)
    {
        _navigationService = navigationService;
        _pendingChanges = pendingChanges;
        _applyChangesService = applyChangesService;
        _snackbarService = snackbarService;
        _pendingChanges.PropertyChanged += OnPendingChangesPropertyChanged;
    }

    [RelayCommand]
    private async Task DeployAsync(CancellationToken cancellationToken)
    {
        if (!_pendingChanges.HasChanges)
            return;

        IsDeploying = true;
        var result = await _applyChangesService.ApplyAsync(cancellationToken);
        IsDeploying = false;

        if (result.Success)
        {
            _snackbarService.Show("Applied", $"{result.Applied} change{(result.Applied == 1 ? "" : "s")} applied successfully",
                ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
        }
        else
        {
            _snackbarService.Show("Errors", $"{result.Errors.Count} error{(result.Errors.Count == 1 ? "" : "s")}: {result.Errors[0]}",
                ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private void ClearPendingChanges()
    {
        _pendingChanges.Clear();
    }

    private void OnPendingChangesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPendingChangesService.Count))
            PendingChangeCount = _pendingChanges.Count;
        else if (e.PropertyName == nameof(IPendingChangesService.HasChanges))
            HasPendingChanges = _pendingChanges.HasChanges;
    }
}
