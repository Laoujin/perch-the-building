using System.ComponentModel;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class ChocolateyPackageManagerProviderTests
{
    private IProcessRunner _processRunner = null!;
    private ChocolateyPackageManagerProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _provider = new ChocolateyPackageManagerProvider(_processRunner);
    }

    [Test]
    public async Task ScanInstalled_ChocoAvailable_ParsesPackages()
    {
        string output = """
            chocolatey 2.2.2
            git 2.42.0
            7zip 23.01
            3 packages installed.
            """;

        _processRunner.RunAsync("choco", "list", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, output, ""));

        var result = await _provider.ScanInstalledAsync();

        Assert.That(result.IsAvailable, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Packages, Has.Length.EqualTo(3));
            Assert.That(result.Packages[0].Name, Is.EqualTo("chocolatey"));
            Assert.That(result.Packages[1].Name, Is.EqualTo("git"));
            Assert.That(result.Packages[2].Name, Is.EqualTo("7zip"));
            Assert.That(result.Packages.All(p => p.Source == PackageManager.Chocolatey), Is.True);
        });
    }

    [Test]
    public async Task ScanInstalled_ChocoNotInstalled_ReturnsUnavailable()
    {
        _processRunner.RunAsync("choco", "list", cancellationToken: Arg.Any<CancellationToken>())
            .Returns<ProcessRunResult>(_ => throw new Win32Exception("not found"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not installed"));
        });
    }

    [Test]
    public async Task ScanInstalled_ChocoReturnsError_ReturnsUnavailable()
    {
        _processRunner.RunAsync("choco", "list", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "something went wrong"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("failed"));
        });
    }
}
