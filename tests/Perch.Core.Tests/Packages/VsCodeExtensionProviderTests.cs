using System.ComponentModel;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class VsCodeExtensionProviderTests
{
    private IProcessRunner _processRunner = null!;
    private VsCodeExtensionProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _provider = new VsCodeExtensionProvider(_processRunner);
    }

    [Test]
    public async Task ScanInstalled_CodeAvailable_ParsesExtensions()
    {
        string output = "ms-dotnettools.csharp\nms-vscode.powershell\nesbenp.prettier-vscode\n";

        _processRunner.RunAsync("code", "--list-extensions", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, output, ""));

        var result = await _provider.ScanInstalledAsync();

        Assert.That(result.IsAvailable, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Packages, Has.Length.EqualTo(3));
            Assert.That(result.Packages[0].Name, Is.EqualTo("ms-dotnettools.csharp"));
            Assert.That(result.Packages[1].Name, Is.EqualTo("ms-vscode.powershell"));
            Assert.That(result.Packages[2].Name, Is.EqualTo("esbenp.prettier-vscode"));
            Assert.That(result.Packages.All(p => p.Source == PackageManager.VsCode), Is.True);
        });
    }

    [Test]
    public async Task ScanInstalled_CodeNotInstalled_ReturnsUnavailable()
    {
        _processRunner.RunAsync("code", "--list-extensions", cancellationToken: Arg.Any<CancellationToken>())
            .Returns<ProcessRunResult>(_ => throw new Win32Exception("not found"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not installed"));
        });
    }

    [Test]
    public async Task ScanInstalled_CodeReturnsError_ReturnsUnavailable()
    {
        _processRunner.RunAsync("code", "--list-extensions", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "error occurred"));

        var result = await _provider.ScanInstalledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("failed"));
        });
    }
}
