#if DESKTOP_TESTS
using System.Runtime.Versioning;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Status;
using Perch.Desktop.Models;
using Perch.Desktop.Services;
using Perch.Desktop.ViewModels;

using Wpf.Ui;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class DashboardViewModelTests
{
    private IStatusService _statusService = null!;
    private ISettingsProvider _settingsProvider = null!;
    private PendingChangesService _pendingChanges = null!;
    private IApplyChangesService _applyChangesService = null!;
    private ISnackbarService _snackbarService = null!;
    private DashboardViewModel _vm = null!;
    private SynchronizationContext? _originalContext;

    [SetUp]
    public void SetUp()
    {
        _originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new ImmediateSyncContext());

        _statusService = Substitute.For<IStatusService>();
        _settingsProvider = Substitute.For<ISettingsProvider>();
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = @"C:\config" });

        _pendingChanges = new PendingChangesService();
        _applyChangesService = Substitute.For<IApplyChangesService>();
        _snackbarService = Substitute.For<ISnackbarService>();

        _vm = new DashboardViewModel(
            _statusService, _settingsProvider, _pendingChanges,
            _applyChangesService, _snackbarService);
    }

    [TearDown]
    public void TearDown()
    {
        SynchronizationContext.SetSynchronizationContext(_originalContext);
    }

    [Test]
    public void InitialState_HealthPercentIs100()
    {
        Assert.That(_vm.HealthPercent, Is.EqualTo(100));
    }

    [Test]
    public void InitialState_StatusMessageIsChecking()
    {
        Assert.That(_vm.StatusMessage, Is.EqualTo("Checking status..."));
    }

    [Test]
    public async Task RefreshAsync_NoConfigRepo_SetsHasConfigRepoFalse()
    {
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = null });

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.HasConfigRepo, Is.False);
            Assert.That(_vm.StatusMessage, Does.Contain("No config repository"));
        });
    }

    [Test]
    public async Task RefreshAsync_AllOk_HealthPercentIs100()
    {
        _statusService.CheckAsync(Arg.Any<string>(), Arg.Any<IProgress<StatusResult>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var progress = callInfo.ArgAt<IProgress<StatusResult>>(1);
                progress.Report(new StatusResult("mod1", "s1", "t1", DriftLevel.Ok, "ok", StatusCategory.Link));
                progress.Report(new StatusResult("mod2", "s2", "t2", DriftLevel.Ok, "ok", StatusCategory.GlobalPackage));
                return 2;
            });

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.HealthPercent, Is.EqualTo(100));
            Assert.That(_vm.StatusMessage, Is.EqualTo("Everything looks good"));
            Assert.That(_vm.AttentionItems, Is.Empty);
            Assert.That(_vm.LinkedDotfilesCount, Is.EqualTo(1));
            Assert.That(_vm.LinkedAppsCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RefreshAsync_MixedCategories_SplitsBadges()
    {
        _statusService.CheckAsync(Arg.Any<string>(), Arg.Any<IProgress<StatusResult>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var progress = callInfo.ArgAt<IProgress<StatusResult>>(1);
                progress.Report(new StatusResult("dotfile1", "s", "t", DriftLevel.Ok, "ok", StatusCategory.Link));
                progress.Report(new StatusResult("dotfile2", "s", "t", DriftLevel.Ok, "ok", StatusCategory.Link));
                progress.Report(new StatusResult("app1", "s", "t", DriftLevel.Ok, "ok", StatusCategory.GlobalPackage));
                progress.Report(new StatusResult("app2", "s", "t", DriftLevel.Ok, "ok", StatusCategory.VscodeExtension));
                progress.Report(new StatusResult("app3", "s", "t", DriftLevel.Ok, "ok", StatusCategory.PsModule));
                progress.Report(new StatusResult("tweak1", "s", "t", DriftLevel.Ok, "ok", StatusCategory.Registry));
                return 6;
            });

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.LinkedCount, Is.EqualTo(6));
            Assert.That(_vm.LinkedDotfilesCount, Is.EqualTo(2));
            Assert.That(_vm.LinkedAppsCount, Is.EqualTo(3));
            Assert.That(_vm.LinkedTweaksCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RefreshAsync_MixedResults_CalculatesHealthPercent()
    {
        _statusService.CheckAsync(Arg.Any<string>(), Arg.Any<IProgress<StatusResult>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var progress = callInfo.ArgAt<IProgress<StatusResult>>(1);
                progress.Report(new StatusResult("mod1", "s1", "t1", DriftLevel.Ok, "ok"));
                progress.Report(new StatusResult("mod2", "s2", "t2", DriftLevel.Ok, "ok"));
                progress.Report(new StatusResult("mod3", "s3", "t3", DriftLevel.Missing, "missing"));
                progress.Report(new StatusResult("mod4", "s4", "t4", DriftLevel.Error, "error"));
                return 4;
            });

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.HealthPercent, Is.EqualTo(50));
    }

    [Test]
    public async Task RefreshAsync_MixedResults_PopulatesAttentionItems()
    {
        _statusService.CheckAsync(Arg.Any<string>(), Arg.Any<IProgress<StatusResult>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var progress = callInfo.ArgAt<IProgress<StatusResult>>(1);
                progress.Report(new StatusResult("mod1", "s1", "t1", DriftLevel.Ok, "ok"));
                progress.Report(new StatusResult("mod2", "s2", "t2", DriftLevel.Drift, "drift"));
                progress.Report(new StatusResult("mod3", "s3", "t3", DriftLevel.Error, "error"));
                return 3;
            });

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.AttentionItems, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RefreshAsync_SingleIssue_StatusMessageSingular()
    {
        _statusService.CheckAsync(Arg.Any<string>(), Arg.Any<IProgress<StatusResult>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var progress = callInfo.ArgAt<IProgress<StatusResult>>(1);
                progress.Report(new StatusResult("mod1", "s1", "t1", DriftLevel.Ok, "ok"));
                progress.Report(new StatusResult("mod2", "s2", "t2", DriftLevel.Missing, "missing"));
                return 2;
            });

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.StatusMessage, Is.EqualTo("1 item need attention"));
    }

    [Test]
    public async Task RefreshAsync_MultipleIssues_StatusMessagePlural()
    {
        _statusService.CheckAsync(Arg.Any<string>(), Arg.Any<IProgress<StatusResult>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var progress = callInfo.ArgAt<IProgress<StatusResult>>(1);
                progress.Report(new StatusResult("mod1", "s1", "t1", DriftLevel.Missing, "missing"));
                progress.Report(new StatusResult("mod2", "s2", "t2", DriftLevel.Error, "error"));
                return 2;
            });

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.StatusMessage, Is.EqualTo("2 items need attention"));
    }

    [Test]
    public async Task RefreshAsync_NoResults_HealthIs100()
    {
        _statusService.CheckAsync(Arg.Any<string>(), Arg.Any<IProgress<StatusResult>>(), Arg.Any<CancellationToken>())
            .Returns(0);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.HealthPercent, Is.EqualTo(100));
    }

    [Test]
    public async Task RefreshAsync_Cancellation_NoError()
    {
        _statusService.CheckAsync(Arg.Any<string>(), Arg.Any<IProgress<StatusResult>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.HasConfigRepo, Is.True);
    }

    [Test]
    public async Task RefreshAsync_StatusServiceThrows_ShowsErrorAndResetsLoading()
    {
        _statusService.CheckAsync(Arg.Any<string>(), Arg.Any<IProgress<StatusResult>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("manifest is corrupted"));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.IsLoading, Is.False);
            Assert.That(_vm.StatusMessage, Does.Contain("manifest is corrupted"));
        });
    }

    [Test]
    public void DiscardChange_RemovesSingleChange()
    {
        var app = CreateAppCard("app1");
        var change = new LinkAppChange(app);
        _pendingChanges.Add(change);
        _pendingChanges.Add(new LinkAppChange(CreateAppCard("app2")));

        _vm.DiscardChangeCommand.Execute(change);

        Assert.Multiple(() =>
        {
            Assert.That(_pendingChanges.Count, Is.EqualTo(1));
            Assert.That(_pendingChanges.Changes[0].Id, Is.EqualTo("app2"));
        });
    }

    [Test]
    public void TogglePendingChange_LinkToUnlink()
    {
        var app = CreateAppCard("app1");
        _pendingChanges.Add(new LinkAppChange(app));

        _vm.TogglePendingChangeCommand.Execute(_pendingChanges.Changes[0]);

        Assert.Multiple(() =>
        {
            Assert.That(_pendingChanges.Count, Is.EqualTo(1));
            Assert.That(_pendingChanges.Changes[0].Kind, Is.EqualTo(PendingChangeKind.UnlinkApp));
        });
    }

    [Test]
    public void TogglePendingChange_ApplyToRevert()
    {
        var tweak = CreateTweakCard("tweak1");
        _pendingChanges.Add(new ApplyTweakChange(tweak));

        _vm.TogglePendingChangeCommand.Execute(_pendingChanges.Changes[0]);

        Assert.Multiple(() =>
        {
            Assert.That(_pendingChanges.Count, Is.EqualTo(1));
            Assert.That(_pendingChanges.Changes[0].Kind, Is.EqualTo(PendingChangeKind.RevertTweak));
        });
    }

    private static AppCardModel CreateAppCard(string id)
    {
        var entry = new CatalogEntry(id, id, null, "test", [], null, null, null, null, null, null);
        return new AppCardModel(entry, CardTier.YourApps, CardStatus.Detected);
    }

    private static TweakCardModel CreateTweakCard(string id)
    {
        var entry = new TweakCatalogEntry(id, id, "test", [], null, true, [], []);
        return new TweakCardModel(entry, CardStatus.NotInstalled);
    }

    private sealed class ImmediateSyncContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
    }
}
#endif
