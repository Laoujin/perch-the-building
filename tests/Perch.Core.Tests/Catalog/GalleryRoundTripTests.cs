using Perch.Core.Catalog;

namespace Perch.Core.Tests.Catalog;

[TestFixture]
public sealed class GalleryRoundTripTests
{
    private static readonly string GalleryRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "..", "perch-gallery", "catalog"));

    private CatalogParser _parser = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if (!File.Exists(Path.Combine(GalleryRoot, "index.yaml")))
            Assert.Ignore($"Gallery not found at {GalleryRoot}. Clone perch-gallery alongside this repo.");
    }

    [SetUp]
    public void SetUp()
    {
        _parser = new CatalogParser();
    }

    [Test]
    public void Index_ParsesSuccessfully()
    {
        var yaml = File.ReadAllText(Path.Combine(GalleryRoot, "index.yaml"));
        var result = _parser.ParseIndex(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Apps, Is.Not.Empty);
            Assert.That(result.Value!.Fonts, Is.Not.Empty);
            Assert.That(result.Value!.Tweaks, Is.Not.Empty);
        });
    }

    [Test]
    public void Index_AllAppReferencesHaveFiles()
    {
        var index = ParseIndex();
        foreach (var entry in index.Apps)
        {
            var path = Path.Combine(GalleryRoot, "apps", $"{entry.Id}.yaml");
            Assert.That(File.Exists(path), Is.True, $"Missing app file for index entry '{entry.Id}'");
        }
    }

    [Test]
    public void Index_AllFontReferencesHaveFiles()
    {
        var index = ParseIndex();
        foreach (var entry in index.Fonts)
        {
            var path = Path.Combine(GalleryRoot, "fonts", $"{entry.Id}.yaml");
            Assert.That(File.Exists(path), Is.True, $"Missing font file for index entry '{entry.Id}'");
        }
    }

    [Test]
    public void Index_AllTweakReferencesHaveFiles()
    {
        var index = ParseIndex();
        foreach (var entry in index.Tweaks)
        {
            var path = Path.Combine(GalleryRoot, "tweaks", $"{entry.Id}.yaml");
            Assert.That(File.Exists(path), Is.True, $"Missing tweak file for index entry '{entry.Id}'");
        }
    }

    [TestCaseSource(nameof(AppFiles))]
    public void Apps_ParseSuccessfully(string file)
    {
        var id = Path.GetFileNameWithoutExtension(file);
        var yaml = File.ReadAllText(file);
        var result = _parser.ParseApp(yaml, id);

        Assert.That(result.IsSuccess, Is.True, $"Failed to parse {id}: {result.Error}");
        Assert.That(result.Value!.Name, Is.Not.Empty);
    }

    [TestCaseSource(nameof(FontFiles))]
    public void Fonts_ParseSuccessfully(string file)
    {
        var id = Path.GetFileNameWithoutExtension(file);
        var yaml = File.ReadAllText(file);
        var result = _parser.ParseFont(yaml, id);

        Assert.That(result.IsSuccess, Is.True, $"Failed to parse {id}: {result.Error}");
        Assert.That(result.Value!.Name, Is.Not.Empty);
    }

    [TestCaseSource(nameof(TweakFiles))]
    public void Tweaks_ParseSuccessfully(string file)
    {
        var id = Path.GetFileNameWithoutExtension(file);
        var yaml = File.ReadAllText(file);
        var result = _parser.ParseTweak(yaml, id);

        Assert.That(result.IsSuccess, Is.True, $"Failed to parse {id}: {result.Error}");
        Assert.That(result.Value!.Name, Is.Not.Empty);
        var hasRegistry = result.Value!.Registry.Length > 0;
        var hasScript = result.Value!.Script != null;
        Assert.That(hasRegistry || hasScript, Is.True, $"Tweak '{id}' has no registry entries or script");
    }

    private CatalogIndex ParseIndex()
    {
        var yaml = File.ReadAllText(Path.Combine(GalleryRoot, "index.yaml"));
        var result = _parser.ParseIndex(yaml);
        Assume.That(result.IsSuccess, Is.True);
        return result.Value!;
    }

    private static IEnumerable<string> AppFiles() => GetYamlFiles("apps");
    private static IEnumerable<string> FontFiles() => GetYamlFiles("fonts");
    private static IEnumerable<string> TweakFiles() => GetYamlFiles("tweaks");

    private static IEnumerable<string> GetYamlFiles(string subfolder)
    {
        var dir = Path.Combine(GalleryRoot, subfolder);
        return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.yaml") : [];
    }
}
