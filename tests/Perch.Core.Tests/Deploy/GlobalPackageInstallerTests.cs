using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Deploy;

[TestFixture]
public sealed class GlobalPackageInstallerTests
{
    private IProcessRunner _processRunner = null!;
    private GlobalPackageInstaller _installer = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _installer = new GlobalPackageInstaller(_processRunner);
    }

    [Test]
    public async Task InstallAsync_DryRun_Npm_ReturnsWouldRun()
    {
        DeployResult result = await _installer.InstallAsync("mod", GlobalPackageManager.Npm, "typescript", true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            Assert.That(result.Message, Is.EqualTo("Would run: npm install -g typescript"));
        });
        await _processRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InstallAsync_DryRun_Bun_ReturnsWouldRun()
    {
        DeployResult result = await _installer.InstallAsync("mod", GlobalPackageManager.Bun, "typescript", true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            Assert.That(result.Message, Is.EqualTo("Would run: bun add -g typescript"));
        });
    }

    [Test]
    public async Task InstallAsync_Npm_Success_ReturnsOk()
    {
        _processRunner.RunAsync("npm", "install -g typescript", null, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "added 1 package", ""));

        DeployResult result = await _installer.InstallAsync("mod", GlobalPackageManager.Npm, "typescript", false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            Assert.That(result.Message, Is.EqualTo("Installed typescript via npm"));
        });
    }

    [Test]
    public async Task InstallAsync_Bun_Success_ReturnsOk()
    {
        _processRunner.RunAsync("bun", "add -g prettier", null, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "installed prettier", ""));

        DeployResult result = await _installer.InstallAsync("mod", GlobalPackageManager.Bun, "prettier", false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            Assert.That(result.Message, Is.EqualTo("Installed prettier via bun"));
        });
    }

    [Test]
    public async Task InstallAsync_Failure_ReturnsErrorWithStderr()
    {
        _processRunner.RunAsync("npm", "install -g bad-pkg", null, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "ERR! 404 Not Found"));

        DeployResult result = await _installer.InstallAsync("mod", GlobalPackageManager.Npm, "bad-pkg", false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(result.Message, Does.Contain("exit 1"));
            Assert.That(result.Message, Does.Contain("ERR! 404 Not Found"));
        });
    }

    [Test]
    public async Task InstallAsync_Failure_FallsBackToStdoutWhenStderrEmpty()
    {
        _processRunner.RunAsync("npm", "install -g bad-pkg", null, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "something went wrong", ""));

        DeployResult result = await _installer.InstallAsync("mod", GlobalPackageManager.Npm, "bad-pkg", false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(result.Message, Does.Contain("something went wrong"));
        });
    }

    [Test]
    public async Task InstallAsync_SetsModuleNameAndPackageName()
    {
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "", ""));

        DeployResult result = await _installer.InstallAsync("my-module", GlobalPackageManager.Npm, "eslint", false);

        Assert.Multiple(() =>
        {
            Assert.That(result.ModuleName, Is.EqualTo("my-module"));
            Assert.That(result.TargetPath, Is.EqualTo("eslint"));
        });
    }
}
