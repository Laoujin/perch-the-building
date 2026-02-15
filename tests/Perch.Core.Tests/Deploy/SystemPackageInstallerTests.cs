using Perch.Core.Deploy;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Deploy;

[TestFixture]
public sealed class SystemPackageInstallerTests
{
    private IProcessRunner _processRunner = null!;
    private SystemPackageInstaller _installer = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _installer = new SystemPackageInstaller(_processRunner);
    }

    [Test]
    public async Task InstallAsync_DryRun_DoesNotRunProcess()
    {
        DeployResult result = await _installer.InstallAsync("7zip", PackageManager.Chocolatey, dryRun: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            Assert.That(result.Message, Does.Contain("Would install"));
            Assert.That(result.Message, Does.Contain("choco"));
        });
        await _processRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InstallAsync_Chocolatey_Success_ReturnsOk()
    {
        _processRunner.RunAsync("choco", "install 7zip -y", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "Installed", ""));

        DeployResult result = await _installer.InstallAsync("7zip", PackageManager.Chocolatey, dryRun: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            Assert.That(result.Message, Does.Contain("Installed 7zip"));
        });
    }

    [Test]
    public async Task InstallAsync_Chocolatey_Failure_ReturnsError()
    {
        _processRunner.RunAsync("choco", "install badpkg -y", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "Package not found"));

        DeployResult result = await _installer.InstallAsync("badpkg", PackageManager.Chocolatey, dryRun: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(result.Message, Does.Contain("failed"));
            Assert.That(result.Message, Does.Contain("Package not found"));
        });
    }

    [Test]
    public async Task InstallAsync_Winget_BuildsCorrectCommand()
    {
        _processRunner.RunAsync("winget", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "OK", ""));

        await _installer.InstallAsync("Microsoft.VisualStudioCode", PackageManager.Winget, dryRun: false);

        await _processRunner.Received(1).RunAsync("winget", "install --id Microsoft.VisualStudioCode --accept-source-agreements --accept-package-agreements", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InstallAsync_Apt_BuildsCorrectCommand()
    {
        _processRunner.RunAsync("sudo", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "OK", ""));

        await _installer.InstallAsync("curl", PackageManager.Apt, dryRun: false);

        await _processRunner.Received(1).RunAsync("sudo", "apt-get install -y curl", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InstallAsync_Brew_BuildsCorrectCommand()
    {
        _processRunner.RunAsync("brew", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "OK", ""));

        await _installer.InstallAsync("wget", PackageManager.Brew, dryRun: false);

        await _processRunner.Received(1).RunAsync("brew", "install wget", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [TestCase(PackageManager.Npm)]
    [TestCase(PackageManager.VsCode)]
    public async Task InstallAsync_UnsupportedManager_SkipsWithoutRunning(PackageManager manager)
    {
        DeployResult result = await _installer.InstallAsync("some-pkg", manager, dryRun: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            Assert.That(result.Message, Does.Contain("handled elsewhere"));
        });
        await _processRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InstallAsync_Failure_FallsBackToStdout_WhenStderrEmpty()
    {
        _processRunner.RunAsync("choco", "install badpkg -y", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "stdout error info", ""));

        DeployResult result = await _installer.InstallAsync("badpkg", PackageManager.Chocolatey, dryRun: false);

        Assert.That(result.Message, Does.Contain("stdout error info"));
    }
}
