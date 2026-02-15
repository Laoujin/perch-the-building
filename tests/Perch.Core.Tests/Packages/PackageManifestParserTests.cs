using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class PackageManifestParserTests
{
    private PackageManifestParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new PackageManifestParser();
    }

    [Test]
    public void Parse_ValidPackages_ReturnsAll()
    {
        string yaml = """
            packages:
              - name: git
                manager: winget
              - name: 7zip
                manager: chocolatey
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Packages, Has.Length.EqualTo(2));
            Assert.That(result.Packages[0].Name, Is.EqualTo("git"));
            Assert.That(result.Packages[0].Manager, Is.EqualTo(PackageManager.Winget));
            Assert.That(result.Packages[1].Name, Is.EqualTo("7zip"));
            Assert.That(result.Packages[1].Manager, Is.EqualTo(PackageManager.Chocolatey));
            Assert.That(result.Errors, Is.Empty);
        });
    }

    [Test]
    public void Parse_MixedValidInvalid_ReturnsPartialSuccess()
    {
        string yaml = """
            packages:
              - name: git
                manager: winget
              - name: bad-pkg
                manager: pacman
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Packages, Has.Length.EqualTo(1));
            Assert.That(result.Packages[0].Name, Is.EqualTo("git"));
            Assert.That(result.Errors, Has.Length.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("bad-pkg"));
        });
    }

    [Test]
    public void Parse_MissingName_ReportsError()
    {
        string yaml = """
            packages:
              - manager: winget
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0], Does.Contain("name"));
    }

    [Test]
    public void Parse_UnknownManager_ReportsError()
    {
        string yaml = """
            packages:
              - name: git
                manager: pacman
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0], Does.Contain("pacman"));
    }

    [Test]
    public void Parse_EmptyPackages_ReturnsFailure()
    {
        string yaml = """
            packages: []
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0], Does.Contain("packages"));
    }

    [Test]
    public void Parse_EmptyYaml_ReturnsFailure()
    {
        string yaml = "";

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0], Does.Contain("empty"));
    }

    [Test]
    public void Parse_InvalidYaml_ReturnsFailure()
    {
        string yaml = "{{invalid yaml::";

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0], Does.Contain("Invalid YAML"));
    }

    [Test]
    public void Parse_CaseInsensitiveManager_Parses()
    {
        string yaml = """
            packages:
              - name: git
                manager: WinGet
              - name: 7zip
                manager: CHOCOLATEY
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Packages[0].Manager, Is.EqualTo(PackageManager.Winget));
            Assert.That(result.Packages[1].Manager, Is.EqualTo(PackageManager.Chocolatey));
        });
    }
}
