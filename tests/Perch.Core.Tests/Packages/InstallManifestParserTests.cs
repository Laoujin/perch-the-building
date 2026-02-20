using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class InstallManifestParserTests
{
    private InstallManifestParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new InstallManifestParser();
    }

    [Test]
    public void Parse_ValidYaml_ReturnsManifest()
    {
        string yaml = """
            apps:
              - git
              - vscode
              - nvm
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Apps, Has.Length.EqualTo(3));
    }

    [Test]
    public void Parse_WithMachineOverrides_ParsesAddAndExclude()
    {
        string yaml = """
            apps:
              - git
              - vscode
              - docker
            machines:
              HOME-PC:
                add: [heidisql]
                exclude: [docker]
              WORK-PC:
                add: [docker]
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.True);
        var manifest = result.Manifest!;
        Assert.Multiple(() =>
        {
            Assert.That(manifest.Apps, Has.Length.EqualTo(3));
            Assert.That(manifest.Machines, Has.Count.EqualTo(2));
            Assert.That(manifest.Machines["HOME-PC"].Add, Has.Length.EqualTo(1));
            Assert.That(manifest.Machines["HOME-PC"].Exclude, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void Parse_EmptyYaml_ReturnsFailure()
    {
        var result = _parser.Parse("");

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public void Parse_NoApps_ReturnsEmptyList()
    {
        string yaml = """
            machines:
              PC:
                add: [git]
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Apps, Is.Empty);
    }

    [Test]
    public void Parse_InvalidYaml_ReturnsFailure()
    {
        var result = _parser.Parse("{{broken yaml::");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("Invalid YAML"));
        });
    }

    [Test]
    public void Parse_WhitespaceApps_FiltersOut()
    {
        string yaml = """
            apps:
              - git
              - ""
              - "  "
              - vscode
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Apps, Has.Length.EqualTo(2));
    }

    [Test]
    public void Parse_NullMachineValue_SkipsEntry()
    {
        string yaml = """
            apps:
              - git
            machines:
              EMPTY-PC:
            """;

        var result = _parser.Parse(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Machines, Is.Empty);
    }

    [Test]
    public void Parse_MachineWithWhitespaceInAddExclude_FiltersOut()
    {
        string yaml = """
            apps:
              - git
            machines:
              PC:
                add: [docker, ""]
                exclude: ["  ", npm]
            """;

        var result = _parser.Parse(yaml);

        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.Machines["PC"].Add, Has.Length.EqualTo(1));
            Assert.That(result.Manifest!.Machines["PC"].Exclude, Has.Length.EqualTo(1));
        });
    }
}
