using Perch.Core.Packages;
using Perch.Core.Templates;

namespace Perch.Core.Tests.Templates;

[TestFixture]
public sealed class OnePasswordResolverTests
{
    private IProcessRunner _processRunner = null!;
    private OnePasswordResolver _resolver = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _resolver = new OnePasswordResolver(_processRunner);
    }

    [Test]
    public async Task ResolveAsync_Success_ReturnsValue()
    {
        _processRunner.RunAsync("op", "read \"op://vault/item/field\"", null, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "my-value\n", ""));

        ReferenceResolveResult result = await _resolver.ResolveAsync("op://vault/item/field");

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo("my-value"));
            Assert.That(result.Error, Is.Null);
        });
    }

    [Test]
    public async Task ResolveAsync_TrimsWhitespace()
    {
        _processRunner.RunAsync("op", "read \"op://v/i/f\"", null, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "  trimmed  \n", ""));

        ReferenceResolveResult result = await _resolver.ResolveAsync("op://v/i/f");

        Assert.That(result.Value, Is.EqualTo("trimmed"));
    }

    [Test]
    public async Task ResolveAsync_Failure_ReturnsError()
    {
        _processRunner.RunAsync("op", "read \"op://vault/item/field\"", null, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "[ERROR] item not found"));

        ReferenceResolveResult result = await _resolver.ResolveAsync("op://vault/item/field");

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error, Does.Contain("[ERROR] item not found"));
        });
    }

    [Test]
    public async Task ResolveAsync_Failure_FallsBackToStdout()
    {
        _processRunner.RunAsync("op", "read \"op://v/i/f\"", null, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "some output", ""));

        ReferenceResolveResult result = await _resolver.ResolveAsync("op://v/i/f");

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error, Does.Contain("some output"));
        });
    }

    [Test]
    public async Task ResolveAsync_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        _processRunner.RunAsync("op", Arg.Any<string>(), null, cts.Token)
            .Returns(new ProcessRunResult(0, "val", ""));

        await _resolver.ResolveAsync("op://v/i/f", cts.Token);

        await _processRunner.Received(1).RunAsync("op", Arg.Any<string>(), null, cts.Token);
    }
}
