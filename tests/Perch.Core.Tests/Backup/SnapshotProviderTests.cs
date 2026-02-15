using Perch.Core.Backup;

namespace Perch.Core.Tests.Backup;

[TestFixture]
public sealed class SnapshotProviderTests
{
    [Test]
    public void CreateSnapshot_ExistingFiles_CopiesToTimestampedDir()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        string backupRoot = Path.Combine(tempDir, "backups");
        Directory.CreateDirectory(tempDir);
        string file1 = Path.Combine(tempDir, "a.txt");
        string file2 = Path.Combine(tempDir, "b.txt");
        File.WriteAllText(file1, "content-a");
        File.WriteAllText(file2, "content-b");

        try
        {
            var provider = new SnapshotProvider(backupRoot);
            string? snapshotDir = provider.CreateSnapshot(new[] { file1, file2 });

            Assert.That(snapshotDir, Is.Not.Null);
            Assert.That(Directory.Exists(snapshotDir), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(snapshotDir!, "a.txt")), Is.EqualTo("content-a"));
            Assert.That(File.ReadAllText(Path.Combine(snapshotDir!, "b.txt")), Is.EqualTo("content-b"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void CreateSnapshot_NoExistingFiles_ReturnsNull()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        string backupRoot = Path.Combine(tempDir, "backups");
        Directory.CreateDirectory(tempDir);

        try
        {
            var provider = new SnapshotProvider(backupRoot);
            string? result = provider.CreateSnapshot(new[]
            {
                Path.Combine(tempDir, "nonexistent1.txt"),
                Path.Combine(tempDir, "nonexistent2.txt"),
            });

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void CreateSnapshot_MixedExistingAndMissing_CopiesOnlyExisting()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        string backupRoot = Path.Combine(tempDir, "backups");
        Directory.CreateDirectory(tempDir);
        string existingFile = Path.Combine(tempDir, "exists.txt");
        File.WriteAllText(existingFile, "content");
        string missingFile = Path.Combine(tempDir, "missing.txt");

        try
        {
            var provider = new SnapshotProvider(backupRoot);
            string? snapshotDir = provider.CreateSnapshot(new[] { existingFile, missingFile });

            Assert.That(snapshotDir, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(snapshotDir!, "exists.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(snapshotDir!, "missing.txt")), Is.False);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void CreateSnapshot_DirectoryTarget_CopiesRecursively()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        string backupRoot = Path.Combine(tempDir, "backups");
        Directory.CreateDirectory(tempDir);
        string sourceDir = Path.Combine(tempDir, "mydir");
        Directory.CreateDirectory(sourceDir);
        string subDir = Path.Combine(sourceDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(sourceDir, "root.txt"), "root-content");
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested-content");

        try
        {
            var provider = new SnapshotProvider(backupRoot);
            string? snapshotDir = provider.CreateSnapshot(new[] { sourceDir });

            Assert.That(snapshotDir, Is.Not.Null);
            Assert.That(File.ReadAllText(Path.Combine(snapshotDir!, "mydir", "root.txt")), Is.EqualTo("root-content"));
            Assert.That(File.ReadAllText(Path.Combine(snapshotDir!, "mydir", "sub", "nested.txt")), Is.EqualTo("nested-content"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
