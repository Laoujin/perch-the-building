using System.Collections.Immutable;
using Perch.Core;
using Perch.Core.Backup;
using Perch.Core.Deploy;
using Perch.Core.Machines;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Symlinks;
using NSubstitute.ReceivedExtensions;

namespace Perch.Core.Tests.Deploy;

internal sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}

[TestFixture]
public sealed class DeployServiceTests
{
    private IModuleDiscoveryService _discoveryService = null!;
    private ISymlinkProvider _symlinkProvider = null!;
    private IFileBackupProvider _backupProvider = null!;
    private IPlatformDetector _platformDetector = null!;
    private IGlobResolver _globResolver = null!;
    private ISnapshotProvider _snapshotProvider = null!;
    private IHookRunner _hookRunner = null!;
    private IMachineProfileService _machineProfileService = null!;
    private IRegistryProvider _registryProvider = null!;
    private IGlobalPackageInstaller _globalPackageInstaller = null!;
    private DeployService _deployService = null!;
    private List<DeployResult> _reported = null!;
    private IProgress<DeployResult> _progress = null!;

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
        var fileLockDetector = Substitute.For<IFileLockDetector>();
        var orchestrator = new SymlinkOrchestrator(_symlinkProvider, _backupProvider, fileLockDetector);
        _snapshotProvider = Substitute.For<ISnapshotProvider>();
        _hookRunner = Substitute.For<IHookRunner>();
        _hookRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DeployResult("", "", "", ResultLevel.Ok, "Hook completed"));
        _machineProfileService = Substitute.For<IMachineProfileService>();
        _registryProvider = Substitute.For<IRegistryProvider>();
        _globalPackageInstaller = Substitute.For<IGlobalPackageInstaller>();
        _deployService = new DeployService(_discoveryService, orchestrator, _platformDetector, _globResolver, _snapshotProvider, _hookRunner, _machineProfileService, _registryProvider, _globalPackageInstaller);
        _reported = new List<DeployResult>();
        _progress = new SynchronousProgress<DeployResult>(r => _reported.Add(r));
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
                new AppModule("a", "Module A", true, moduleAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("b", "Module B", true, moduleBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(tempDir, "nonexistent", "sub", "a.txt"), LinkType.Symlink),
                    new LinkEntry("file2.txt", Path.Combine(tempDir, "a.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

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
                new AppModule("a", "A", true, Path.Combine(tempDir, "a"), ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(tempDir, "t"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                () => _deployService.DeployAsync(tempDir, progress: _progress, cancellationToken: cts.Token));
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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", "%PERCH_TEST_DEPLOY%\\output.txt", LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress);

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
                new AppModule("vscode", "VS Code", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("settings.json", Path.Combine(targetDir, "settings.json"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress);

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
                new AppModule("linux-only", "Linux Only", true, Path.Combine(tempDir, "mod"), ImmutableArray.Create(Platform.Linux), ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(tempDir, "t"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

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
                new AppModule("win-mod", "Windows Module", true, modulePath, ImmutableArray.Create(Platform.Windows), ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress);

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
                new AppModule("all-plat", "All Platforms", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress);

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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", null, platformTargets, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress);

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
            _platformDetector.CurrentPlatform.Returns(Platform.MacOS);
            var platformTargets = ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create(Platform.Windows, @"C:\test\win.txt"),
            });
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", null, platformTargets, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("settings.json", globTarget, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress);

            _symlinkProvider.Received(2).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_DryRun_NoFilesystemChanges()
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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "file.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, dryRun: true, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Message, Does.Contain("Would"));
            });
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
            _snapshotProvider.DidNotReceive().CreateSnapshot(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_WithExistingTargets_CreatesSnapshot()
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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "file.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress);

            _snapshotProvider.Received(1).CreateSnapshot(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_DryRun_SkipsSnapshot()
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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "file.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, dryRun: true, _progress);

            _snapshotProvider.DidNotReceive().CreateSnapshot(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("settings.json", globTarget, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

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

    [Test]
    public async Task DeployAsync_PreHookFails_SkipsModuleLinks()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var hooks = new DeployHooks("./setup.ps1", null);
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "file.txt"), LinkType.Symlink)), hooks));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));
            _hookRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DeployResult("Module", "", "", ResultLevel.Error, "Hook failed"));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported.Any(r => r.Level == ResultLevel.Error), Is.True);
            });
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_PostHookFails_ReportsError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var hooks = new DeployHooks(null, "./cleanup.ps1");
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "file.txt"), LinkType.Symlink)), hooks));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            string postHookPath = Path.GetFullPath(Path.Combine(modulePath, "./cleanup.ps1"));
            _hookRunner.RunAsync("Module", postHookPath, modulePath, Arg.Any<CancellationToken>())
                .Returns(new DeployResult("Module", "", "", ResultLevel.Error, "Post hook failed"));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(_reported.Any(r => r.Level == ResultLevel.Error && r.Message.Contains("Post hook")), Is.True);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_DryRun_SkipsHooks()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var hooks = new DeployHooks("./setup.ps1", "./cleanup.ps1");
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "file.txt"), LinkType.Symlink)), hooks));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, dryRun: true, _progress);

            await _hookRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_NoHooks_ProcessesNormally()
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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "file.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

            Assert.That(exitCode, Is.EqualTo(0));
            await _hookRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            _symlinkProvider.Received(1).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_IncludeModules_SkipsExcluded()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modAPath = Path.Combine(tempDir, "git");
        string modBPath = Path.Combine(tempDir, "steam");
        Directory.CreateDirectory(modAPath);
        Directory.CreateDirectory(modBPath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var profile = new MachineProfile(ImmutableArray.Create("git"), ImmutableArray<string>.Empty);
            _machineProfileService.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(profile);

            var modules = ImmutableArray.Create(
                new AppModule("git", "Git", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("steam", "Steam", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(_reported.Any(r => r.Message.Contains("not in machine profile")), Is.True);
            _symlinkProvider.Received(1).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ExcludeModules_SkipsExcluded()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modAPath = Path.Combine(tempDir, "git");
        string modBPath = Path.Combine(tempDir, "steam");
        Directory.CreateDirectory(modAPath);
        Directory.CreateDirectory(modBPath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var profile = new MachineProfile(ImmutableArray<string>.Empty, ImmutableArray.Create("steam"));
            _machineProfileService.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(profile);

            var modules = ImmutableArray.Create(
                new AppModule("git", "Git", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("steam", "Steam", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(_reported.Any(r => r.Message.Contains("excluded by machine profile")), Is.True);
            _symlinkProvider.Received(1).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_NoProfile_ProcessesAll()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modAPath = Path.Combine(tempDir, "git");
        string modBPath = Path.Combine(tempDir, "steam");
        Directory.CreateDirectory(modAPath);
        Directory.CreateDirectory(modBPath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            _machineProfileService.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((MachineProfile?)null);

            var modules = ImmutableArray.Create(
                new AppModule("git", "Git", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("steam", "Steam", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

            Assert.That(exitCode, Is.EqualTo(0));
            _symlinkProvider.Received(2).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_RegistryEntry_SetsValue()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var registry = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\Software\Perch\Test", "Theme", 0, RegistryValueType.DWord));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink)), Registry: registry));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress);

            _registryProvider.Received(1).SetValue(@"HKCU\Software\Perch\Test", "Theme", 0, RegistryValueType.DWord);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_RegistryAlreadyCorrect_Skips()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            _registryProvider.GetValue(@"HKCU\Software\Perch\Test", "Theme").Returns(0);
            var registry = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\Software\Perch\Test", "Theme", 0, RegistryValueType.DWord));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink)), Registry: registry));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress);

            _registryProvider.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<RegistryValueType>());
            Assert.That(_reported.Any(r => r.Message.Contains("already set")), Is.True);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_DryRun_SkipsRegistryChanges()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var registry = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\Software\Perch\Test", "Theme", 0, RegistryValueType.DWord));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink)), Registry: registry));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, dryRun: true, _progress);

            _registryProvider.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<RegistryValueType>());
            _registryProvider.DidNotReceive().GetValue(Arg.Any<string>(), Arg.Any<string>());
            Assert.That(_reported.Any(r => r.Message.Contains("Would set")), Is.True);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_DisabledModule_SkipsModule()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", false, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(tempDir, "f.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(_reported.Any(r => r.Message.Contains("Skipped (disabled)")), Is.True);
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_GlobalPackages_InstallsViaInstaller()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        _globalPackageInstaller.InstallAsync(Arg.Any<string>(), Arg.Any<GlobalPackageManager>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(x => new DeployResult(x.ArgAt<string>(0), "", x.ArgAt<string>(2), ResultLevel.Ok, $"Installed {x.ArgAt<string>(2)}"));

        try
        {
            var globalPackages = new GlobalPackagesDefinition(GlobalPackageManager.Bun, ImmutableArray.Create("prettier", "tsx"));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Bun Packages", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, GlobalPackages: globalPackages));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress);

            Assert.That(exitCode, Is.EqualTo(0));
            await _globalPackageInstaller.Received(2).InstallAsync("Bun Packages", GlobalPackageManager.Bun, Arg.Any<string>(), false, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_GlobalPackages_DryRun_PassesDryRunFlag()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        _globalPackageInstaller.InstallAsync(Arg.Any<string>(), Arg.Any<GlobalPackageManager>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(x => new DeployResult(x.ArgAt<string>(0), "", x.ArgAt<string>(2), ResultLevel.Ok, "Would run"));

        try
        {
            var globalPackages = new GlobalPackagesDefinition(GlobalPackageManager.Bun, ImmutableArray.Create("tsx"));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Bun", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, GlobalPackages: globalPackages));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, dryRun: true, _progress);

            await _globalPackageInstaller.Received(1).InstallAsync("Bun", GlobalPackageManager.Bun, "tsx", true, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ConfirmYes_ProcessesModule()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var confirmation = Substitute.For<IDeployConfirmation>();
            confirmation.Confirm(Arg.Any<string>()).Returns(DeployConfirmationChoice.Yes);

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "file.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress, confirmation: confirmation);

            _symlinkProvider.Received(1).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ConfirmNo_SkipsModule()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var confirmation = Substitute.For<IDeployConfirmation>();
            confirmation.Confirm(Arg.Any<string>()).Returns(DeployConfirmationChoice.No);

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(targetDir, "file.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress, confirmation: confirmation);

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(_reported.Any(r => r.Message.Contains("user declined")), Is.True);
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ConfirmAll_SkipsRemainingPrompts()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modAPath = Path.Combine(tempDir, "a");
        string modBPath = Path.Combine(tempDir, "b");
        Directory.CreateDirectory(modAPath);
        Directory.CreateDirectory(modBPath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var confirmation = Substitute.For<IDeployConfirmation>();
            confirmation.Confirm(Arg.Any<string>()).Returns(DeployConfirmationChoice.All);

            var modules = ImmutableArray.Create(
                new AppModule("a", "A", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("b", "B", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress, confirmation: confirmation);

            confirmation.Received(1).Confirm(Arg.Any<string>());
            _symlinkProvider.Received(2).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ConfirmQuit_StopsEarly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modAPath = Path.Combine(tempDir, "a");
        string modBPath = Path.Combine(tempDir, "b");
        Directory.CreateDirectory(modAPath);
        Directory.CreateDirectory(modBPath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            var confirmation = Substitute.For<IDeployConfirmation>();
            confirmation.Confirm(Arg.Any<string>()).Returns(DeployConfirmationChoice.Quit);

            var modules = ImmutableArray.Create(
                new AppModule("a", "A", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("b", "B", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, progress: _progress, confirmation: confirmation);

            Assert.That(exitCode, Is.EqualTo(0));
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_FilteredModules_NotPrompted()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            _platformDetector.CurrentPlatform.Returns(Platform.Windows);
            var confirmation = Substitute.For<IDeployConfirmation>();

            var modules = ImmutableArray.Create(
                new AppModule("linux-only", "Linux Only", true, modulePath, ImmutableArray.Create(Platform.Linux), ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(tempDir, "t"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, progress: _progress, confirmation: confirmation);

            confirmation.DidNotReceive().Confirm(Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
