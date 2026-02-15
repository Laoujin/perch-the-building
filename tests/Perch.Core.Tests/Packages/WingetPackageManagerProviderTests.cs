using System.ComponentModel;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class WingetPackageManagerProviderTests
{
    private IProcessRunner _processRunner = null!;
    private WingetPackageManagerProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _provider = new WingetPackageManagerProvider(_processRunner);
    }

    [Test]
    public async Task ScanInstalled_WingetAvailable_ParsesPackages()
    {
        string output =
            "Name                           Id                     Version\r\n" +
            "------------------------------  ---------------------  -------\r\n" +
            "Git                            Git.Git                2.42.0\r\n" +
            "7-Zip                          7zip.7zip              23.01\r\n";

        _processRunner.RunAsync("winget", "list --source winget", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, output, ""));

        var result = await _provider.ScanInstalledAsync();

        Assert.That(result.IsAvailable, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Packages, Has.Length.EqualTo(2));
            Assert.That(result.Packages[0].Name, Is.EqualTo("Git"));
            Assert.That(result.Packages[1].Name, Is.EqualTo("7-Zip"));
            Assert.That(result.Packages.All(p => p.Source == PackageManager.Winget), Is.True);
        });
    }

    [Test]
    public async Task ScanInstalled_WingetNotInstalled_ReturnsUnavailable()
    {
        _processRunner.RunAsync("winget", "list --source winget", cancellationToken: Arg.Any<CancellationToken>())
            .Returns<ProcessRunResult>(_ => throw new Win32Exception("not found"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not installed"));
        });
    }

    [Test]
    public async Task ScanInstalled_WingetReturnsError_ReturnsUnavailable()
    {
        _processRunner.RunAsync("winget", "list --source winget", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "error occurred"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("failed"));
        });
    }
}
