using Perch.Core.Catalog;

namespace Perch.Core.Tests.Catalog;

[TestFixture]
public sealed class CatalogParserGitHubStarsTests
{
    private CatalogParser _parser = null!;

    [SetUp]
    public void SetUp() => _parser = new CatalogParser();

    [Test]
    public void ParseGitHubStars_ValidYaml_ReturnsDictionary()
    {
        string yaml = """
            app/vscode:
              repo: microsoft/vscode
              stars: 181800
            app/nodejs:
              repo: nodejs/node
              stars: 110000
            """;

        var result = _parser.ParseGitHubStars(yaml);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result["vscode"], Is.EqualTo(181800));
            Assert.That(result["nodejs"], Is.EqualTo(110000));
        });
    }

    [Test]
    public void ParseGitHubStars_EmptyYaml_ReturnsEmpty()
    {
        var result = _parser.ParseGitHubStars("");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ParseGitHubStars_StripsAppPrefix()
    {
        string yaml = """
            app/alacritty:
              repo: alacritty/alacritty
              stars: 62472
            """;

        var result = _parser.ParseGitHubStars(yaml);

        Assert.That(result.ContainsKey("alacritty"), Is.True);
    }

    [Test]
    public void ParseGitHubStars_KeyWithoutPrefix_StillWorks()
    {
        string yaml = """
            custom-tool:
              repo: org/custom-tool
              stars: 500
            """;

        var result = _parser.ParseGitHubStars(yaml);

        Assert.That(result["custom-tool"], Is.EqualTo(500));
    }
}
