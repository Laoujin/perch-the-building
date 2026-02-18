#if DESKTOP_TESTS
using System.Runtime.Versioning;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Perch.Core.Config;
using Perch.Core.Startup;
using Perch.Core.Status;
using Perch.Core.Symlinks;
using Perch.Core.Tweaks;
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
    private IPendingChangesService _pendingChanges = null!;
    private IAppLinkService _appLinkService = null!;
    private ITweakService _tweakService = null!;
    private IStartupService _startupService = null!;
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
        _appLinkService = Substitute.For<IAppLinkService>();
        _tweakService = Substitute.For<ITweakService>();
        _startupService = Substitute.For<IStartupService>();
        _snackbarService = Substitute.For<ISnackbarService>();

        _vm = new DashboardViewModel(
            _statusService, _settingsProvider, _pendingChanges,
            _appLinkService, _tweakService, _startupService, _snackbarService);
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
                progress.Report(new StatusResult("mod1", "s1", "t1", DriftLevel.Ok, "ok"));
                progress.Report(new StatusResult("mod2", "s2", "t2", DriftLevel.Ok, "ok"));
                return 2;
            });

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.HealthPercent, Is.EqualTo(100));
            Assert.That(_vm.StatusMessage, Is.EqualTo("Everything looks good"));
            Assert.That(_vm.AttentionItems, Is.Empty);
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

    private sealed class ImmediateSyncContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
    }
}
#endif
