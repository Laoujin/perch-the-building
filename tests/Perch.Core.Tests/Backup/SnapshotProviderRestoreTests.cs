using Perch.Core.Backup;

namespace Perch.Core.Tests.Backup;

[TestFixture]
public sealed class SnapshotProviderRestoreTests
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
    public void CreateSnapshot_WritesManifest()
    {
        string file1 = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(file1, "content-a");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { file1 });

        Assert.That(snapshotDir, Is.Not.Null);
        string manifestPath = Path.Combine(snapshotDir!, "snapshot-manifest.json");
        Assert.That(File.Exists(manifestPath), Is.True);
        string json = File.ReadAllText(manifestPath);
        Assert.That(json, Does.Contain("a.txt"));
        Assert.That(json, Does.Contain(file1.Replace("\\", "\\\\")));
    }

    [Test]
    public void ListSnapshots_NoSnapshots_ReturnsEmpty()
    {
        var provider = new SnapshotProvider(_backupRoot);

        var snapshots = provider.ListSnapshots();

        Assert.That(snapshots, Is.Empty);
    }

    [Test]
    public void ListSnapshots_MultipleSnapshots_ReturnsSortedDescending()
    {
        string file1 = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(file1, "content");

        var provider = new SnapshotProvider(_backupRoot);
        provider.CreateSnapshot(new[] { file1 });
        Thread.Sleep(1100);
        provider.CreateSnapshot(new[] { file1 });

        var snapshots = provider.ListSnapshots();

        Assert.That(snapshots, Has.Count.EqualTo(2));
        Assert.That(snapshots[0].Timestamp, Is.GreaterThanOrEqualTo(snapshots[1].Timestamp));
    }

    [Test]
    public void RestoreSnapshot_ValidId_RestoresFiles()
    {
        string file1 = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(file1, "original");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { file1 });

        File.WriteAllText(file1, "modified");

        string snapshotId = Path.GetFileName(snapshotDir!);
        var results = provider.RestoreSnapshot(snapshotId);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Restored));
        Assert.That(File.ReadAllText(file1), Is.EqualTo("original"));
    }

    [Test]
    public void RestoreSnapshot_FileFilter_RestoresOnlyMatching()
    {
        string file1 = Path.Combine(_tempDir, "a.txt");
        string file2 = Path.Combine(_tempDir, "b.txt");
        File.WriteAllText(file1, "content-a");
        File.WriteAllText(file2, "content-b");

        var provider = new SnapshotProvider(_backupRoot);
        string? snapshotDir = provider.CreateSnapshot(new[] { file1, file2 });

        File.WriteAllText(file1, "modified-a");
        File.WriteAllText(file2, "modified-b");

        string snapshotId = Path.GetFileName(snapshotDir!);
        var results = provider.RestoreSnapshot(snapshotId, fileFilter: "a.txt");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].FileName, Is.EqualTo("a.txt"));
        Assert.That(File.ReadAllText(file1), Is.EqualTo("content-a"));
        Assert.That(File.ReadAllText(file2), Is.EqualTo("modified-b"));
    }

    [Test]
    public void RestoreSnapshot_InvalidId_ReturnsError()
    {
        var provider = new SnapshotProvider(_backupRoot);

        var results = provider.RestoreSnapshot("nonexistent");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Error));
        Assert.That(results[0].Message, Does.Contain("not found"));
    }

    [Test]
    public void RestoreSnapshot_NoManifest_ReturnsError()
    {
        string snapshotDir = Path.Combine(_backupRoot, "20250101-120000");
        Directory.CreateDirectory(snapshotDir);
        File.WriteAllText(Path.Combine(snapshotDir, "a.txt"), "content");

        var provider = new SnapshotProvider(_backupRoot);

        var results = provider.RestoreSnapshot("20250101-120000");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Error));
        Assert.That(results[0].Message, Does.Contain("manifest"));
    }

    [Test]
    public void RestoreSnapshot_ExistingTarget_BacksUpFirst()
    {
        string file1 = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(file1, "original");

        var backupProvider = Substitute.For<IFileBackupProvider>();
        backupProvider.BackupFile(Arg.Any<string>()).Returns(x => x.Arg<string>() + ".backup");

        var provider = new SnapshotProvider(_backupRoot, backupProvider);
        string? snapshotDir = provider.CreateSnapshot(new[] { file1 });

        File.WriteAllText(file1, "modified");

        string snapshotId = Path.GetFileName(snapshotDir!);
        var results = provider.RestoreSnapshot(snapshotId);

        Assert.That(results[0].Outcome, Is.EqualTo(RestoreOutcome.Restored));
        backupProvider.Received(1).BackupFile(file1);
    }
}
