using Perch.Core.Scripts;

namespace Perch.Core.Tests.Scripts;

[TestFixture]
public sealed class PowerShellScriptExecutorTests
{
    private PowerShellScriptExecutor _executor = null!;

    [SetUp]
    public void SetUp()
    {
        _executor = new PowerShellScriptExecutor();
    }

    [Test]
    public async Task ExecuteAsync_SimpleEcho_ReturnsOutput()
    {
        var result = await _executor.ExecuteAsync("Write-Output 'hello'");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Output, Is.EqualTo("hello"));
            Assert.That(result.Error, Is.Null);
        });
    }

    [Test]
    public async Task ExecuteAsync_FailingScript_ReturnsFailure()
    {
        var result = await _executor.ExecuteAsync("exit 1");

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task ExecuteAsync_ErrorOutput_CapturesStdErr()
    {
        var result = await _executor.ExecuteAsync("Write-Error 'oops'; exit 1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("oops"));
        });
    }

    [Test]
    public async Task ExecuteAsync_CancellationToken_Supported()
    {
        using var cts = new CancellationTokenSource();
        var result = await _executor.ExecuteAsync("Write-Output 'ok'", cts.Token);

        Assert.That(result.Success, Is.True);
    }
}
