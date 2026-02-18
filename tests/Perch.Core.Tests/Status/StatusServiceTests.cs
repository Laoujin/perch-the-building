using System.Collections.Immutable;
using Perch.Core.Machines;
using Perch.Core.Modules;
using Perch.Core.Packages;
using Perch.Core.Registry;
using Perch.Core.Status;
using Perch.Core.Symlinks;
using Perch.Core.Templates;

namespace Perch.Core.Tests.Status;

[TestFixture]
public sealed class StatusServiceTests
{
    private IModuleDiscoveryService _discoveryService = null!;
    private ISymlinkProvider _symlinkProvider = null!;
    private IPlatformDetector _platformDetector = null!;
    private IGlobResolver _globResolver = null!;
    private IMachineProfileService _machineProfileService = null!;
    private IRegistryProvider _registryProvider = null!;
    private List<IPackageManagerProvider> _packageManagerProviders = null!;
    private PackageManifestParser _packageManifestParser = null!;
    private IProcessRunner _processRunner = null!;
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
        _machineProfileService = Substitute.For<IMachineProfileService>();
        _registryProvider = Substitute.For<IRegistryProvider>();
        _packageManagerProviders = [];
        _packageManifestParser = new PackageManifestParser();
        _processRunner = Substitute.For<IProcessRunner>();
        _statusService = CreateService();
        _reported = [];
        _progress = new SynchronousProgress<StatusResult>(r => _reported.Add(r));
    }

    private StatusService CreateService(IRegistryProvider? registryProvider = null) =>
        new(
            _discoveryService,
            _symlinkProvider,
            _platformDetector,
            _globResolver,
            _machineProfileService,
            registryProvider ?? _registryProvider,
            _packageManagerProviders,
            _packageManifestParser,
            _processRunner,
            new TemplateProcessor());

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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
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
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
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
                new AppModule("linux-only", "Linux Only", true, Path.Combine(tempDir, "mod"), ImmutableArray.Create(Platform.Linux), ImmutableArray.Create(
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
                new AppModule("a", "A", true, Path.Combine(tempDir, "a"), ImmutableArray<Platform>.Empty, ImmutableArray.Create(
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

    [Test]
    public async Task CheckAsync_MachineFilter_SkipsExcluded()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modAPath = Path.Combine(tempDir, "git");
        string modBPath = Path.Combine(tempDir, "steam");
        Directory.CreateDirectory(modAPath);
        Directory.CreateDirectory(modBPath);
        string targetFile = Path.Combine(tempDir, "file.txt");
        File.WriteAllText(targetFile, "content");
        string sourcePath = Path.Combine(modAPath, "file.txt");

        try
        {
            _symlinkProvider.IsSymlink(targetFile).Returns(true);
            _symlinkProvider.GetSymlinkTarget(targetFile).Returns(sourcePath);

            var profile = new MachineProfile(ImmutableArray.Create("git"), ImmutableArray<string>.Empty, ImmutableDictionary<string, string>.Empty);
            _machineProfileService.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(profile);

            var modules = ImmutableArray.Create(
                new AppModule("git", "Git", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", targetFile, LinkType.Symlink))),
                new AppModule("steam", "Steam", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", Path.Combine(tempDir, "other.txt"), LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].ModuleName, Is.EqualTo("Git"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_RegistryMatch_ReportsOk()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            _registryProvider.GetValue(@"HKCU\Software\Test", "Theme").Returns(0);
            var registry = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\Software\Test", "Theme", 0, RegistryValueType.DWord));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, Registry: registry));
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
    public async Task CheckAsync_RegistryMismatch_ReportsDrift()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            _registryProvider.GetValue(@"HKCU\Software\Test", "Theme").Returns(1);
            var registry = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\Software\Test", "Theme", 0, RegistryValueType.DWord));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, Registry: registry));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Drift));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_RegistryMissing_ReportsMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            _registryProvider.GetValue(@"HKCU\Software\Test", "Theme").Returns((object?)null);
            var registry = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\Software\Test", "Theme", 0, RegistryValueType.DWord));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, Registry: registry));
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
    public async Task CheckAsync_NonWindows_SkipsRegistry()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var statusService = CreateService(new NoOpRegistryProvider());

            var registry = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\Software\Test", "Theme", 0, RegistryValueType.DWord));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, Registry: registry));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await statusService.CheckAsync(tempDir, _progress);

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
    public async Task CheckAsync_GlobalPackagesInstalled_ReportsOk()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var npmProvider = Substitute.For<IPackageManagerProvider>();
            npmProvider.Manager.Returns(PackageManager.Npm);
            npmProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
                .Returns(new PackageManagerScanResult(true, ImmutableArray.Create(new InstalledPackage("eslint", PackageManager.Npm)), null));
            _packageManagerProviders.Add(npmProvider);
            _statusService = CreateService();

            var globalPackages = new GlobalPackagesDefinition(GlobalPackageManager.Npm, ImmutableArray.Create("eslint"));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, GlobalPackages: globalPackages));
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
    public async Task CheckAsync_GlobalPackagesMissing_ReportsMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var npmProvider = Substitute.For<IPackageManagerProvider>();
            npmProvider.Manager.Returns(PackageManager.Npm);
            npmProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
                .Returns(new PackageManagerScanResult(true, ImmutableArray<InstalledPackage>.Empty, null));
            _packageManagerProviders.Add(npmProvider);
            _statusService = CreateService();

            var globalPackages = new GlobalPackagesDefinition(GlobalPackageManager.Npm, ImmutableArray.Create("eslint"));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, GlobalPackages: globalPackages));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Missing));
                Assert.That(_reported[0].Message, Does.Contain("eslint"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_BunGlobalPackages_Skipped()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var globalPackages = new GlobalPackagesDefinition(GlobalPackageManager.Bun, ImmutableArray.Create("some-pkg"));
            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, GlobalPackages: globalPackages));
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
    public async Task CheckAsync_VscodeExtensionsInstalled_ReportsOk()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var vscodeProvider = Substitute.For<IPackageManagerProvider>();
            vscodeProvider.Manager.Returns(PackageManager.VsCode);
            vscodeProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
                .Returns(new PackageManagerScanResult(true, ImmutableArray.Create(new InstalledPackage("ms-dotnettools.csharp", PackageManager.VsCode)), null));
            _packageManagerProviders.Add(vscodeProvider);
            _statusService = CreateService();

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, VscodeExtensions: ImmutableArray.Create("ms-dotnettools.csharp")));
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
    public async Task CheckAsync_VscodeExtensionsMissing_ReportsMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            var vscodeProvider = Substitute.For<IPackageManagerProvider>();
            vscodeProvider.Manager.Returns(PackageManager.VsCode);
            vscodeProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
                .Returns(new PackageManagerScanResult(true, ImmutableArray<InstalledPackage>.Empty, null));
            _packageManagerProviders.Add(vscodeProvider);
            _statusService = CreateService();

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, VscodeExtensions: ImmutableArray.Create("ms-dotnettools.csharp")));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Missing));
                Assert.That(_reported[0].Message, Does.Contain("ms-dotnettools.csharp"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_PsModulesInstalled_ReportsOk()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            _processRunner.RunAsync("pwsh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new ProcessRunResult(0, "posh-git\nPSReadLine\n", ""));

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, PsModules: ImmutableArray.Create("posh-git")));
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
    public async Task CheckAsync_PsModulesMissing_ReportsMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            _processRunner.RunAsync("pwsh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new ProcessRunResult(0, "PSReadLine\n", ""));

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, PsModules: ImmutableArray.Create("posh-git")));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Missing));
                Assert.That(_reported[0].Message, Does.Contain("posh-git"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_PwshUnavailable_ReportsError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);

        try
        {
            _processRunner.RunAsync("pwsh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns<ProcessRunResult>(_ => throw new System.ComponentModel.Win32Exception("pwsh not found"));

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, PsModules: ImmutableArray.Create("posh-git")));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Error));
                Assert.That(_reported[0].Message, Does.Contain("pwsh"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_PwshUnavailable_SkipsSubsequentModules()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modAPath = Path.Combine(tempDir, "modA");
        string modBPath = Path.Combine(tempDir, "modB");
        Directory.CreateDirectory(modAPath);
        Directory.CreateDirectory(modBPath);

        try
        {
            _processRunner.RunAsync("pwsh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns<ProcessRunResult>(_ => throw new System.ComponentModel.Win32Exception("pwsh not found"));

            var modules = ImmutableArray.Create(
                new AppModule("modA", "ModA", true, modAPath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, PsModules: ImmutableArray.Create("posh-git")),
                new AppModule("modB", "ModB", true, modBPath, ImmutableArray<Platform>.Empty, ImmutableArray<LinkEntry>.Empty, PsModules: ImmutableArray.Create("PSReadLine")));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].ModuleName, Is.EqualTo("ModA"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_SystemPackagesInstalled_ReportsOk()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "packages.yaml"), """
            packages:
              - name: git
                manager: chocolatey
            """);

        try
        {
            var chocoProvider = Substitute.For<IPackageManagerProvider>();
            chocoProvider.Manager.Returns(PackageManager.Chocolatey);
            chocoProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
                .Returns(new PackageManagerScanResult(true, ImmutableArray.Create(new InstalledPackage("git", PackageManager.Chocolatey)), null));
            _packageManagerProviders.Add(chocoProvider);
            _statusService = CreateService();

            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(ImmutableArray<AppModule>.Empty, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Ok));
                Assert.That(_reported[0].ModuleName, Is.EqualTo("system-packages"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_SystemPackagesMissing_ReportsMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "packages.yaml"), """
            packages:
              - name: git
                manager: chocolatey
            """);

        try
        {
            var chocoProvider = Substitute.For<IPackageManagerProvider>();
            chocoProvider.Manager.Returns(PackageManager.Chocolatey);
            chocoProvider.ScanInstalledAsync(Arg.Any<CancellationToken>())
                .Returns(new PackageManagerScanResult(true, ImmutableArray<InstalledPackage>.Empty, null));
            _packageManagerProviders.Add(chocoProvider);
            _statusService = CreateService();

            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(ImmutableArray<AppModule>.Empty, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Missing));
                Assert.That(_reported[0].Message, Does.Contain("git"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_SystemPackagesNonPlatform_Skipped()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "packages.yaml"), """
            packages:
              - name: curl
                manager: apt
            """);

        try
        {
            _platformDetector.CurrentPlatform.Returns(Platform.Windows);

            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(ImmutableArray<AppModule>.Empty, ImmutableArray<string>.Empty));

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
    public async Task CheckAsync_MissingPackagesYaml_NoError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(ImmutableArray<AppModule>.Empty, ImmutableArray<string>.Empty));

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
    public async Task CheckAsync_TemplateSource_TargetExists_ReportsOk()
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
            File.WriteAllText(targetFile, "token = resolved-value");

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("config.conf", targetFile, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Ok));
                Assert.That(_reported[0].Message, Does.Contain("generated file"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_TemplateSource_TargetMissing_ReportsMissing()
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

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Missing));
                Assert.That(_reported[0].Message, Does.Contain("Generated file does not exist"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_NonTemplateSource_ChecksSymlink()
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
            File.WriteAllText(sourceFile, "no placeholders");
            string targetFile = Path.Combine(targetDir, "plain.conf");
            File.WriteAllText(targetFile, "content");

            _symlinkProvider.IsSymlink(targetFile).Returns(true);
            _symlinkProvider.GetSymlinkTarget(targetFile).Returns(sourceFile);

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("plain.conf", targetFile, LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Ok));
            });
            _symlinkProvider.Received(1).IsSymlink(targetFile);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CheckAsync_MachineProfileVariables_ExpandInTargetPaths()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string modulePath = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modulePath);
        string targetDir = Path.Combine(tempDir, "resolved");
        Directory.CreateDirectory(targetDir);
        string targetFile = Path.Combine(targetDir, "file.txt");
        File.WriteAllText(targetFile, "content");
        string sourcePath = Path.Combine(modulePath, "file.txt");

        try
        {
            _symlinkProvider.IsSymlink(targetFile).Returns(true);
            _symlinkProvider.GetSymlinkTarget(targetFile).Returns(sourcePath);

            var variables = ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create("config_dir", targetDir),
            });
            var profile = new MachineProfile(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, variables);
            _machineProfileService.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(profile);

            var modules = ImmutableArray.Create(
                new AppModule("mod", "Module", true, modulePath, ImmutableArray<Platform>.Empty, ImmutableArray.Create(
                    new LinkEntry("file.txt", $"%config_dir%{Path.DirectorySeparatorChar}file.txt", LinkType.Symlink))));
            _discoveryService.DiscoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DiscoveryResult(modules, ImmutableArray<string>.Empty));

            int exitCode = await _statusService.CheckAsync(tempDir, _progress);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(_reported, Has.Count.EqualTo(1));
                Assert.That(_reported[0].Level, Is.EqualTo(DriftLevel.Ok));
                Assert.That(_reported[0].TargetPath, Is.EqualTo(targetFile));
            });
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
