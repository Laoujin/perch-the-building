using System.Collections.Immutable;
using Perch.Core;
using Perch.Core.Backup;
using Perch.Core.Deploy;
using Perch.Core.Machines;
using Perch.Core.Modules;
using Perch.Core.Packages;
using Perch.Core.Registry;
using Perch.Core.Git;
using Perch.Core.Symlinks;
using Perch.Core.Templates;
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
    private IVscodeExtensionInstaller _vscodeExtensionInstaller = null!;
    private IPsModuleInstaller _psModuleInstaller = null!;
    private ISystemPackageInstaller _systemPackageInstaller = null!;
    private IReferenceResolver _referenceResolver = null!;
    private IVariableResolver _variableResolver = null!;
    private ICleanFilterService _cleanFilterService = null!;
    private IInstallResolver _installResolver = null!;
    private SymlinkOrchestrator _orchestrator = null!;
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
        _orchestrator = new SymlinkOrchestrator(_symlinkProvider, _backupProvider, fileLockDetector);
        _snapshotProvider = Substitute.For<ISnapshotProvider>();
        _hookRunner = Substitute.For<IHookRunner>();
        _hookRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DeployResult("", "", "", ResultLevel.Ok, "Hook completed"));
        _machineProfileService = Substitute.For<IMachineProfileService>();
        _registryProvider = Substitute.For<IRegistryProvider>();
        _globalPackageInstaller = Substitute.For<IGlobalPackageInstaller>();
        _vscodeExtensionInstaller = Substitute.For<IVscodeExtensionInstaller>();
        _psModuleInstaller = Substitute.For<IPsModuleInstaller>();
        _systemPackageInstaller = Substitute.For<ISystemPackageInstaller>();
        _systemPackageInstaller.InstallAsync(Arg.Any<string>(), Arg.Any<PackageManager>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(x => new DeployResult("system-packages", "", x.ArgAt<string>(0), ResultLevel.Ok, $"Installed {x.ArgAt<string>(0)}"));
        _referenceResolver = Substitute.For<IReferenceResolver>();
        _variableResolver = Substitute.For<IVariableResolver>();
        _cleanFilterService = Substitute.For<ICleanFilterService>();
        _cleanFilterService.SetupAsync(Arg.Any<string>(), Arg.Any<ImmutableArray<AppModule>>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<CleanFilterResult>.Empty);
        _installResolver = Substitute.For<IInstallResolver>();
        _deployService = new DeployService(_discoveryService, _orchestrator, _platformDetector, _globResolver, _snapshotProvider, _hookRunner, _machineProfileService, _registryProvider, _globalPackageInstaller, _vscodeExtensionInstaller, _psModuleInstaller, new PackageManifestParser(), new InstallManifestParser(), _installResolver, _systemPackageInstaller, new TemplateProcessor(), _referenceResolver, _variableResolver, _cleanFilterService);
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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported.Where(r => r.EventType == DeployEventType.Action).ToList(), Has.Count.GreaterThanOrEqualTo(2));
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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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
                () => _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress }, cts.Token));
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
                    new LinkEntry("file.txt", $"%PERCH_TEST_DEPLOY%{Path.DirectorySeparatorChar}output.txt", LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            var actions = _reported.Where(r => r.EventType == DeployEventType.Action).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(actions, Has.Count.EqualTo(1));
                Assert.That(actions[0].Message, Does.Contain("Skipped"));
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

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

            var actions = _reported.Where(r => r.EventType == DeployEventType.Action).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(actions, Has.Count.EqualTo(1));
                Assert.That(actions[0].Message, Does.Contain("Would"));
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

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            var actions = _reported.Where(r => r.EventType == DeployEventType.Action).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(actions, Has.Count.EqualTo(1));
                Assert.That(actions[0].Level, Is.EqualTo(ResultLevel.Warning));
                Assert.That(actions[0].Message, Does.Contain("glob"));
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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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
            var profile = new MachineProfile(ImmutableArray.Create("git"), ImmutableArray<string>.Empty, ImmutableDictionary<string, string>.Empty);
            _machineProfileService.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(profile);

            var modules = ImmutableArray.Create(
                new AppModule("git", "Git", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("steam", "Steam", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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
            var profile = new MachineProfile(ImmutableArray<string>.Empty, ImmutableArray.Create("steam"), ImmutableDictionary<string, string>.Empty);
            _machineProfileService.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(profile);

            var modules = ImmutableArray.Create(
                new AppModule("git", "Git", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("steam", "Steam", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

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

            await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

            await _globalPackageInstaller.Received(1).InstallAsync("Bun", GlobalPackageManager.Bun, "tsx", true, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_VscodeExtensions_InstallsAll()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        _vscodeExtensionInstaller.InstallAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(x => new DeployResult(x.ArgAt<string>(0), "", x.ArgAt<string>(1), ResultLevel.Ok, $"Installed {x.ArgAt<string>(1)}"));

        try
        {
            var extensions = ImmutableArray.Create("ms-dotnettools.csharp", "esbenp.prettier-vscode");
            var modules = ImmutableArray.Create(
                new AppModule("mod", "VS Code", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, VscodeExtensions: extensions));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.That(exitCode, Is.EqualTo(0));
            await _vscodeExtensionInstaller.Received(2).InstallAsync("VS Code", Arg.Any<string>(), false, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_VscodeExtensions_DryRun_PassesDryRunFlag()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        _vscodeExtensionInstaller.InstallAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(x => new DeployResult(x.ArgAt<string>(0), "", x.ArgAt<string>(1), ResultLevel.Ok, "Would run"));

        try
        {
            var extensions = ImmutableArray.Create("ms-dotnettools.csharp");
            var modules = ImmutableArray.Create(
                new AppModule("mod", "VS Code", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, VscodeExtensions: extensions));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

            await _vscodeExtensionInstaller.Received(1).InstallAsync("VS Code", "ms-dotnettools.csharp", true, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_PsModules_InstallsAll()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        _psModuleInstaller.InstallAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(x => new DeployResult(x.ArgAt<string>(0), "", x.ArgAt<string>(1), ResultLevel.Ok, $"Installed {x.ArgAt<string>(1)}"));

        try
        {
            var psModules = ImmutableArray.Create("posh-git", "PSReadLine");
            var modules = ImmutableArray.Create(
                new AppModule("mod", "PowerShell", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, PsModules: psModules));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.That(exitCode, Is.EqualTo(0));
            await _psModuleInstaller.Received(2).InstallAsync("PowerShell", Arg.Any<string>(), false, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_PsModules_DryRun_PassesDryRunFlag()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        _psModuleInstaller.InstallAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(x => new DeployResult(x.ArgAt<string>(0), "", x.ArgAt<string>(1), ResultLevel.Ok, "Would run"));

        try
        {
            var psModules = ImmutableArray.Create("posh-git");
            var modules = ImmutableArray.Create(
                new AppModule("mod", "PowerShell", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, PsModules: psModules));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

            await _psModuleInstaller.Received(1).InstallAsync("PowerShell", "posh-git", true, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_EmitsModuleDiscoveredForEligibleModules()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modAPath = Path.Combine(tempDir, "a");
        string modBPath = Path.Combine(tempDir, "b");
        Directory.CreateDirectory(modAPath);
        Directory.CreateDirectory(modBPath);

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("a", "Module A", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty),
                new AppModule("b", "Module B", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            var discovered = _reported.Where(r => r.EventType == DeployEventType.ModuleDiscovered).ToList();
            Assert.That(discovered, Has.Count.EqualTo(2));
            Assert.That(discovered[0].ModuleName, Is.EqualTo("Module A"));
            Assert.That(discovered[1].ModuleName, Is.EqualTo("Module B"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_EmitsModuleStartedAndCompletedPerModule()
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
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            var started = _reported.Where(r => r.EventType == DeployEventType.ModuleStarted).ToList();
            var completed = _reported.Where(r => r.EventType == DeployEventType.ModuleCompleted).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(started, Has.Count.EqualTo(1));
                Assert.That(completed, Has.Count.EqualTo(1));
                Assert.That(completed[0].Level, Is.EqualTo(ResultLevel.Ok));
            });

            int startedIdx = _reported.IndexOf(started[0]);
            int completedIdx = _reported.IndexOf(completed[0]);
            var actionsBetween = _reported.Skip(startedIdx + 1).Take(completedIdx - startedIdx - 1)
                .Where(r => r.EventType == DeployEventType.Action).ToList();
            Assert.That(actionsBetween, Has.Count.GreaterThanOrEqualTo(1));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_EmitsModuleSkippedForFilteredModules()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            _platformDetector.CurrentPlatform.Returns(Platform.Windows);
            var modules = ImmutableArray.Create(
                new AppModule("disabled", "Disabled", false, Path.Combine(tempDir, "a"), ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty),
                new AppModule("linux", "Linux Only", true, Path.Combine(tempDir, "b"), ImmutableArray.Create(Platform.Linux), ImmutableArray<LinkEntry>.Empty));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            var skipped = _reported.Where(r => r.EventType == DeployEventType.ModuleSkipped).ToList();
            Assert.That(skipped, Has.Count.EqualTo(2));
            Assert.That(skipped[0].Message, Does.Contain("disabled"));
            Assert.That(skipped[1].Message, Does.Contain("not for Windows"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ModuleCompletedCarriesErrorLevel_WhenModuleHadErrors()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(tempDir, "nonexistent", "sub", "a.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            var completed = _reported.Where(r => r.EventType == DeployEventType.ModuleCompleted).ToList();
            Assert.That(completed, Has.Count.EqualTo(1));
            Assert.That(completed[0].Level, Is.EqualTo(ResultLevel.Error));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_BeforeModule_Skip_SkipsModule()
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
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            var options = new DeployOptions
            {
                Progress = _progress,
                BeforeModule = (_, _) => Task.FromResult(ModuleAction.Skip),
            };

            await _deployService.DeployAsync(tempDir, options);

            Assert.That(_reported.Any(r => r.EventType == DeployEventType.ModuleSkipped && r.Message.Contains("user")), Is.True);
            Assert.That(_reported.Any(r => r.EventType == DeployEventType.ModuleStarted), Is.False);
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_BeforeModule_Abort_StopsRemainingModules()
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
            var modules = ImmutableArray.Create(
                new AppModule("a", "Module A", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(targetDir, "a.txt"), LinkType.Symlink))),
                new AppModule("b", "Module B", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("f.txt", Path.Combine(targetDir, "b.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            var options = new DeployOptions
            {
                Progress = _progress,
                BeforeModule = (_, _) => Task.FromResult(ModuleAction.Abort),
            };

            await _deployService.DeployAsync(tempDir, options);

            Assert.That(_reported.Any(r => r.EventType == DeployEventType.ModuleStarted), Is.False);
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_BeforeModule_Proceed_ExecutesModule()
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
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            var options = new DeployOptions
            {
                Progress = _progress,
                BeforeModule = (_, _) => Task.FromResult(ModuleAction.Proceed),
            };

            await _deployService.DeployAsync(tempDir, options);

            Assert.That(_reported.Any(r => r.EventType == DeployEventType.ModuleStarted), Is.True);
            Assert.That(_reported.Any(r => r.EventType == DeployEventType.ModuleCompleted), Is.True);
            _symlinkProvider.Received(1).CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_BeforeModule_ReceivesPreviewResults()
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
                    new LinkEntry("f.txt", Path.Combine(targetDir, "f.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            IReadOnlyList<DeployResult>? capturedPreview = null;
            var options = new DeployOptions
            {
                Progress = _progress,
                BeforeModule = (_, preview) =>
                {
                    capturedPreview = preview;
                    return Task.FromResult(ModuleAction.Proceed);
                },
            };

            await _deployService.DeployAsync(tempDir, options);

            Assert.That(capturedPreview, Is.Not.Null);
            Assert.That(capturedPreview, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(capturedPreview![0].Message, Does.Contain("Would"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_BeforeModule_NotCalledForFilteredModules()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            _platformDetector.CurrentPlatform.Returns(Platform.Windows);
            var modules = ImmutableArray.Create(
                new AppModule("disabled", "Disabled", false, Path.Combine(tempDir, "a"), ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty),
                new AppModule("linux", "Linux Only", true, Path.Combine(tempDir, "b"), ImmutableArray.Create(Platform.Linux), ImmutableArray<LinkEntry>.Empty));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            bool callbackInvoked = false;
            var options = new DeployOptions
            {
                Progress = _progress,
                BeforeModule = (_, _) =>
                {
                    callbackInvoked = true;
                    return Task.FromResult(ModuleAction.Proceed);
                },
            };

            await _deployService.DeployAsync(tempDir, options);

            Assert.That(callbackInvoked, Is.False);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_PackagesYaml_InstallsChocolateyPackages()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "packages.yaml"),
                "packages:\n  - name: 7zip\n    manager: chocolatey\n  - name: git\n    manager: chocolatey\n");
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(ImmutableArray<AppModule>.Empty, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.That(exitCode, Is.EqualTo(0));
            await _systemPackageInstaller.Received(2).InstallAsync(Arg.Any<string>(), PackageManager.Chocolatey, false, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_PackagesYaml_DryRun_PassesDryRunFlag()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "packages.yaml"),
                "packages:\n  - name: 7zip\n    manager: chocolatey\n");
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(ImmutableArray<AppModule>.Empty, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

            await _systemPackageInstaller.Received(1).InstallAsync("7zip", PackageManager.Chocolatey, true, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_MissingPackagesYaml_NoError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(ImmutableArray<AppModule>.Empty, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.That(exitCode, Is.EqualTo(0));
            await _systemPackageInstaller.DidNotReceive().InstallAsync(Arg.Any<string>(), Arg.Any<PackageManager>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_PackagesYaml_NonPlatformPackages_Skipped()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            _platformDetector.CurrentPlatform.Returns(Platform.Windows);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "packages.yaml"),
                "packages:\n  - name: curl\n    manager: apt\n  - name: wget\n    manager: brew\n  - name: 7zip\n    manager: chocolatey\n");
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(ImmutableArray<AppModule>.Empty, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            await _systemPackageInstaller.Received(1).InstallAsync("7zip", PackageManager.Chocolatey, false, Arg.Any<CancellationToken>());
            await _systemPackageInstaller.DidNotReceive().InstallAsync("curl", Arg.Any<PackageManager>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
            await _systemPackageInstaller.DidNotReceive().InstallAsync("wget", Arg.Any<PackageManager>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_TemplateSource_ResolvesAndWritesToGenerated()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            string sourceFile = Path.Combine(modulePath, "config.conf");
            File.WriteAllText(sourceFile, "token = {{op://vault/item/token}}");
            string targetFile = Path.Combine(targetDir, "config.conf");
            string generatedFile = Path.Combine(tempDir, ".generated", "mod", "config.conf");

            _referenceResolver.ResolveAsync("op://vault/item/token", Arg.Any<CancellationToken>())
                .Returns(new ReferenceResolveResult("abc123", null));

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("config.conf", targetFile, null, LinkType.Symlink, true))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(File.Exists(generatedFile), Is.True);
                Assert.That(File.ReadAllText(generatedFile), Is.EqualTo("token = abc123"));
            });
            _symlinkProvider.Received(1).CreateSymlink(targetFile, generatedFile);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_TemplateSource_DryRun_DoesNotWriteFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            string sourceFile = Path.Combine(modulePath, "config.conf");
            File.WriteAllText(sourceFile, "token = {{op://vault/item/token}}");
            string targetFile = Path.Combine(targetDir, "config.conf");
            string generatedFile = Path.Combine(tempDir, ".generated", "mod", "config.conf");

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("config.conf", targetFile, null, LinkType.Symlink, true))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(generatedFile), Is.False);
                Assert.That(_reported.Any(r => r.Message.Contains("Would resolve")), Is.True);
            });
            await _referenceResolver.DidNotReceive().ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_TemplateSource_ResolutionFailure_ReportsError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            string sourceFile = Path.Combine(modulePath, "config.conf");
            File.WriteAllText(sourceFile, "token = {{op://vault/item/token}}");
            string targetFile = Path.Combine(targetDir, "config.conf");

            _referenceResolver.ResolveAsync("op://vault/item/token", Arg.Any<CancellationToken>())
                .Returns(new ReferenceResolveResult(null, "item not found"));

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("config.conf", targetFile, null, LinkType.Symlink, true))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(File.Exists(targetFile), Is.False);
                Assert.That(_reported.Any(r => r.Level == ResultLevel.Error && r.Message.Contains("item not found")), Is.True);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_NonTemplateSource_CreatesSymlink()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            string sourceFile = Path.Combine(modulePath, "plain.conf");
            File.WriteAllText(sourceFile, "no placeholders here");
            string targetFile = Path.Combine(targetDir, "plain.conf");

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("plain.conf", targetFile, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            _symlinkProvider.Received(1).CreateSymlink(targetFile, Arg.Any<string>());
            await _referenceResolver.DidNotReceive().ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_NonTemplateWithOpContent_CreatesSymlinkWithoutResolving()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            string sourceFile = Path.Combine(modulePath, "config.conf");
            File.WriteAllText(sourceFile, "token = {{op://vault/item/token}}");
            string targetFile = Path.Combine(targetDir, "config.conf");

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("config.conf", targetFile, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            _symlinkProvider.Received(1).CreateSymlink(targetFile, Arg.Any<string>());
            await _referenceResolver.DidNotReceive().ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_TemplateWithVariables_ResolvesVariables()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            string sourceFile = Path.Combine(modulePath, "config.conf");
            File.WriteAllText(sourceFile, "host = {{machine.name}}\ntoken = {{op://vault/item/token}}");
            string targetFile = Path.Combine(targetDir, "config.conf");
            string generatedFile = Path.Combine(tempDir, ".generated", "mod", "config.conf");

            _referenceResolver.ResolveAsync("op://vault/item/token", Arg.Any<CancellationToken>())
                .Returns(new ReferenceResolveResult("abc123", null));
            _variableResolver.Resolve("machine.name", Arg.Any<IReadOnlyDictionary<string, string>?>())
                .Returns("WORKSTATION");

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("config.conf", targetFile, null, LinkType.Symlink, true))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(File.Exists(generatedFile), Is.True);
                Assert.That(File.ReadAllText(generatedFile), Is.EqualTo("host = WORKSTATION\ntoken = abc123"));
            });
            _symlinkProvider.Received(1).CreateSymlink(targetFile, generatedFile);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_TemplateWithUnknownVariable_ReportsWarningAndContinues()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            string sourceFile = Path.Combine(modulePath, "config.conf");
            File.WriteAllText(sourceFile, "value = {{unknown.var}}");
            string targetFile = Path.Combine(targetDir, "config.conf");

            _variableResolver.Resolve("unknown.var", Arg.Any<IReadOnlyDictionary<string, string>?>())
                .Returns((string?)null);

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("config.conf", targetFile, null, LinkType.Symlink, true))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported.Any(r => r.Level == ResultLevel.Warning && r.Message.Contains("unknown.var")), Is.True);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_TemplateMissingSourceFile_ReportsError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "target");
        Directory.CreateDirectory(targetDir);

        try
        {
            string targetFile = Path.Combine(targetDir, "config.conf");

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("config.conf", targetFile, null, LinkType.Symlink, true))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported.Any(r => r.Level == ResultLevel.Error && r.Message.Contains("Template source file not found")), Is.True);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_MachineProfileVariables_ExpandInTargetPaths()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "resolved");
        Directory.CreateDirectory(targetDir);

        try
        {
            var variables = ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create("config_dir", targetDir),
            });
            var profile = new MachineProfile(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, variables);
            _machineProfileService.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(profile);

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", $"%config_dir%{Path.DirectorySeparatorChar}output.txt", LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            string expectedTarget = Path.Combine(targetDir, "output.txt");
            _symlinkProvider.Received(1).CreateSymlink(expectedTarget, Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_ModulesWithCleanFilter_CallsSetup()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var filter = new CleanFilterDefinition("mod-clean", null, ImmutableArray.Create("config.xml"),
                ImmutableArray.Create(new FilterRule("strip-xml-elements", ImmutableArray.Create("FindHistory"))));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty,
                    ImmutableArray<LinkEntry>.Empty, CleanFilter: filter));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { Progress = _progress });

            await _cleanFilterService.Received(1).SetupAsync(tempDir, modules, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DeployAsync_DryRun_SkipsCleanFilterSetup()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var filter = new CleanFilterDefinition("mod-clean", null, ImmutableArray.Create("config.xml"),
                ImmutableArray.Create(new FilterRule("strip-xml-elements", ImmutableArray.Create("FindHistory"))));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty,
                    ImmutableArray<LinkEntry>.Empty, CleanFilter: filter));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            await _deployService.DeployAsync(tempDir, new DeployOptions { DryRun = true, Progress = _progress });

            await _cleanFilterService.DidNotReceive().SetupAsync(Arg.Any<string>(), Arg.Any<ImmutableArray<AppModule>>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

}
