using System.Text.Json;

using NSubstitute;

using Perch.Core.Backup;

namespace Perch.Core.Tests.Backup;

[TestFixture]
public sealed class SnapshotProviderTests
{
    private string _tempDir = null!;
    private string _backupRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        _backupRoot = Path.Combine(_tempDir, "backups");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public void CreateSnapshot_ExistingFiles_CopiesToTimestampedDir()
    {
        string file1 = Path.Combine(_tempDir, "a.txt");
        string file2 = Path.Combine(_tempDir, "b.txt");
        File.WriteAllText(file1, "content-a");
        File.WriteAllText(file2, "content-b");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { file1, file2 });

        Assert.That(snapshotDir, Is.Not.Null);
        Assert.That(Directory.Exists(snapshotDir), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(snapshotDir!, "a.txt")), Is.EqualTo("content-a"));
        Assert.That(File.ReadAllText(Path.Combine(snapshotDir!, "b.txt")), Is.EqualTo("content-b"));
    }

    [Test]
    public void CreateSnapshot_NoExistingFiles_ReturnsNull()
    {
        var provider = new SnapshotProvider(_backupRoot);
        string? result = provider.CreateSnapshot(new[]
        {
            Path.Combine(_tempDir, "nonexistent1.txt"),
            Path.Combine(_tempDir, "nonexistent2.txt"),
        });

        Assert.That(result, Is.Null);
    }

    [Test]
    public void CreateSnapshot_MixedExistingAndMissing_CopiesOnlyExisting()
    {
        string existingFile = Path.Combine(_tempDir, "exists.txt");
        File.WriteAllText(existingFile, "content");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { existingFile, Path.Combine(_tempDir, "missing.txt") });

        Assert.That(snapshotDir, Is.Not.Null);
        Assert.That(File.Exists(Path.Combine(snapshotDir!, "exists.txt")), Is.True);
        Assert.That(File.Exists(Path.Combine(snapshotDir!, "missing.txt")), Is.False);
    }

    [Test]
    public void CreateSnapshot_DirectoryTarget_CopiesRecursively()
    {
        string sourceDir = Path.Combine(_tempDir, "mydir");
        string subDir = Path.Combine(sourceDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(sourceDir, "root.txt"), "root-content");
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested-content");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { sourceDir });

        Assert.That(snapshotDir, Is.Not.Null);
        Assert.That(File.ReadAllText(Path.Combine(snapshotDir!, "mydir", "root.txt")), Is.EqualTo("root-content"));
        Assert.That(File.ReadAllText(Path.Combine(snapshotDir!, "mydir", "sub", "nested.txt")), Is.EqualTo("nested-content"));
    }

    [Test]
    public void CreateSnapshot_WritesManifest()
    {
        string file = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(file, "content");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { file });

        string manifestPath = Path.Combine(snapshotDir!, "snapshot-manifest.json");
        Assert.That(File.Exists(manifestPath), Is.True);
        string json = File.ReadAllText(manifestPath);
        Assert.That(json, Does.Contain("a.txt"));
        Assert.That(json, Does.Contain(file.Replace("\\", "\\\\")));
    }

    [Test]
    public void ListSnapshots_NoBackupRoot_ReturnsEmpty()
    {
        var provider = new SnapshotProvider(_backupRoot);

        var snapshots = provider.ListSnapshots();

        Assert.That(snapshots, Is.Empty);
    }

    [Test]
    public void ListSnapshots_ValidSnapshots_ReturnsSortedByTimestampDesc()
    {
        string file = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(file, "content");

        var provider = new SnapshotProvider(_backupRoot);
        provider.CreateSnapshot(new[] { file });
        Thread.Sleep(1100);
        provider.CreateSnapshot(new[] { file });

        var snapshots = provider.ListSnapshots();

        Assert.That(snapshots, Has.Count.EqualTo(2));
        Assert.That(snapshots[0].Timestamp, Is.GreaterThan(snapshots[1].Timestamp));
    }

    [Test]
    public void ListSnapshots_InvalidDirectoryNames_Skipped()
    {
        Directory.CreateDirectory(_backupRoot);
        Directory.CreateDirectory(Path.Combine(_backupRoot, "not-a-timestamp"));
        Directory.CreateDirectory(Path.Combine(_backupRoot, "random-dir"));

        var provider = new SnapshotProvider(_backupRoot);
        var snapshots = provider.ListSnapshots();

        Assert.That(snapshots, Is.Empty);
    }

    [Test]
    public void ListSnapshots_SnapshotWithoutManifest_ReturnsEmptyFiles()
    {
        Directory.CreateDirectory(_backupRoot);
        string snapshotDir = Path.Combine(_backupRoot, "20240115-120000");
        Directory.CreateDirectory(snapshotDir);

        var provider = new SnapshotProvider(_backupRoot);
        var snapshots = provider.ListSnapshots();

        Assert.That(snapshots, Has.Count.EqualTo(1));
        Assert.That(snapshots[0].Files, Is.Empty);
    }

    [Test]
    public void ListSnapshots_SnapshotWithManifest_ParsesFiles()
    {
        string file = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(file, "data");

        var provider = new SnapshotProvider(_backupRoot);
        provider.CreateSnapshot(new[] { file });

        var snapshots = provider.ListSnapshots();

        Assert.That(snapshots, Has.Count.EqualTo(1));
        Assert.That(snapshots[0].Files, Has.Length.EqualTo(1));
        Assert.That(snapshots[0].Files[0].FileName, Is.EqualTo("test.txt"));
    }

    [Test]
    public void RestoreSnapshot_MissingSnapshotDir_ReturnsError()
    {
        Directory.CreateDirectory(_backupRoot);
        var backupProvider = Substitute.For<IFileBackupProvider>();
        var provider = new SnapshotProvider(_backupRoot, backupProvider);

        var results = provider.RestoreSnapshot("nonexistent");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Error));
        Assert.That(results[0].Message, Does.Contain("not found"));
    }

    [Test]
    public void RestoreSnapshot_MissingManifest_ReturnsError()
    {
        Directory.CreateDirectory(_backupRoot);
        string snapshotDir = Path.Combine(_backupRoot, "20240115-120000");
        Directory.CreateDirectory(snapshotDir);

        var backupProvider = Substitute.For<IFileBackupProvider>();
        var provider = new SnapshotProvider(_backupRoot, backupProvider);

        var results = provider.RestoreSnapshot("20240115-120000");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Error));
        Assert.That(results[0].Message, Does.Contain("manifest"));
    }

    [Test]
    public void RestoreSnapshot_SuccessfulRestore_CopiesAndBackups()
    {
        string origFile = Path.Combine(_tempDir, "target.txt");
        File.WriteAllText(origFile, "original");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { origFile });

        File.WriteAllText(origFile, "modified");

        var backupProvider = Substitute.For<IFileBackupProvider>();
        var restoreProvider = new SnapshotProvider(_backupRoot, backupProvider);
        string snapshotId = Path.GetFileName(snapshotDir!);
        var results = restoreProvider.RestoreSnapshot(snapshotId);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Restored));
        Assert.That(File.ReadAllText(origFile), Is.EqualTo("original"));
        backupProvider.Received(1).BackupFile(origFile);
    }

    [Test]
    public void RestoreSnapshot_NewFile_NoBackupCall()
    {
        string origFile = Path.Combine(_tempDir, "target.txt");
        File.WriteAllText(origFile, "original");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { origFile });

        File.Delete(origFile);

        var backupProvider = Substitute.For<IFileBackupProvider>();
        var restoreProvider = new SnapshotProvider(_backupRoot, backupProvider);
        string snapshotId = Path.GetFileName(snapshotDir!);
        var results = restoreProvider.RestoreSnapshot(snapshotId);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Restored));
        backupProvider.DidNotReceive().BackupFile(Arg.Any<string>());
    }

    [Test]
    public void RestoreSnapshot_WithFileFilter_RestoresOnlyMatching()
    {
        string file1 = Path.Combine(_tempDir, "keep.txt");
        string file2 = Path.Combine(_tempDir, "skip.txt");
        File.WriteAllText(file1, "keep-content");
        File.WriteAllText(file2, "skip-content");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { file1, file2 });

        File.WriteAllText(file1, "changed");
        File.WriteAllText(file2, "changed");

        var backupProvider = Substitute.For<IFileBackupProvider>();
        var restoreProvider = new SnapshotProvider(_backupRoot, backupProvider);
        string snapshotId = Path.GetFileName(snapshotDir!);
        var results = restoreProvider.RestoreSnapshot(snapshotId, fileFilter: "keep.txt");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].FileName, Is.EqualTo("keep.txt"));
        Assert.That(File.ReadAllText(file1), Is.EqualTo("keep-content"));
        Assert.That(File.ReadAllText(file2), Is.EqualTo("changed"));
    }

    [Test]
    public void RestoreSnapshot_FileMissingFromSnapshot_ReturnsError()
    {
        string file = Path.Combine(_tempDir, "target.txt");
        File.WriteAllText(file, "content");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { file });

        File.Delete(Path.Combine(snapshotDir!, "target.txt"));

        var backupProvider = Substitute.For<IFileBackupProvider>();
        var restoreProvider = new SnapshotProvider(_backupRoot, backupProvider);
        string snapshotId = Path.GetFileName(snapshotDir!);
        var results = restoreProvider.RestoreSnapshot(snapshotId);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Error));
        Assert.That(results[0].Message, Does.Contain("missing"));
    }

    [Test]
    public void RestoreSnapshot_BackupThrows_ReturnsError()
    {
        string file = Path.Combine(_tempDir, "target.txt");
        File.WriteAllText(file, "content");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { file });

        var backupProvider = Substitute.For<IFileBackupProvider>();
        backupProvider.BackupFile(Arg.Any<string>()).Returns(_ => throw new IOException("disk full"));
        var restoreProvider = new SnapshotProvider(_backupRoot, backupProvider);
        string snapshotId = Path.GetFileName(snapshotDir!);
        var results = restoreProvider.RestoreSnapshot(snapshotId);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Error));
        Assert.That(results[0].Message, Does.Contain("disk full"));
    }
}
