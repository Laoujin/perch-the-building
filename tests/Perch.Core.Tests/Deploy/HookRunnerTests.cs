using Perch.Core.Deploy;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Deploy;

[TestFixture]
public sealed class HookRunnerTests
{
    private IProcessRunner _processRunner = null!;
    private HookRunner _hookRunner = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _hookRunner = new HookRunner(_processRunner);
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public async Task RunAsync_ScriptExists_ReturnsOk()
    {
        string scriptPath = Path.Combine(_tempDir, "setup.ps1");
        await File.WriteAllTextAsync(scriptPath, "echo hello");
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "hello", ""));

        DeployResult result = await _hookRunner.RunAsync("mymod", scriptPath, _tempDir);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            Assert.That(result.ModuleName, Is.EqualTo("mymod"));
        });
    }

    [Test]
    public async Task RunAsync_ScriptNotFound_ReturnsError()
    {
        string scriptPath = Path.Combine(_tempDir, "nonexistent.ps1");

        DeployResult result = await _hookRunner.RunAsync("mymod", scriptPath, _tempDir);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(result.Message, Does.Contain("not found"));
        });
        await _processRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_ScriptExitNonZero_ReturnsError()
    {
        string scriptPath = Path.Combine(_tempDir, "fail.ps1");
        await File.WriteAllTextAsync(scriptPath, "exit 1");
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "", "something went wrong"));

        DeployResult result = await _hookRunner.RunAsync("mymod", scriptPath, _tempDir);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(result.Message, Does.Contain("exit 1"));
            Assert.That(result.Message, Does.Contain("something went wrong"));
        });
    }

    [Test]
    public async Task RunAsync_SetsWorkingDirectoryToModulePath()
    {
        string scriptPath = Path.Combine(_tempDir, "setup.ps1");
        await File.WriteAllTextAsync(scriptPath, "echo ok");
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "", ""));

        await _hookRunner.RunAsync("mymod", scriptPath, _tempDir);

        await _processRunner.Received(1).RunAsync(Arg.Any<string>(), Arg.Any<string>(), _tempDir, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_ShellScript_UsesBash()
    {
        string scriptPath = Path.Combine(_tempDir, "setup.sh");
        await File.WriteAllTextAsync(scriptPath, "echo ok");
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "", ""));

        await _hookRunner.RunAsync("mymod", scriptPath, _tempDir);

        await _processRunner.Received(1).RunAsync("bash", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_PowerShellScript_UsesPwsh()
    {
        string scriptPath = Path.Combine(_tempDir, "setup.ps1");
        await File.WriteAllTextAsync(scriptPath, "echo ok");
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "", ""));

        await _hookRunner.RunAsync("mymod", scriptPath, _tempDir);

        await _processRunner.Received(1).RunAsync("pwsh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_UnknownExtension_UsesScriptPathDirectly()
    {
        string scriptPath = Path.Combine(_tempDir, "setup.cmd");
        await File.WriteAllTextAsync(scriptPath, "echo ok");
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "", ""));

        await _hookRunner.RunAsync("mymod", scriptPath, _tempDir);

        await _processRunner.Received(1).RunAsync(scriptPath, "", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_ExitNonZero_EmptyStderr_FallsBackToStdout()
    {
        string scriptPath = Path.Combine(_tempDir, "fail.ps1");
        await File.WriteAllTextAsync(scriptPath, "exit 1");
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(1, "stdout error info", ""));

        DeployResult result = await _hookRunner.RunAsync("mymod", scriptPath, _tempDir);

        Assert.That(result.Message, Does.Contain("stdout error info"));
    }
}
