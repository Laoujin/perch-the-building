using Perch.Core.Symlinks;

namespace Perch.Core.Tests.Symlinks;

[TestFixture]
[Platform("Linux,MacOsX")]
public sealed class UnixSymlinkProviderTests
{
    private string _tempDir = null!;
    private UnixSymlinkProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _provider = new UnixSymlinkProvider();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public void CreateSymlink_File_CreatesSymbolicLink()
    {
        string targetPath = Path.Combine(_tempDir, "target.txt");
        File.WriteAllText(targetPath, "content");
        string linkPath = Path.Combine(_tempDir, "link.txt");

        _provider.CreateSymlink(linkPath, targetPath);

        Assert.That(File.Exists(linkPath), Is.True);
        Assert.That(new FileInfo(linkPath).LinkTarget, Is.Not.Null);
    }

    [Test]
    public void CreateJunction_FallsBackToDirectorySymlink()
    {
        string targetDir = Path.Combine(_tempDir, "targetdir");
        Directory.CreateDirectory(targetDir);
        string linkPath = Path.Combine(_tempDir, "linkdir");

        _provider.CreateJunction(linkPath, targetDir);

        Assert.That(Directory.Exists(linkPath), Is.True);
        Assert.That(new DirectoryInfo(linkPath).LinkTarget, Is.Not.Null);
    }

    [Test]
    public void IsSymlink_Symlink_ReturnsTrue()
    {
        string targetPath = Path.Combine(_tempDir, "target.txt");
        File.WriteAllText(targetPath, "content");
        string linkPath = Path.Combine(_tempDir, "link.txt");
        File.CreateSymbolicLink(linkPath, targetPath);

        Assert.That(_provider.IsSymlink(linkPath), Is.True);
    }

    [Test]
    public void IsSymlink_RegularFile_ReturnsFalse()
    {
        string filePath = Path.Combine(_tempDir, "regular.txt");
        File.WriteAllText(filePath, "content");

        Assert.That(_provider.IsSymlink(filePath), Is.False);
    }

    [Test]
    public void GetSymlinkTarget_Symlink_ReturnsTarget()
    {
        string targetPath = Path.Combine(_tempDir, "target.txt");
        File.WriteAllText(targetPath, "content");
        string linkPath = Path.Combine(_tempDir, "link.txt");
        File.CreateSymbolicLink(linkPath, targetPath);

        string? result = _provider.GetSymlinkTarget(linkPath);

        Assert.That(result, Is.EqualTo(targetPath));
    }
}
