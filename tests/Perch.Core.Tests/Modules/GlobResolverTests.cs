using Perch.Core.Modules;

namespace Perch.Core.Tests.Modules;

[TestFixture]
public sealed class GlobResolverTests
{
    private string _tempDir = null!;
    private GlobResolver _resolver = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _resolver = new GlobResolver();
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
    public void Resolve_NoGlob_ReturnsSamePath()
    {
        string path = Path.Combine(_tempDir, "some", "path");

        IReadOnlyList<string> result = _resolver.Resolve(path);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(path));
    }

    [Test]
    public void Resolve_DirectoryGlob_MatchesDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "17.0_abc"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "other"));

        string pattern = Path.Combine(_tempDir, "17.0_*");

        IReadOnlyList<string> result = _resolver.Resolve(pattern);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Does.EndWith("17.0_abc"));
    }

    [Test]
    public void Resolve_DirectoryGlob_MultipleMatches_ReturnsAll()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "17.0_abc"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "17.0_def"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "other"));

        string pattern = Path.Combine(_tempDir, "17.0_*");

        IReadOnlyList<string> result = _resolver.Resolve(pattern);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void Resolve_DirectoryGlob_NoMatch_ReturnsEmpty()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "other"));

        string pattern = Path.Combine(_tempDir, "17.0_*");

        IReadOnlyList<string> result = _resolver.Resolve(pattern);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Resolve_FileGlob_MatchesFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "settings.bak"), "{}");

        string pattern = Path.Combine(_tempDir, "settings.*");

        IReadOnlyList<string> result = _resolver.Resolve(pattern);

        Assert.That(result, Has.Count.EqualTo(2));
    }
}
