using System.ComponentModel;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class NpmPackageManagerProviderTests
{
    private IProcessRunner _processRunner = null!;
    private NpmPackageManagerProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _provider = new NpmPackageManagerProvider(_processRunner);
    }

    [Test]
    public async Task ScanInstalled_NpmAvailable_ParsesPackages()
    {
        string output =
            "/usr/lib\n" +
            "+-- typescript@5.3.2\n" +
            "`-- prettier@3.1.0\n";

        _processRunner.RunAsync("npm", "list -g --depth=0", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, output, ""));

        var result = await _provider.ScanInstalledAsync();

        Assert.That(result.IsAvailable, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Packages, Has.Length.EqualTo(2));
            Assert.That(result.Packages[0].Name, Is.EqualTo("typescript"));
            Assert.That(result.Packages[1].Name, Is.EqualTo("prettier"));
            Assert.That(result.Packages.All(p => p.Source == PackageManager.Npm), Is.True);
        });
    }

    [Test]
    public async Task ScanInstalled_NpmNotInstalled_ReturnsUnavailable()
    {
        _processRunner.RunAsync("npm", "list -g --depth=0", cancellationToken: Arg.Any<CancellationToken>())
            .Returns<ProcessRunResult>(_ => throw new Win32Exception("not found"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not installed"));
        });
    }

    [Test]
    public async Task ScanInstalled_NpmReturnsError_ReturnsUnavailable()
    {
        _processRunner.RunAsync("npm", "list -g --depth=0", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "error occurred"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("failed"));
        });
    }
}
