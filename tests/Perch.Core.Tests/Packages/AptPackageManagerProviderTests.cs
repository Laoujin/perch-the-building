using System.ComponentModel;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class AptPackageManagerProviderTests
{
    private IProcessRunner _processRunner = null!;
    private AptPackageManagerProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _provider = new AptPackageManagerProvider(_processRunner);
    }

    [Test]
    public async Task ScanInstalled_AptAvailable_ParsesPackages()
    {
        string output =
            "Listing...\n" +
            "git/jammy,now 1:2.34.1-1ubuntu1.10 amd64 [installed]\n" +
            "curl/jammy,now 7.81.0-1ubuntu1.15 amd64 [installed]\n" +
            "vim/jammy,now 2:8.2.3995-1ubuntu2.13 amd64 [installed]\n";

        _processRunner.RunAsync("apt", "list --installed", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, output, ""));

        var result = await _provider.ScanInstalledAsync();

        Assert.That(result.IsAvailable, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Packages, Has.Length.EqualTo(3));
            Assert.That(result.Packages[0].Name, Is.EqualTo("git"));
            Assert.That(result.Packages[1].Name, Is.EqualTo("curl"));
            Assert.That(result.Packages[2].Name, Is.EqualTo("vim"));
            Assert.That(result.Packages.All(p => p.Source == PackageManager.Apt), Is.True);
        });
    }

    [Test]
    public async Task ScanInstalled_AptNotInstalled_ReturnsUnavailable()
    {
        _processRunner.RunAsync("apt", "list --installed", cancellationToken: Arg.Any<CancellationToken>())
            .Returns<ProcessRunResult>(_ => throw new Win32Exception("not found"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not installed"));
        });
    }

    [Test]
    public async Task ScanInstalled_AptReturnsError_ReturnsUnavailable()
    {
        _processRunner.RunAsync("apt", "list --installed", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "error occurred"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("failed"));
        });
    }
}
