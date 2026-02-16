using NSubstitute;

using Perch.Core.Packages;
using Perch.Core.Scanner;

namespace Perch.Core.Tests.Scanner;

[TestFixture]
public sealed class VsCodeServiceTests
{
    private IProcessRunner _processRunner = null!;
    private VsCodeService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _service = new TestableVsCodeService(_processRunner);
    }

    private sealed class TestableVsCodeService(IProcessRunner runner) : VsCodeService(runner)
    {
        protected override string? FindCodePath() => "code";
    }

    [Test]
    public async Task GetInstalledExtensionsAsync_ParsesOutput()
    {
        string output = """
            dbaeumer.vscode-eslint@3.0.10
            esbenp.prettier-vscode@11.0.0
            eamodio.gitlens@15.6.0
            """;

        _processRunner.RunAsync(Arg.Any<string>(), "--list-extensions --show-versions", null, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, output, string.Empty));

        var extensions = await _service.GetInstalledExtensionsAsync();

        Assert.That(extensions, Has.Length.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(extensions[0].Id, Is.EqualTo("dbaeumer.vscode-eslint"));
            Assert.That(extensions[0].Version, Is.EqualTo("3.0.10"));
            Assert.That(extensions[1].Id, Is.EqualTo("esbenp.prettier-vscode"));
        });
    }

    [Test]
    public async Task GetInstalledExtensionsAsync_NonZeroExitCode_ReturnsEmpty()
    {
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, string.Empty, "error"));

        var extensions = await _service.GetInstalledExtensionsAsync();

        Assert.That(extensions, Is.Empty);
    }
}
