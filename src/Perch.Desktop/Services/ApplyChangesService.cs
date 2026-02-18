using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Deploy;
using Perch.Core.Startup;
using Perch.Core.Symlinks;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;

namespace Perch.Desktop.Services;

public sealed partial class ApplyChangesService : ObservableObject, IApplyChangesService
{
    private readonly IPendingChangesService _pendingChanges;
    private readonly IAppLinkService _appLinkService;
    private readonly ITweakService _tweakService;
    private readonly IStartupService _startupService;

    [ObservableProperty]
    private bool _isApplying;

    public ApplyChangesService(
        IPendingChangesService pendingChanges,
        IAppLinkService appLinkService,
        ITweakService tweakService,
        IStartupService startupService)
    {
        _pendingChanges = pendingChanges;
        _appLinkService = appLinkService;
        _tweakService = tweakService;
        _startupService = startupService;
    }

    public async Task<ApplyChangesResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (!_pendingChanges.HasChanges)
            return new ApplyChangesResult(0, []);

        IsApplying = true;
        var errors = new List<string>();
        var applied = 0;
        var changes = _pendingChanges.Changes.ToList();

        try
        {
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
                        app.Status = CardStatus.Linked;
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
                        app.Status = CardStatus.Detected;
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

            foreach (var change in changes.Where(c => c.Kind == PendingChangeKind.RevertTweakToCaptured))
            {
                try
                {
                    var tweak = ((RevertTweakToCapturedChange)change).Tweak;
                    await _tweakService.RevertToCapturedAsync(tweak.CatalogEntry, cancellationToken: cancellationToken);
                    applied++;
                }
                catch (Exception ex) { errors.Add($"Revert to captured: {ex.Message}"); }
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
        }
        finally
        {
            IsApplying = false;
        }

        if (errors.Count == 0)
            _pendingChanges.Clear();

        return new ApplyChangesResult(applied, errors);
    }
}
