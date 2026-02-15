using System.Collections.Immutable;
using Perch.Core.Modules;
using Perch.Core.Status;
using Perch.Core.Symlinks;

namespace Perch.Core.Tests.Status;

[TestFixture]
public sealed class StatusServiceTests
{
    private IModuleDiscoveryService _discoveryService = null!;
    private ISymlinkProvider _symlinkProvider = null!;
    private IPlatformDetector _platformDetector = null!;
    private IGlobResolver _globResolver = null!;
    private StatusService _statusService = null!;
    private List<StatusResult> _reported = null!;
    private IProgress<StatusResult> _progress = null!;

    [SetUp]
    public void SetUp()
    {
        _discoveryService = Substitute.For<IModuleDiscoveryService>();
        _symlinkProvider = Substitute.For<ISymlinkProvider>();
        _platformDetector = Substitute.For<IPlatformDetector>();
        _platformDetector.CurrentPlatform.Returns(Platform.Windows);
        _globResolver = Substitute.For<IGlobResolver>();
        _globResolver.Resolve(Arg.Any<string>()).Returns(x => new[] { x.Arg<string>() });
        _statusService = new StatusService(_discoveryService, _symlinkProvider, _platformDetector, _globResolver);
        _reported = new List<StatusResult>();
        _progress = new SynchronousProgress<StatusResult>(r => _reported.Add(r));
    }

    [Test]
    public async Task CheckAsync_AllLinksCorrect_ReturnsZero()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);
        string targetFile = Path.Combine(targetDir, "file.txt");
        File.WriteAllText(targetFile, "content");
        string sourcePath = Path.Combine(modulePath, "file.txt");

        try
        {
            _symlinkProvider.IsSymlink(targetFile).Returns(true);
            _symlinkProvider.GetSymlinkTarget(targetFile).Returns(sourcePath);

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", targetFile, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Ok));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_SymlinkRemoved_ReportsMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetFile = Path.Combine(tempDir, "nonexistent.txt");

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", targetFile, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Missing));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_ReplacedWithRegularFile_ReportsDrift()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetFile = Path.Combine(tempDir, "file.txt");
        File.WriteAllText(targetFile, "not a symlink");

        try
        {
            _symlinkProvider.IsSymlink(targetFile).Returns(false);

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", targetFile, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Drift));
                Assert.That(_reported[0].Message, Does.Contain("regular file"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_SymlinkTargetMismatch_ReportsDrift()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetFile = Path.Combine(tempDir, "file.txt");
        File.WriteAllText(targetFile, "content");

        try
        {
            _symlinkProvider.IsSymlink(targetFile).Returns(true);
            _symlinkProvider.GetSymlinkTarget(targetFile).Returns("C:\\wrong\\target.txt");

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", targetFile, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Drift));
                Assert.That(_reported[0].Message, Does.Contain("points to"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_PlatformMismatch_SkipsModule()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            _platformDetector.CurrentPlatform.Returns(Platform.Windows);
            var modules = ImmutableArray.Create(
                new AppModule("linux-only", "Linux Only", Path.Combine(tempDir, "mod"), ImmutableArray.Create(Platform.Linux), ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(tempDir, "t"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void CheckAsync_Cancellation_Throws()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("a", "A", Path.Combine(tempDir, "a"), ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(tempDir, "t"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                () => _statusService.CheckAsync(tempDir, _progress, cts.Token));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
