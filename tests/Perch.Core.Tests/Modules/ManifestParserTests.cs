using Perch.Core;
using Perch.Core.Modules;

namespace Perch.Core.Tests.Modules;

[TestFixture]
public sealed class ManifestParserTests
{
    private ManifestParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new ManifestParser();
    }

    [Test]
    public void Parse_SingleLink_ReturnsManifestWithOneLink()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\Code\\User\\settings.json"
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        var manifest = result.Manifest!;
        Assert.Multiple(() =>
        {
            Assert.That(manifest.ModuleName, Is.EqualTo("vscode"));
            Assert.That(manifest.DisplayName, Is.EqualTo("vscode"));
            Assert.That(manifest.Links, Has.Length.EqualTo(1));
            Assert.That(manifest.Links[0].Source, Is.EqualTo("settings.json"));
            Assert.That(manifest.Links[0].Target, Is.EqualTo("%APPDATA%\\Code\\User\\settings.json"));
            Assert.That(manifest.Links[0].LinkType, Is.EqualTo(LinkType.Symlink));
        });
    }

    [Test]
    public void Parse_MultipleLinks_ReturnsAllLinks()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\Code\\User\\settings.json"
              - source: keybindings.json
                target: "%APPDATA%\\Code\\User\\keybindings.json"
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Links, Has.Length.EqualTo(2));
    }

    [Test]
    public void Parse_JunctionLinkType_ParsesCorrectly()
    {
        string yaml = """
            links:
              - source: data
                target: "C:\\ProgramData\\MyApp"
                link-type: junction
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Links[0].LinkType, Is.EqualTo(LinkType.Junction));
    }

    [Test]
    public void Parse_WithDisplayName_UsesDisplayName()
    {
        string yaml = """
            display-name: Visual Studio Code
            links:
              - source: settings.json
                target: "%APPDATA%\\Code\\User\\settings.json"
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.ModuleName, Is.EqualTo("vscode"));
            Assert.That(result.Manifest!.DisplayName, Is.EqualTo("Visual Studio Code"));
        });
    }

    [Test]
    public void Parse_DefaultLinkType_IsSymlink()
    {
        string yaml = """
            links:
              - source: config
                target: "C:\\Users\\test\\config"
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Links[0].LinkType, Is.EqualTo(LinkType.Symlink));
    }

    [Test]
    public void Parse_InvalidYaml_ReturnsError()
    {
        string yaml = "{{invalid yaml::";

        var result = _parser.Parse(yaml, "bad");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Parse_MissingLinks_ReturnsError()
    {
        string yaml = """
            display-name: No Links App
            """;

        var result = _parser.Parse(yaml, "nolinks");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("links"));
    }

    [Test]
    public void Parse_EmptyFile_ReturnsError()
    {
        string yaml = "";

        var result = _parser.Parse(yaml, "empty");

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public void Parse_LinkMissingSource_ReturnsError()
    {
        string yaml = """
            links:
              - target: "C:\\somewhere"
            """;

        var result = _parser.Parse(yaml, "badsource");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("source"));
    }

    [Test]
    public void Parse_LinkMissingTarget_ReturnsError()
    {
        string yaml = """
            links:
              - source: settings.json
            """;

        var result = _parser.Parse(yaml, "badtarget");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("target"));
    }

    [Test]
    public void Parse_WithPlatforms_ReturnsParsedPlatforms()
    {
        string yaml = """
            platforms:
              - windows
              - linux
            links:
              - source: config
                target: "C:\\test\\config"
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Platforms, Is.EquivalentTo(new[] { Platform.Windows, Platform.Linux }));
    }

    [Test]
    public void Parse_NoPlatforms_ReturnsEmptyArray()
    {
        string yaml = """
            links:
              - source: config
                target: "C:\\test\\config"
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Platforms, Is.Empty);
    }

    [Test]
    public void Parse_UnknownPlatform_IgnoresUnknown()
    {
        string yaml = """
            platforms:
              - windows
              - beos
            links:
              - source: config
                target: "C:\\test\\config"
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Platforms, Has.Length.EqualTo(1));
        Assert.That(result.Manifest!.Platforms[0], Is.EqualTo(Platform.Windows));
    }

    [Test]
    public void Parse_SimpleTarget_BackwardCompatible()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\Code\\User\\settings.json"
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Links[0].Target, Is.EqualTo("%APPDATA%\\Code\\User\\settings.json"));
        Assert.That(result.Manifest!.Links[0].PlatformTargets, Is.Null);
    }

    [Test]
    public void Parse_PlatformTargets_ReturnsDictionary()
    {
        string yaml = """
            links:
              - source: settings.json
                target:
                  windows: "%APPDATA%\\Code\\User\\settings.json"
                  linux: "$HOME/.config/Code/User/settings.json"
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        var link = result.Manifest!.Links[0];
        Assert.Multiple(() =>
        {
            Assert.That(link.Target, Is.Null);
            Assert.That(link.PlatformTargets, Is.Not.Null);
            Assert.That(link.PlatformTargets!, Has.Count.EqualTo(2));
            Assert.That(link.GetTargetForPlatform(Platform.Windows), Is.EqualTo("%APPDATA%\\Code\\User\\settings.json"));
            Assert.That(link.GetTargetForPlatform(Platform.Linux), Is.EqualTo("$HOME/.config/Code/User/settings.json"));
        });
    }

    [Test]
    public void Parse_MixedSimpleAndPlatformLinks_ParsesBoth()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\Code\\User\\settings.json"
              - source: keybindings.json
                target:
                  windows: "%APPDATA%\\Code\\User\\keybindings.json"
                  macos: "$HOME/Library/Application Support/Code/User/keybindings.json"
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.Links, Has.Length.EqualTo(2));
            Assert.That(result.Manifest!.Links[0].Target, Is.Not.Null);
            Assert.That(result.Manifest!.Links[1].PlatformTargets, Is.Not.Null);
        });
    }

    [Test]
    public void Parse_WithHooks_ReturnsDeployHooks()
    {
        string yaml = """
            hooks:
              pre-deploy: "./scripts/setup.ps1"
              post-deploy: "./scripts/import.ps1"
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.Hooks, Is.Not.Null);
            Assert.That(result.Manifest!.Hooks!.PreDeploy, Is.EqualTo("./scripts/setup.ps1"));
            Assert.That(result.Manifest!.Hooks!.PostDeploy, Is.EqualTo("./scripts/import.ps1"));
        });
    }

    [Test]
    public void Parse_WithPartialHooks_ReturnsPartialDeployHooks()
    {
        string yaml = """
            hooks:
              pre-deploy: "./scripts/setup.ps1"
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.Hooks, Is.Not.Null);
            Assert.That(result.Manifest!.Hooks!.PreDeploy, Is.EqualTo("./scripts/setup.ps1"));
            Assert.That(result.Manifest!.Hooks!.PostDeploy, Is.Null);
        });
    }

    [Test]
    public void Parse_NoHooks_ReturnsNullHooks()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Hooks, Is.Null);
    }

    [Test]
    public void Parse_WithCleanFilter_ReturnsCleanFilterDefinition()
    {
        string yaml = """
            clean-filter:
              name: obsidian-clean
              script: scripts/clean.sh
              files:
                - data.json
                - workspace.json
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "obsidian");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.CleanFilter, Is.Not.Null);
            Assert.That(result.Manifest!.CleanFilter!.Name, Is.EqualTo("obsidian-clean"));
            Assert.That(result.Manifest!.CleanFilter!.Script, Is.EqualTo("scripts/clean.sh"));
            Assert.That(result.Manifest!.CleanFilter!.Files, Has.Length.EqualTo(2));
            Assert.That(result.Manifest!.CleanFilter!.Files[0], Is.EqualTo("data.json"));
        });
    }

    [Test]
    public void Parse_NoCleanFilter_ReturnsNull()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.CleanFilter, Is.Null);
    }
}
