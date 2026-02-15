using Perch.Core.Diff;

namespace Perch.Core.Tests.Diff;

[TestFixture]
public sealed class DiffSnapshotServiceTests
{
    private DiffSnapshotService _service = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new DiffSnapshotService();
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-diff-test-{Guid.NewGuid():N}");
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
    public async Task CaptureAsync_CapturesAllFiles()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "a.txt"), "hello");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "b.txt"), "world");

        await _service.CaptureAsync(_tempDir);

        Assert.That(_service.HasActiveSnapshot(), Is.True);
    }

    [Test]
    public async Task CompareAsync_NoChanges_ReturnsEmpty()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "a.txt"), "hello");

        await _service.CaptureAsync(_tempDir);
        DiffResult result = await _service.CompareAsync();

        Assert.That(result.Changes, Is.Empty);
    }

    [Test]
    public async Task CompareAsync_NewFile_ReportsAdded()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "a.txt"), "hello");
        await _service.CaptureAsync(_tempDir);

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "b.txt"), "new file");
        DiffResult result = await _service.CompareAsync();

        Assert.That(result.Changes, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Changes[0].RelativePath, Is.EqualTo("b.txt"));
            Assert.That(result.Changes[0].Type, Is.EqualTo(DiffChangeType.Added));
        });
    }

    [Test]
    public async Task CompareAsync_DeletedFile_ReportsDeleted()
    {
        string filePath = Path.Combine(_tempDir, "a.txt");
        await File.WriteAllTextAsync(filePath, "hello");
        await _service.CaptureAsync(_tempDir);

        File.Delete(filePath);
        DiffResult result = await _service.CompareAsync();

        Assert.That(result.Changes, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Changes[0].RelativePath, Is.EqualTo("a.txt"));
            Assert.That(result.Changes[0].Type, Is.EqualTo(DiffChangeType.Deleted));
        });
    }

    [Test]
    public async Task CompareAsync_ModifiedFile_ReportsModified()
    {
        string filePath = Path.Combine(_tempDir, "a.txt");
        await File.WriteAllTextAsync(filePath, "hello");
        await _service.CaptureAsync(_tempDir);

        await File.WriteAllTextAsync(filePath, "changed content");
        DiffResult result = await _service.CompareAsync();

        Assert.That(result.Changes, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Changes[0].RelativePath, Is.EqualTo("a.txt"));
            Assert.That(result.Changes[0].Type, Is.EqualTo(DiffChangeType.Modified));
        });
    }

    [Test]
    public async Task CompareAsync_MixedChanges_ReportsAll()
    {
        string existingFile = Path.Combine(_tempDir, "existing.txt");
        string deleteMe = Path.Combine(_tempDir, "delete-me.txt");
        await File.WriteAllTextAsync(existingFile, "original");
        await File.WriteAllTextAsync(deleteMe, "will be deleted");
        await _service.CaptureAsync(_tempDir);

        await File.WriteAllTextAsync(existingFile, "modified");
        File.Delete(deleteMe);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "new.txt"), "brand new");
        DiffResult result = await _service.CompareAsync();

        Assert.That(result.Changes, Has.Length.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(result.Changes.Any(c => c.Type == DiffChangeType.Added && c.RelativePath == "new.txt"), Is.True);
            Assert.That(result.Changes.Any(c => c.Type == DiffChangeType.Modified && c.RelativePath == "existing.txt"), Is.True);
            Assert.That(result.Changes.Any(c => c.Type == DiffChangeType.Deleted && c.RelativePath == "delete-me.txt"), Is.True);
        });
    }

    [Test]
    public void CompareAsync_NoSnapshot_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.CompareAsync());
    }
}
