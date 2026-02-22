using System.Collections.Immutable;
using Perch.Core.Deploy;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Deploy;

[TestFixture]
public sealed class SystemPackageInstallerTests
{
    private IProcessRunner _processRunner = null!;
    private IInstalledAppChecker _installedAppChecker = null!;
    private SystemPackageInstaller _installer = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _installedAppChecker = Substitute.For<IInstalledAppChecker>();
        _installedAppChecker.GetInstalledPackageIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _installer = new SystemPackageInstaller(_processRunner, _installedAppChecker);
    }

    [Test]
    public async Task InstallAsync_AlreadyInstalled_SkipsInstall()
    {
        _installedAppChecker.GetInstalledPackageIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(["7zip.7zip"], StringComparer.OrdinalIgnoreCase));

        var package = new PackageDefinition("7zip.7zip", PackageManager.Winget, ["7zip.7zip"]);
        DeployResult result = await _installer.InstallAsync(package, dryRun: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Synced));
            Assert.That(result.Message, Does.Contain("Already installed"));
        });
        await _processRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InstallAsync_AlternativeIdMatch_DetectsInstalledByChocoId()
    {
        // App installed via choco shows with choco ID, but gallery prefers winget
        _installedAppChecker.GetInstalledPackageIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(["git"], StringComparer.OrdinalIgnoreCase));

        // Winget is preferred but choco "git" is also in alternative IDs
        var package = new PackageDefinition("Git.Git", PackageManager.Winget, ["Git.Git", "git"]);
        DeployResult result = await _installer.InstallAsync(package, dryRun: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Synced));
            Assert.That(result.Message, Does.Contain("Already installed"));
            Assert.That(result.Message, Does.Contain("git"));
        });
        await _processRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InstallAsync_DryRun_DoesNotRunProcess()
    {
        var package = new PackageDefinition("7zip", PackageManager.Chocolatey, ["7zip"]);
        DeployResult result = await _installer.InstallAsync(package, dryRun: true);

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

        var package = new PackageDefinition("7zip", PackageManager.Chocolatey, ["7zip"]);
        DeployResult result = await _installer.InstallAsync(package, dryRun: false);

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

        var package = new PackageDefinition("badpkg", PackageManager.Chocolatey, ["badpkg"]);
        DeployResult result = await _installer.InstallAsync(package, dryRun: false);

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

        var package = new PackageDefinition("Microsoft.VisualStudioCode", PackageManager.Winget, ["Microsoft.VisualStudioCode"]);
        await _installer.InstallAsync(package, dryRun: false);

        await _processRunner.Received(1).RunAsync("winget", "install --id Microsoft.VisualStudioCode --accept-source-agreements --accept-package-agreements", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InstallAsync_Apt_BuildsCorrectCommand()
    {
        _processRunner.RunAsync("sudo", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "OK", ""));

        var package = new PackageDefinition("curl", PackageManager.Apt, ["curl"]);
        await _installer.InstallAsync(package, dryRun: false);

        await _processRunner.Received(1).RunAsync("sudo", "apt-get install -y curl", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InstallAsync_Brew_BuildsCorrectCommand()
    {
        _processRunner.RunAsync("brew", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "OK", ""));

        var package = new PackageDefinition("wget", PackageManager.Brew, ["wget"]);
        await _installer.InstallAsync(package, dryRun: false);

        await _processRunner.Received(1).RunAsync("brew", "install wget", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [TestCase(PackageManager.Npm)]
    [TestCase(PackageManager.VsCode)]
    public async Task InstallAsync_UnsupportedManager_SkipsWithoutRunning(PackageManager manager)
    {
        var package = new PackageDefinition("some-pkg", manager, ["some-pkg"]);
        DeployResult result = await _installer.InstallAsync(package, dryRun: false);

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

        var package = new PackageDefinition("badpkg", PackageManager.Chocolatey, ["badpkg"]);
        DeployResult result = await _installer.InstallAsync(package, dryRun: false);

        Assert.That(result.Message, Does.Contain("stdout error info"));
    }
}
