using NSubstitute;

using Perch.Core;
using Perch.Core.Git;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Git;

[TestFixture]
public sealed class SubmoduleServiceTests
{
    private IProcessRunner _processRunner = null!;
    private SubmoduleService _service = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _service = new SubmoduleService(_processRunner);
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
    public async Task InitializeIfNeededAsync_NoGitmodules_ReturnsFalse()
    {
        bool result = await _service.InitializeIfNeededAsync(_tempDir);

        Assert.That(result, Is.False);
        await _processRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitializeIfNeededAsync_UninitializedSubmodule_Initializes()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".gitmodules"), "[submodule \"git\"]\n\tpath = git\n");

        _processRunner.RunAsync("git", "submodule status", _tempDir, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "-abc123 git\n", ""));
        _processRunner.RunAsync("git", "submodule update --init", _tempDir, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "", ""));

        bool result = await _service.InitializeIfNeededAsync(_tempDir);

        Assert.That(result, Is.True);
        await _processRunner.Received(1).RunAsync("git", "submodule update --init", _tempDir, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitializeIfNeededAsync_AllInitialized_SkipsInit()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".gitmodules"), "[submodule \"git\"]\n\tpath = git\n");

        _processRunner.RunAsync("git", "submodule status", _tempDir, Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, " abc123 git (heads/master)\n", ""));

        bool result = await _service.InitializeIfNeededAsync(_tempDir);

        Assert.That(result, Is.False);
        await _processRunner.DidNotReceive().RunAsync("git", "submodule update --init", _tempDir, Arg.Any<CancellationToken>());
    }
}
