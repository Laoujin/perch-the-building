using Perch.Core.Symlinks;

namespace Perch.Core.Tests.Symlinks;

[TestFixture]
[Platform("Win")]
public sealed class WindowsFileLockDetectorTests
{
    private WindowsFileLockDetector _detector = null!;

    [SetUp]
    public void SetUp()
    {
        _detector = new WindowsFileLockDetector();
    }

    [Test]
    public void IsLocked_FileNotLocked_ReturnsFalse()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            Assert.That(_detector.IsLocked(tempFile), Is.False);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void IsLocked_FileLockedByAnotherStream_ReturnsTrue()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using FileStream stream = File.Open(tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.That(_detector.IsLocked(tempFile), Is.True);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void IsLocked_FileDoesNotExist_ReturnsFalse()
    {
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"perch-nonexistent-{Guid.NewGuid():N}.txt");
        Assert.That(_detector.IsLocked(nonExistentPath), Is.False);
    }
}
