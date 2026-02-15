using System.Collections.Immutable;
using Perch.Core;
using Perch.Core.Backup;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Symlinks;

namespace Perch.Core.Tests.Deploy;

[TestFixture]
public sealed class DeployServiceTests
{
    private IModuleDiscoveryService _discoveryService = null!;
    private ISymlinkProvider _symlinkProvider = null!;
    private IFileBackupProvider _backupProvider = null!;
    private IPlatformDetector _platformDetector = null!;
    private IGlobResolver _globResolver = null!;
    private DeployService _deployService = null!;
    private List<DeployResult> _reported = null!;
    private Progress<DeployResult> _progress = null!;

    [SetUp]
    public void SetUp()
    {
        _discoveryService = Substitute.For<IModuleDiscoveryService>();
        _symlinkProvider = Substitute.For<ISymlinkProvider>();
        _backupProvider = Substitute.For<IFileBackupProvider>();
        _platformDetector = Substitute.For<IPlatformDetector>();
        _platformDetector.CurrentPlatform.Returns(Platform.Windows);
        _globResolver = Substitute.For<IGlobResolver>();
        _globResolver.Resolve(Arg.Any<string>()).Returns(x => new[] { x.Arg<string>() });
        var orchestrator = new SymlinkOrchestrator(_symlinkProvider, _backupProvider);
        _deployService = new DeployService(_discoveryService, orchestrator, _platformDetector, _globResolver);
        _reported = new List<DeployResult>();
        _progress = new Progress<DeployResult>(r => _reported.Add(r));
    }

    [Test]
    public async Task DeployAsync_MultipleModules_ProcessesAll()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string moduleAPath = Path.Combine(tempDir, "moduleA");
        string moduleBPath = Path.Combine(tempDir, "moduleB");
        Directory.CreateDirectory(moduleAPath);
        Directory.CreateDirectory(moduleBPath);
        string targetDir = Path.Combine(tempDir, "targets");
        Directory.CreateDirectory(targetDir);

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("a", "Module A", moduleAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("b", "Module B", moduleBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, _progress);

            await Task.Delay(50);
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported.Count, Is.GreaterThanOrEqualTo(2));
            });
            _symlinkProvider.Received(2).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_OneModuleFails_ContinuesAndReturnsError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(tempDir, "nonexistent", "sub", "a.txt"), LinkType.Symlink),
                    new LinkEntry("file2.txt", Path.Combine(tempDir, "a.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, _progress);

            await Task.Delay(50);
            Assert.That(exitCode, Is.EqualTo(1));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void DeployAsync_Cancellation_Throws()
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
                () => _deployService.DeployAsync(tempDir, _progress, cts.Token));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_EnvironmentVariableExpansion_ExpandsTarget()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "expanded");
        Directory.CreateDirectory(targetDir);
        Environment.SetEnvironmentVariable("PERCH_TEST_DEPLOY", targetDir);

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", "%PERCH_TEST_DEPLOY%\\output.txt", LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, _progress);

            await Task.Delay(50);
            string expectedTarget = Path.Combine(targetDir, "output.txt");
            _symlinkProvider.Received(1).CreateSymlink(expectedTarget, Arg.Any<string>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PERCH_TEST_DEPLOY", null);
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_SourcePathResolution_ResolvesRelativeToModule()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "vscode");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("vscode", "VS Code", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("settings.json", Path.Combine(targetDir, "settings.json"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, _progress);

            string expectedSource = Path.Combine(modulePath, "settings.json");
            _symlinkProvider.Received(1).CreateSymlink(Arg.Any<string>(), expectedSource);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ModulePlatformMismatch_SkipsModule()
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

            int exitCode = await _deployService.DeployAsync(tempDir, _progress);

            await Task.Delay(50);
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Message, Does.Contain("Skipped"));
            });
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ModulePlatformMatch_ProcessesModule()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            _platformDetector.CurrentPlatform.Returns(Platform.Windows);
            var modules = ImmutableArray.Create(
                new AppModule("win-mod", "Windows Module", modulePath, ImmutableArray.Create(Platform.Windows), ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, _progress);

            _symlinkProvider.Received(1).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ModuleNoPlatforms_ProcessesOnAllPlatforms()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("all-plat", "All Platforms", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, _progress);

            _symlinkProvider.Received(1).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_PlatformSpecificTargetMatch_UsesCorrectTarget()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            _platformDetector.CurrentPlatform.Returns(Platform.Windows);
            var platformTargets = ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create(Platform.Windows, Path.Combine(targetDir, "win.txt")),
                KeyValuePair.Create(Platform.Linux, "/home/test/lin.txt"),
            });
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", null, platformTargets, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, _progress);

            _symlinkProvider.Received(1).CreateSymlink(Path.Combine(targetDir, "win.txt"), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_PlatformSpecificTargetNoMatch_Skips()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            await Task.Delay(50);
            _reported.Clear();
            _platformDetector.CurrentPlatform.Returns(Platform.MacOS);
            var platformTargets = ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create(Platform.Windows, @"C:\test\win.txt"),
            });
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", null, platformTargets, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, _progress);

            await Task.Delay(50);
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Message, Does.Contain("Skipped"));
            });
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_GlobTarget_ResolvesAndProcessesAll()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir1 = Path.Combine(tempDir, "target1");
        string targetDir2 = Path.Combine(tempDir, "target2");
        Directory.CreateDirectory(targetDir1);
        Directory.CreateDirectory(targetDir2);

        try
        {
            string globTarget = Path.Combine(tempDir, "target*", "settings.json");
            string resolved1 = Path.Combine(targetDir1, "settings.json");
            string resolved2 = Path.Combine(targetDir2, "settings.json");
            _globResolver.Resolve(globTarget).Returns(new[] { resolved1, resolved2 });

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("settings.json", globTarget, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, _progress);

            _symlinkProvider.Received(2).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_GlobNoMatch_SkipsWithWarning()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            string globTarget = Path.Combine(tempDir, "nonexistent*", "settings.json");
            _globResolver.Resolve(globTarget).Returns(Array.Empty<string>());

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("settings.json", globTarget, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, _progress);

            await Task.Delay(50);
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(ResultLevel.Warning));
                Assert.That(_reported[0].Message, Does.Contain("glob"));
            });
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
