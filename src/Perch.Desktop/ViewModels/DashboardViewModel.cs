using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Startup;
using Perch.Core.Status;
using Perch.Core.Symlinks;
using Perch.Core.Tweaks;
using Perch.Desktop.Services;

using Wpf.Ui;

namespace Perch.Desktop.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly IStatusService _statusService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IPendingChangesService _pendingChanges;
    private readonly IAppLinkService _appLinkService;
    private readonly ITweakService _tweakService;
    private readonly IStartupService _startupService;
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
        IAppLinkService appLinkService,
        ITweakService tweakService,
        IStartupService startupService,
        ISnackbarService snackbarService)
    {
        _statusService = statusService;
        _settingsProvider = settingsProvider;
        _pendingChanges = pendingChanges;
        _appLinkService = appLinkService;
        _tweakService = tweakService;
        _startupService = startupService;
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
        var errors = new List<string>();
        var applied = 0;
        var changes = _pendingChanges.Changes.ToList();

        foreach (var change in changes.Where(c => c.Kind == PendingChangeKind.LinkApp))
        {
            try
            {
                var app = ((LinkAppChange)change).App;
                var results = await _appLinkService.LinkAppAsync(app.CatalogEntry, cancellationToken);
                if (results.Any(r => r.Level == ResultLevel.Error))
                    errors.Add($"Link {app.DisplayLabel}: {results.First(r => r.Level == ResultLevel.Error).Message}");
                else
                {
                    app.Status = Models.CardStatus.Linked;
                    applied++;
                }
            }
            catch (Exception ex) { errors.Add($"Link: {ex.Message}"); }
        }

        foreach (var change in changes.Where(c => c.Kind == PendingChangeKind.UnlinkApp))
        {
            try
            {
                var app = ((UnlinkAppChange)change).App;
                var results = await _appLinkService.UnlinkAppAsync(app.CatalogEntry, cancellationToken);
                if (results.Any(r => r.Level == ResultLevel.Error))
                    errors.Add($"Unlink {app.DisplayLabel}: {results.First(r => r.Level == ResultLevel.Error).Message}");
                else
                {
                    app.Status = Models.CardStatus.Detected;
                    applied++;
                }
            }
            catch (Exception ex) { errors.Add($"Unlink: {ex.Message}"); }
        }

        foreach (var change in changes.Where(c => c.Kind == PendingChangeKind.ApplyTweak))
        {
            try
            {
                var tweak = ((ApplyTweakChange)change).Tweak;
                _tweakService.Apply(tweak.CatalogEntry);
                applied++;
            }
            catch (Exception ex) { errors.Add($"Apply tweak: {ex.Message}"); }
        }

        foreach (var change in changes.Where(c => c.Kind == PendingChangeKind.RevertTweak))
        {
            try
            {
                var tweak = ((RevertTweakChange)change).Tweak;
                _tweakService.Revert(tweak.CatalogEntry);
                applied++;
            }
            catch (Exception ex) { errors.Add($"Revert tweak: {ex.Message}"); }
        }

        foreach (var change in changes.Where(c => c.Kind == PendingChangeKind.ToggleStartup))
        {
            try
            {
                var sc = (ToggleStartupChange)change;
                await _startupService.SetEnabledAsync(sc.Startup.Entry, sc.Enable, cancellationToken);
                sc.Startup.IsEnabled = sc.Enable;
                applied++;
            }
            catch (Exception ex) { errors.Add($"Startup: {ex.Message}"); }
        }

        IsApplying = false;

        if (errors.Count == 0)
        {
            _pendingChanges.Clear();
            _snackbarService.Show("Applied", $"{applied} change{(applied == 1 ? "" : "s")} applied successfully",
                Wpf.Ui.Controls.ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
        }
        else
        {
            _snackbarService.Show("Errors", $"{errors.Count} error{(errors.Count == 1 ? "" : "s")}: {errors[0]}",
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
