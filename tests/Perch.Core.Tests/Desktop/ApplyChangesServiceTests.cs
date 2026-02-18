#if DESKTOP_TESTS
using System.Runtime.Versioning;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Perch.Core.Catalog;
using Perch.Core.Deploy;
using Perch.Core.Startup;
using Perch.Core.Symlinks;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class ApplyChangesServiceTests
{
    private PendingChangesService _pendingChanges = null!;
    private IAppLinkService _appLinkService = null!;
    private ITweakService _tweakService = null!;
    private IStartupService _startupService = null!;
    private ApplyChangesService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _pendingChanges = new PendingChangesService();
        _appLinkService = Substitute.For<IAppLinkService>();
        _tweakService = Substitute.For<ITweakService>();
        _startupService = Substitute.For<IStartupService>();

        _sut = new ApplyChangesService(_pendingChanges, _appLinkService, _tweakService, _startupService);
    }

    [Test]
    public async Task ApplyAsync_NoChanges_ReturnsZeroApplied()
    {
        var result = await _sut.ApplyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.Zero);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Success, Is.True);
        });
    }

    [Test]
    public async Task ApplyAsync_LinkApp_CallsLinkAppAsync()
    {
        var app = CreateAppCard("app1", CardStatus.Detected);
        _pendingChanges.Add(new LinkAppChange(app));
        _appLinkService.LinkAppAsync(app.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("mod", "s", "t", ResultLevel.Ok, "ok") });

        var result = await _sut.ApplyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
            Assert.That(app.Status, Is.EqualTo(CardStatus.Linked));
        });
    }

    [Test]
    public async Task ApplyAsync_UnlinkApp_CallsUnlinkAppAsync()
    {
        var app = CreateAppCard("app1", CardStatus.Linked);
        _pendingChanges.Add(new UnlinkAppChange(app));
        _appLinkService.UnlinkAppAsync(app.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("mod", "s", "t", ResultLevel.Ok, "ok") });

        var result = await _sut.ApplyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
            Assert.That(app.Status, Is.EqualTo(CardStatus.Detected));
        });
    }

    [Test]
    public async Task ApplyAsync_LinkApp_Error_ReturnsErrorMessage()
    {
        var app = CreateAppCard("app1", CardStatus.Detected);
        _pendingChanges.Add(new LinkAppChange(app));
        _appLinkService.LinkAppAsync(app.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("mod", "s", "t", ResultLevel.Error, "symlink failed") });

        var result = await _sut.ApplyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.Zero);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("symlink failed"));
        });
    }

    [Test]
    public async Task ApplyAsync_LinkApp_Exception_ReturnsError()
    {
        var app = CreateAppCard("app1", CardStatus.Detected);
        _pendingChanges.Add(new LinkAppChange(app));
        _appLinkService.LinkAppAsync(app.CatalogEntry, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _sut.ApplyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.Zero);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("boom"));
        });
    }

    [Test]
    public async Task ApplyAsync_ApplyTweak_CallsTweakServiceApply()
    {
        var tweak = CreateTweakCard("tweak1");
        _pendingChanges.Add(new ApplyTweakChange(tweak));

        var result = await _sut.ApplyAsync();

        Assert.That(result.Applied, Is.EqualTo(1));
        _tweakService.Received(1).Apply(tweak.CatalogEntry);
    }

    [Test]
    public async Task ApplyAsync_RevertTweak_CallsTweakServiceRevert()
    {
        var tweak = CreateTweakCard("tweak1");
        _pendingChanges.Add(new RevertTweakChange(tweak));

        var result = await _sut.ApplyAsync();

        Assert.That(result.Applied, Is.EqualTo(1));
        _tweakService.Received(1).Revert(tweak.CatalogEntry);
    }

    [Test]
    public async Task ApplyAsync_RevertTweakToCaptured_CallsTweakServiceRevertToCaptured()
    {
        var tweak = CreateTweakCard("tweak1");
        _pendingChanges.Add(new RevertTweakToCapturedChange(tweak));

        var result = await _sut.ApplyAsync();

        Assert.That(result.Applied, Is.EqualTo(1));
        await _tweakService.Received(1).RevertToCapturedAsync(tweak.CatalogEntry, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyAsync_ToggleStartup_CallsStartupService()
    {
        var startup = CreateStartupCard("startup1");
        _pendingChanges.Add(new ToggleStartupChange(startup, true));

        var result = await _sut.ApplyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.EqualTo(1));
            Assert.That(startup.IsEnabled, Is.True);
        });
        await _startupService.Received(1).SetEnabledAsync(startup.Entry, true, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyAsync_Success_ClearsPendingChanges()
    {
        var app = CreateAppCard("app1", CardStatus.Detected);
        _pendingChanges.Add(new LinkAppChange(app));
        _appLinkService.LinkAppAsync(app.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("mod", "s", "t", ResultLevel.Ok, "ok") });

        await _sut.ApplyAsync();

        Assert.That(_pendingChanges.Count, Is.Zero);
    }

    [Test]
    public async Task ApplyAsync_WithErrors_DoesNotClearPendingChanges()
    {
        var app = CreateAppCard("app1", CardStatus.Detected);
        _pendingChanges.Add(new LinkAppChange(app));
        _appLinkService.LinkAppAsync(app.CatalogEntry, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.ApplyAsync();

        Assert.That(_pendingChanges.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplyAsync_SetsIsApplyingDuringExecution()
    {
        var wasApplying = false;
        var app = CreateAppCard("app1", CardStatus.Detected);
        _pendingChanges.Add(new LinkAppChange(app));
        _appLinkService.LinkAppAsync(app.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                wasApplying = _sut.IsApplying;
                return new List<DeployResult> { new("mod", "s", "t", ResultLevel.Ok, "ok") };
            });

        await _sut.ApplyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(wasApplying, Is.True);
            Assert.That(_sut.IsApplying, Is.False);
        });
    }

    [Test]
    public async Task ApplyAsync_MultipleChanges_AppliesAll()
    {
        var app = CreateAppCard("app1", CardStatus.Detected);
        var tweak = CreateTweakCard("tweak1");
        _pendingChanges.Add(new LinkAppChange(app));
        _pendingChanges.Add(new ApplyTweakChange(tweak));

        _appLinkService.LinkAppAsync(app.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("mod", "s", "t", ResultLevel.Ok, "ok") });

        var result = await _sut.ApplyAsync();

        Assert.That(result.Applied, Is.EqualTo(2));
    }

    private static AppCardModel CreateAppCard(string id, CardStatus status = CardStatus.Detected)
    {
        var entry = new CatalogEntry(id, id, null, "test", [], null, null, null, null, null, null);
        return new AppCardModel(entry, CardTier.YourApps, status);
    }

    private static TweakCardModel CreateTweakCard(string id)
    {
        var entry = new TweakCatalogEntry(id, id, "test", [], null, true, [], []);
        return new TweakCardModel(entry, CardStatus.NotInstalled);
    }

    private static StartupCardModel CreateStartupCard(string name)
    {
        var entry = new StartupEntry(name, name, "test.exe", null, StartupSource.RegistryCurrentUser, false);
        return new StartupCardModel(entry);
    }
}
#endif
