using Perch.Core.Backup;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Symlinks;

namespace Perch.Core.Tests.Symlinks;

[TestFixture]
public sealed class SymlinkOrchestratorTests
{
    private ISymlinkProvider _symlinkProvider = null!;
    private IFileBackupProvider _backupProvider = null!;
    private IFileLockDetector _fileLockDetector = null!;
    private SymlinkOrchestrator _orchestrator = null!;

    [SetUp]
    public void SetUp()
    {
        _symlinkProvider = Substitute.For<ISymlinkProvider>();
        _backupProvider = Substitute.For<IFileBackupProvider>();
        _fileLockDetector = Substitute.For<IFileLockDetector>();
        _orchestrator = new SymlinkOrchestrator(_symlinkProvider, _backupProvider, _fileLockDetector);
    }

    [Test]
    public void ProcessLink_NoTargetExists_CreatesLink()
    {
        string source = "C:\\repo\\vscode\\settings.json";
        string target = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}", "settings.json");
        string? targetDir = Path.GetDirectoryName(target);
        Directory.CreateDirectory(targetDir!);
        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink);

            Assert.Multiple(() =>
            {
                Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
                Assert.That(result.Message, Does.Contain("Created link"));
            });
            _symlinkProvider.Received(1).CreateSymlink(target, source);
        }
        finally
        {
            Directory.Delete(targetDir!, true);
        }
    }

    [Test]
    public void ProcessLink_ExistingFileAtTarget_BackupsAndCreatesLink()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        File.WriteAllText(target, "existing");
        string source = "C:\\repo\\vscode\\settings.json";
        _backupProvider.BackupFile(target).Returns(target + ".backup");

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink);

            Assert.Multiple(() =>
            {
                Assert.That(result.Level, Is.EqualTo(ResultLevel.Warning));
                Assert.That(result.Message, Does.Contain("Backed up"));
            });
            _backupProvider.Received(1).BackupFile(target);
            _symlinkProvider.Received(1).CreateSymlink(target, source);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_AlreadyLinkedToSameSource_ReturnsSynced()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        string source = "C:\\repo\\vscode\\settings.json";
        _symlinkProvider.IsSymlink(target).Returns(true);
        _symlinkProvider.GetSymlinkTarget(target).Returns(source);

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink);

            Assert.Multiple(() =>
            {
                Assert.That(result.Level, Is.EqualTo(ResultLevel.Synced));
                Assert.That(result.Message, Does.Contain("Already linked"));
            });
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_Junction_CreatesJunction()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "data");
        string source = "C:\\repo\\myapp\\data";

        try
        {
            DeployResult result = _orchestrator.ProcessLink("myapp", source, target, LinkType.Junction);

            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            _symlinkProvider.Received(1).CreateJunction(target, source);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_ParentDirectoryMissing_CreatesDirectoryAndLink()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-nonexistent-{Guid.NewGuid():N}");
        string target = Path.Combine(tempDir, "sub", "settings.json");
        string source = "C:\\repo\\vscode\\settings.json";

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink);

            Assert.Multiple(() =>
            {
                Assert.That(result.Level, Is.EqualTo(ResultLevel.Warning));
                Assert.That(result.Message, Does.Contain("Created parent directory"));
            });
            Assert.That(Directory.Exists(Path.GetDirectoryName(target)), Is.True);
            _symlinkProvider.Received(1).CreateSymlink(target, source);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_CreationFails_ReturnsError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        string source = "C:\\repo\\vscode\\settings.json";
        _symlinkProvider.When(x => x.CreateSymlink(Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new IOException("Access denied"));

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink);

            Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(result.Message, Does.Contain("Access denied"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_UnauthorizedAccessException_ReturnsActionableError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        string source = "C:\\repo\\vscode\\settings.json";
        _symlinkProvider.When(x => x.CreateSymlink(Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new UnauthorizedAccessException("A required privilege is not held by the client."));

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink);

            Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(result.Message, Does.Contain("Developer Mode").Or.Contain("permissions"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_PrivilegeIOException_ReturnsActionableError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        string source = "C:\\repo\\vscode\\settings.json";
        _symlinkProvider.When(x => x.CreateSymlink(Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new IOException("A required privilege is not held by the client. : 'path'"));

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink);

            Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(result.Message, Does.Contain("Developer Mode").Or.Contain("permissions"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_DryRun_ParentDirectoryMissing_ReportsWouldCreate()
    {
        string target = Path.Combine(Path.GetTempPath(), $"perch-nonexistent-{Guid.NewGuid():N}", "sub", "settings.json");
        string source = "C:\\repo\\vscode\\settings.json";

        DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink, dryRun: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            Assert.That(result.Message, Does.Contain("Would create parent directory"));
        });
        Assert.That(Directory.Exists(Path.GetDirectoryName(target)), Is.False);
        _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public void ProcessLink_DryRun_NoTargetExists_ReportsWouldCreate()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        string source = "C:\\repo\\vscode\\settings.json";

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink, dryRun: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
                Assert.That(result.Message, Does.Contain("Would create link"));
            });
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_DryRun_ExistingFile_ReportsWouldBackup()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        File.WriteAllText(target, "existing");
        string source = "C:\\repo\\vscode\\settings.json";

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink, dryRun: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.Level, Is.EqualTo(ResultLevel.Warning));
                Assert.That(result.Message, Does.Contain("Would back up"));
            });
            _backupProvider.DidNotReceive().BackupFile(Arg.Any<string>());
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_DryRun_AlreadyLinked_ReturnsSynced()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        string source = "C:\\repo\\vscode\\settings.json";
        _symlinkProvider.IsSymlink(target).Returns(true);
        _symlinkProvider.GetSymlinkTarget(target).Returns(source);

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink, dryRun: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.Level, Is.EqualTo(ResultLevel.Synced));
                Assert.That(result.Message, Does.Contain("Already linked"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_TargetFileLocked_ReturnsError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        File.WriteAllText(target, "existing");
        string source = "C:\\repo\\vscode\\settings.json";
        _fileLockDetector.IsLocked(target).Returns(true);

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink);

            Assert.Multiple(() =>
            {
                Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
                Assert.That(result.Message, Does.Contain("locked"));
            });
            _backupProvider.DidNotReceive().BackupFile(Arg.Any<string>());
            _symlinkProvider.DidNotReceive().CreateSymlink(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ProcessLink_TargetFileNotLocked_ProceedsNormally()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, "settings.json");
        string source = "C:\\repo\\vscode\\settings.json";
        _fileLockDetector.IsLocked(target).Returns(false);

        try
        {
            DeployResult result = _orchestrator.ProcessLink("vscode", source, target, LinkType.Symlink);

            Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
            _symlinkProvider.Received(1).CreateSymlink(target, source);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
