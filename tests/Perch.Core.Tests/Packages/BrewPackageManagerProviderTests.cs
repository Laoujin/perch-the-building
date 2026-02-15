using System.ComponentModel;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class BrewPackageManagerProviderTests
{
    private IProcessRunner _processRunner = null!;
    private BrewPackageManagerProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _provider = new BrewPackageManagerProvider(_processRunner);
    }

    [Test]
    public async Task ScanInstalled_BrewAvailable_ParsesPackages()
    {
        string output = "git\nnode\nwget\n";

        _processRunner.RunAsync("brew", "list --formula", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, output, ""));

        var result = await _provider.ScanInstalledAsync();

        Assert.That(result.IsAvailable, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Packages, Has.Length.EqualTo(3));
            Assert.That(result.Packages[0].Name, Is.EqualTo("git"));
            Assert.That(result.Packages[1].Name, Is.EqualTo("node"));
            Assert.That(result.Packages[2].Name, Is.EqualTo("wget"));
            Assert.That(result.Packages.All(p => p.Source == PackageManager.Brew), Is.True);
        });
    }

    [Test]
    public async Task ScanInstalled_BrewNotInstalled_ReturnsUnavailable()
    {
        _processRunner.RunAsync("brew", "list --formula", cancellationToken: Arg.Any<CancellationToken>())
            .Returns<ProcessRunResult>(_ => throw new Win32Exception("not found"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not installed"));
        });
    }

    [Test]
    public async Task ScanInstalled_BrewReturnsError_ReturnsUnavailable()
    {
        _processRunner.RunAsync("brew", "list --formula", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "error occurred"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("failed"));
        });
    }
}
