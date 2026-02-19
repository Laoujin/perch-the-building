using System.Runtime.Versioning;

using NSubstitute.ExceptionExtensions;

using Perch.Core.Catalog;
using Perch.Desktop.Models;
using Perch.Desktop.Services;
using Perch.Desktop.ViewModels;

using Wpf.Ui;

namespace Perch.Desktop.Tests;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class MainWindowViewModelTests
{
    private INavigationService _navigationService = null!;
    private PendingChangesService _pendingChanges = null!;
    private IApplyChangesService _applyChangesService = null!;
    private ISnackbarService _snackbarService = null!;
    private MainWindowViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _navigationService = Substitute.For<INavigationService>();
        _pendingChanges = new PendingChangesService();
        _applyChangesService = Substitute.For<IApplyChangesService>();
        _snackbarService = Substitute.For<ISnackbarService>();

        _vm = new MainWindowViewModel(
            _navigationService, _pendingChanges,
            _applyChangesService, _snackbarService);
    }

    [Test]
    public void InitialState_NoPendingChanges()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.PendingChangeCount, Is.Zero);
            Assert.That(_vm.HasPendingChanges, Is.False);
            Assert.That(_vm.IsDeploying, Is.False);
        });
    }

    [Test]
    public void PendingChangesPropertyChanged_UpdatesCount()
    {
        var app = CreateAppCard("app1");
        _pendingChanges.Add(new LinkAppChange(app));

        Assert.Multiple(() =>
        {
            Assert.That(_vm.PendingChangeCount, Is.EqualTo(1));
            Assert.That(_vm.HasPendingChanges, Is.True);
        });
    }

    [Test]
    public async Task DeployAsync_Success_ShowsSuccessSnackbar()
    {
        var app = CreateAppCard("app1");
        _pendingChanges.Add(new LinkAppChange(app));
        _applyChangesService.ApplyAsync(Arg.Any<CancellationToken>())
            .Returns(new ApplyChangesResult(1, []));

        await _vm.DeployCommand.ExecuteAsync(null);

        Assert.That(_vm.IsDeploying, Is.False);
        _snackbarService.Received(1).Show("Applied", Arg.Any<string>(),
            Arg.Any<Wpf.Ui.Controls.ControlAppearance>(), Arg.Any<Wpf.Ui.Controls.IconElement?>(), Arg.Any<TimeSpan>());
    }

    [Test]
    public async Task DeployAsync_WithErrors_ShowsErrorSnackbar()
    {
        var app = CreateAppCard("app1");
        _pendingChanges.Add(new LinkAppChange(app));
        _applyChangesService.ApplyAsync(Arg.Any<CancellationToken>())
            .Returns(new ApplyChangesResult(0, ["symlink failed"]));

        await _vm.DeployCommand.ExecuteAsync(null);

        Assert.That(_vm.IsDeploying, Is.False);
        _snackbarService.Received(1).Show("Errors", Arg.Any<string>(),
            Arg.Any<Wpf.Ui.Controls.ControlAppearance>(), Arg.Any<Wpf.Ui.Controls.IconElement?>(), Arg.Any<TimeSpan>());
    }

    [Test]
    public async Task DeployAsync_ServiceThrows_IsDeployingResetsToFalse()
    {
        var app = CreateAppCard("app1");
        _pendingChanges.Add(new LinkAppChange(app));
        _applyChangesService.ApplyAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("unexpected error"));

        try
        {
            await _vm.DeployCommand.ExecuteAsync(null);
        }
        catch (InvalidOperationException)
        {
            // RelayCommand may or may not swallow — either way we check state
        }

        Assert.That(_vm.IsDeploying, Is.False);
    }

    [Test]
    public async Task DeployAsync_NoPendingChanges_DoesNotCallService()
    {
        await _vm.DeployCommand.ExecuteAsync(null);

        await _applyChangesService.DidNotReceive().ApplyAsync(Arg.Any<CancellationToken>());
        Assert.That(_vm.IsDeploying, Is.False);
    }

    [Test]
    public void ClearPendingChanges_ClearsAll()
    {
        _pendingChanges.Add(new LinkAppChange(CreateAppCard("app1")));
        _pendingChanges.Add(new LinkAppChange(CreateAppCard("app2")));

        _vm.ClearPendingChangesCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.PendingChangeCount, Is.Zero);
            Assert.That(_vm.HasPendingChanges, Is.False);
        });
    }

    [Test]
    public void Dispose_UnsubscribesFromPendingChanges()
    {
        _vm.Dispose();

        _pendingChanges.Add(new LinkAppChange(CreateAppCard("after-dispose")));

        Assert.That(_vm.PendingChangeCount, Is.Zero);
    }

    private static AppCardModel CreateAppCard(string id)
    {
        var entry = new CatalogEntry(id, id, null, "test", [], null, null, null, null, null, null);
        return new AppCardModel(entry, CardTier.YourApps, CardStatus.Detected);
    }
}
