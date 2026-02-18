#if DESKTOP_TESTS
using System.Runtime.Versioning;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Perch.Core.Config;
using Perch.Desktop.ViewModels;

using Wpf.Ui;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class SettingsViewModelTests
{
    private ISettingsProvider _settingsProvider = null!;
    private SettingsViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _settingsProvider = Substitute.For<ISettingsProvider>();
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings());

        _vm = new SettingsViewModel(_settingsProvider, Substitute.For<INavigationService>());
    }

    [Test]
    public void InitialState_IsSavingIsFalse()
    {
        Assert.That(_vm.IsSaving, Is.False);
    }

    [Test]
    public void InitialState_StatusMessageIsEmpty()
    {
        Assert.That(_vm.StatusMessage, Is.Empty);
    }

    [Test]
    public void Constructor_SetsAppVersion()
    {
        Assert.That(_vm.AppVersion, Is.Not.Empty);
    }

    [Test]
    public async Task LoadAsync_PopulatesConfigRepoPath()
    {
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = @"C:\repos\config" });

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.ConfigRepoPath, Is.EqualTo(@"C:\repos\config"));
    }

    [Test]
    public async Task LoadAsync_NullPath_SetsEmptyString()
    {
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = null });

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.ConfigRepoPath, Is.Empty);
    }

    [Test]
    public async Task SaveAsync_TrimsAndSavesPath()
    {
        _vm.ConfigRepoPath = "  C:\\repos\\config  ";

        await _vm.SaveCommand.ExecuteAsync(null);

        await _settingsProvider.Received(1).SaveAsync(
            Arg.Is<PerchSettings>(s => s.ConfigRepoPath == @"C:\repos\config"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SaveAsync_EmptyPath_SavesNullPath()
    {
        _vm.ConfigRepoPath = "   ";

        await _vm.SaveCommand.ExecuteAsync(null);

        await _settingsProvider.Received(1).SaveAsync(
            Arg.Is<PerchSettings>(s => s.ConfigRepoPath == null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SaveAsync_OnSuccess_SetsStatusMessage()
    {
        _vm.ConfigRepoPath = @"C:\repos\config";

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.That(_vm.StatusMessage, Is.EqualTo("Settings saved."));
    }

    [Test]
    public async Task SaveAsync_OnFailure_SetsErrorStatusMessage()
    {
        _settingsProvider.SaveAsync(Arg.Any<PerchSettings>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("disk full"));

        _vm.ConfigRepoPath = @"C:\repos\config";
        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.That(_vm.StatusMessage, Does.Contain("disk full"));
    }

    [Test]
    public async Task SaveAsync_SetsIsSavingDuringSave()
    {
        var isSavingDuringSave = false;
        _settingsProvider.SaveAsync(Arg.Any<PerchSettings>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isSavingDuringSave = _vm.IsSaving;
                return Task.CompletedTask;
            });

        _vm.ConfigRepoPath = @"C:\repos\config";
        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(isSavingDuringSave, Is.True);
            Assert.That(_vm.IsSaving, Is.False);
        });
    }
}
#endif
