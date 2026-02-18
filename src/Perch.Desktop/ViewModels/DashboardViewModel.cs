using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Core.Status;
using Perch.Desktop.Services;

using Wpf.Ui;

namespace Perch.Desktop.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly IStatusService _statusService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IPendingChangesService _pendingChanges;
    private readonly IApplyChangesService _applyChangesService;
    private readonly ISnackbarService _snackbarService;

    [ObservableProperty]
    private int _linkedCount;

    [ObservableProperty]
    private int _linkedAppsCount;

    [ObservableProperty]
    private int _linkedDotfilesCount;

    [ObservableProperty]
    private int _linkedTweaksCount;

    [ObservableProperty]
    private int _attentionCount;

    [ObservableProperty]
    private int _brokenCount;

    [ObservableProperty]
    private int _healthPercent = 100;

    [ObservableProperty]
    private string _statusMessage = "Checking status...";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasConfigRepo = true;

    [ObservableProperty]
    private bool _isApplying;

    public ReadOnlyObservableCollection<PendingChange> PendingChanges => _pendingChanges.Changes;
    public bool HasPendingChanges => _pendingChanges.HasChanges;
    public int PendingChangeCount => _pendingChanges.Count;
    public ObservableCollection<StatusItemViewModel> AttentionItems { get; } = [];

    public DashboardViewModel(
        IStatusService statusService,
        ISettingsProvider settingsProvider,
        IPendingChangesService pendingChanges,
        IApplyChangesService applyChangesService,
        ISnackbarService snackbarService)
    {
        _statusService = statusService;
        _settingsProvider = settingsProvider;
        _pendingChanges = pendingChanges;
        _applyChangesService = applyChangesService;
        _snackbarService = snackbarService;

        _pendingChanges.PropertyChanged += OnPendingChangesPropertyChanged;
    }

    private void OnPendingChangesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(PendingChangeCount));
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ConfigRepoPath))
        {
            HasConfigRepo = false;
            StatusMessage = "No config repository configured";
            return;
        }

        HasConfigRepo = true;
        IsLoading = true;
        AttentionItems.Clear();
        LinkedCount = 0;
        LinkedAppsCount = 0;
        LinkedDotfilesCount = 0;
        LinkedTweaksCount = 0;
        AttentionCount = 0;
        BrokenCount = 0;

        var progress = new Progress<StatusResult>(result =>
        {
            switch (result.Level)
            {
                case DriftLevel.Ok:
                    LinkedCount++;
                    switch (result.Category)
                    {
                        case StatusCategory.Link:
                            LinkedDotfilesCount++;
                            break;
                        case StatusCategory.Registry:
                            LinkedTweaksCount++;
                            break;
                        default:
                            LinkedAppsCount++;
                            break;
                    }
                    break;
                case DriftLevel.Missing:
                case DriftLevel.Drift:
                    AttentionCount++;
                    AttentionItems.Add(new StatusItemViewModel(result));
                    break;
                case DriftLevel.Error:
                    BrokenCount++;
                    AttentionItems.Add(new StatusItemViewModel(result));
                    break;
            }
        });

        try
        {
            await _statusService.CheckAsync(settings.ConfigRepoPath, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to check status: {ex.Message}";
            IsLoading = false;
            return;
        }

        var total = LinkedCount + AttentionCount + BrokenCount;
        HealthPercent = total > 0 ? (int)(LinkedCount * 100.0 / total) : 100;

        var issues = AttentionCount + BrokenCount;
        StatusMessage = issues == 0
            ? $"Everything looks good"
            : $"{issues} item{(issues == 1 ? "" : "s")} need attention";

        IsLoading = false;
    }

    [RelayCommand]
    private async Task ApplyAllAsync(CancellationToken cancellationToken)
    {
        if (!_pendingChanges.HasChanges)
            return;

        IsApplying = true;
        var result = await _applyChangesService.ApplyAsync(cancellationToken);
        IsApplying = false;

        if (result.Success)
        {
            _snackbarService.Show("Applied", $"{result.Applied} change{(result.Applied == 1 ? "" : "s")} applied successfully",
                Wpf.Ui.Controls.ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
        }
        else
        {
            _snackbarService.Show("Errors", $"{result.Errors.Count} error{(result.Errors.Count == 1 ? "" : "s")}: {result.Errors[0]}",
                Wpf.Ui.Controls.ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private void DiscardAll()
    {
        _pendingChanges.Clear();
    }

    [RelayCommand]
    private void DiscardChange(PendingChange change)
    {
        _pendingChanges.Remove(change.Id, change.Kind);
    }

    [RelayCommand]
    private void TogglePendingChange(PendingChange change)
    {
        _pendingChanges.Remove(change.Id, change.Kind);

        PendingChange toggled = change switch
        {
            LinkAppChange c => new UnlinkAppChange(c.App),
            UnlinkAppChange c => new LinkAppChange(c.App),
            ApplyTweakChange c => new RevertTweakChange(c.Tweak),
            RevertTweakChange c => new ApplyTweakChange(c.Tweak),
            RevertTweakToCapturedChange c => new ApplyTweakChange(c.Tweak),
            ToggleStartupChange c => new ToggleStartupChange(c.Startup, !c.Enable),
            _ => change,
        };

        if (toggled != change)
            _pendingChanges.Add(toggled);
    }
}

public sealed class StatusItemViewModel
{
    public string ModuleName { get; }
    public string SourcePath { get; }
    public string TargetPath { get; }
    public DriftLevel Level { get; }
    public string Message { get; }
    public StatusCategory Category { get; }

    public string LevelDisplay => Level switch
    {
        DriftLevel.Missing => "Missing",
        DriftLevel.Drift => "Drift",
        DriftLevel.Error => "Error",
        _ => "OK",
    };

    public StatusItemViewModel(StatusResult result)
    {
        ModuleName = result.ModuleName;
        SourcePath = result.SourcePath;
        TargetPath = result.TargetPath;
        Level = result.Level;
        Message = result.Message;
        Category = result.Category;
    }
}
