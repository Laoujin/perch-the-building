using Perch.Core;
using Perch.Core.Catalog;
using Perch.Core.Git;
using Perch.Core.Modules;
using Perch.Core.Registry;

namespace Perch.Core.Tests.Catalog;

[TestFixture]
public sealed class CatalogParserTests
{
    private CatalogParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new CatalogParser();
    }

    [Test]
    public void ParseApp_ValidYaml_ReturnsEntry()
    {
        string yaml = """
            name: Visual Studio Code
            display-name: VS Code
            category: Development/IDEs
            tags: [editor, ide, microsoft]
            description: Lightweight but powerful source code editor
            install:
              winget: Microsoft.VisualStudio.Code
              choco: vscode
            config:
              links:
                - source: settings.json
                  target:
                    windows: "%APPDATA%/Code/User/settings.json"
                    linux: "$HOME/.config/Code/User/settings.json"
            extensions:
              bundled: []
              recommended:
                - dbaeumer.vscode-eslint
            """;

        var result = _parser.ParseApp(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        var entry = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(entry.Id, Is.EqualTo("vscode"));
            Assert.That(entry.Name, Is.EqualTo("Visual Studio Code"));
            Assert.That(entry.DisplayName, Is.EqualTo("VS Code"));
            Assert.That(entry.Category, Is.EqualTo("Development/IDEs"));
            Assert.That(entry.Tags, Has.Length.EqualTo(3));
            Assert.That(entry.Install!.Winget, Is.EqualTo("Microsoft.VisualStudio.Code"));
            Assert.That(entry.Install.Choco, Is.EqualTo("vscode"));
            Assert.That(entry.Config!.Links, Has.Length.EqualTo(1));
            Assert.That(entry.Config.Links[0].Targets[Platform.Windows], Is.EqualTo("%APPDATA%/Code/User/settings.json"));
            Assert.That(entry.Extensions!.Recommended, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void ParseApp_EmptyYaml_ReturnsFailure()
    {
        var result = _parser.ParseApp("", "test");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo("YAML content is empty."));
    }

    [Test]
    public void ParseApp_MissingName_ReturnsFailure()
    {
        string yaml = """
            category: Test
            """;

        var result = _parser.ParseApp(yaml, "test");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("missing 'name'"));
    }

    [Test]
    public void ParseFont_ValidYaml_ReturnsEntry()
    {
        string yaml = """
            name: JetBrains Mono Nerd Font
            category: Fonts/Programming
            tags: [monospace, nerd-font, ligatures]
            description: JetBrains Mono with Nerd Font patches
            preview-text: "fn main() { let x = 42; }"
            install:
              choco: nerd-fonts-jetbrainsmono
              winget: DEVCOM.JetBrainsMonoNerdFont
            """;

        var result = _parser.ParseFont(yaml, "jetbrains-mono-nf");

        Assert.That(result.IsSuccess, Is.True);
        var entry = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(entry.Id, Is.EqualTo("jetbrains-mono-nf"));
            Assert.That(entry.Name, Is.EqualTo("JetBrains Mono Nerd Font"));
            Assert.That(entry.PreviewText, Is.EqualTo("fn main() { let x = 42; }"));
            Assert.That(entry.Install!.Choco, Is.EqualTo("nerd-fonts-jetbrainsmono"));
        });
    }

    [Test]
    public void ParseFont_EmptyYaml_ReturnsFailure()
    {
        var result = _parser.ParseFont("", "test");

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public void ParseTweak_ValidYaml_ReturnsEntry()
    {
        string yaml = """
            name: Show File Extensions
            category: Developer Settings
            tags: [explorer, files]
            description: Always show file name extensions in Explorer
            reversible: true
            profiles: [developer, power-user]
            priority: 1
            registry:
              - key: HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced
                name: HideFileExt
                value: 0
                type: dword
            """;

        var result = _parser.ParseTweak(yaml, "show-file-extensions");

        Assert.That(result.IsSuccess, Is.True);
        var entry = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(entry.Id, Is.EqualTo("show-file-extensions"));
            Assert.That(entry.Name, Is.EqualTo("Show File Extensions"));
            Assert.That(entry.Reversible, Is.True);
            Assert.That(entry.Profiles, Has.Length.EqualTo(2));
            Assert.That(entry.Priority, Is.EqualTo(1));
            Assert.That(entry.Registry, Has.Length.EqualTo(1));
            Assert.That(entry.Registry[0].Name, Is.EqualTo("HideFileExt"));
            Assert.That(entry.Registry[0].Kind, Is.EqualTo(RegistryValueType.DWord));
        });
    }

    [Test]
    public void ParseIndex_ValidYaml_ReturnsIndex()
    {
        string yaml = """
            apps:
              - id: vscode
                name: Visual Studio Code
                category: Development
                tags: [editor]
              - id: firefox
                name: Firefox
                category: Browsers
                tags: [browser]
            fonts:
              - id: jetbrains-mono
                name: JetBrains Mono
                category: Fonts
                tags: [monospace]
            tweaks:
              - id: show-extensions
                name: Show File Extensions
                category: Developer
                tags: [explorer]
            """;

        var result = _parser.ParseIndex(yaml);

        Assert.That(result.IsSuccess, Is.True);
        var index = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(index.Apps, Has.Length.EqualTo(2));
            Assert.That(index.Fonts, Has.Length.EqualTo(1));
            Assert.That(index.Tweaks, Has.Length.EqualTo(1));
            Assert.That(index.Apps[0].Id, Is.EqualTo("vscode"));
            Assert.That(index.Apps[0].Name, Is.EqualTo("Visual Studio Code"));
        });
    }

    [Test]
    public void ParseIndex_EmptyYaml_ReturnsFailure()
    {
        var result = _parser.ParseIndex("");

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public void ParseApp_ConfigLinkWithLinkType_ParsesLinkType()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: data
                  link-type: junction
                  target:
                    windows: "%APPDATA%/TestApp/data"
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config!.Links[0].LinkType, Is.EqualTo(LinkType.Junction));
    }

    [Test]
    public void ParseApp_ConfigLinkWithoutLinkType_DefaultsToSymlink()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: settings.json
                  target:
                    windows: "%APPDATA%/TestApp/settings.json"
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config!.Links[0].LinkType, Is.EqualTo(LinkType.Symlink));
    }

    [Test]
    public void ParseApp_ConfigLinkWithPlatforms_ParsesPlatforms()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: config.xml
                  platforms: [windows]
                  target:
                    windows: "%APPDATA%/TestApp/config.xml"
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        var link = result.Value!.Config!.Links[0];
        Assert.That(link.Platforms, Has.Length.EqualTo(1));
        Assert.That(link.Platforms[0], Is.EqualTo(Platform.Windows));
    }

    [Test]
    public void ParseApp_ConfigLinkWithTemplate_ParsesTemplateFlag()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: config.template
                  template: true
                  target:
                    windows: "%APPDATA%/TestApp/config"
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config!.Links[0].Template, Is.True);
    }

    [Test]
    public void ParseApp_ConfigLinkWithoutTemplate_DefaultsToFalse()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: settings.json
                  target:
                    windows: "%APPDATA%/TestApp/settings.json"
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config!.Links[0].Template, Is.False);
    }

    [Test]
    public void ParseApp_WithCleanFilter_ParsesRules()
    {
        string yaml = """
            name: Notepad++
            config:
              links:
                - source: config.xml
                  target:
                    windows: "%APPDATA%/Notepad++/config.xml"
              clean-filter:
                files: [config.xml]
                rules:
                  - type: strip-xml-elements
                    elements: [FindHistory, Session]
            """;

        var result = _parser.ParseApp(yaml, "notepadplusplus");

        Assert.That(result.IsSuccess, Is.True);
        var filter = result.Value!.Config!.CleanFilter;
        Assert.That(filter, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(filter!.Files, Has.Length.EqualTo(1));
            Assert.That(filter.Files[0], Is.EqualTo("config.xml"));
            Assert.That(filter.Rules, Has.Length.EqualTo(1));
            Assert.That(filter.Rules[0].Type, Is.EqualTo("strip-xml-elements"));
            Assert.That(filter.Rules[0].Patterns, Has.Length.EqualTo(2));
        });
    }

    [Test]
    public void ParseApp_WithoutCleanFilter_CleanFilterIsNull()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: settings.json
                  target:
                    windows: "%APPDATA%/TestApp/settings.json"
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config!.CleanFilter, Is.Null);
    }

    [Test]
    public void ParseApp_AllNewFields_ParsedCorrectly()
    {
        string yaml = """
            name: TestApp
            logo: testapp.svg
            config:
              links:
                - source: data
                  link-type: junction
                  platforms: [windows, linux]
                  template: true
                  target:
                    windows: "%APPDATA%/TestApp/data"
                    linux: "$HOME/.config/TestApp/data"
              clean-filter:
                files: [data/state.json]
                rules:
                  - type: strip-xml-elements
                    elements: [History]
                  - type: strip-ini-keys
                    keys: [LastOpened, WindowPos]
            extensions:
              bundled: [ext1]
              recommended: [ext2, ext3]
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        var entry = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(entry.Logo, Is.EqualTo("testapp.svg"));
            var link = entry.Config!.Links[0];
            Assert.That(link.LinkType, Is.EqualTo(LinkType.Junction));
            Assert.That(link.Platforms, Has.Length.EqualTo(2));
            Assert.That(link.Template, Is.True);
            var filter = entry.Config.CleanFilter!;
            Assert.That(filter.Rules, Has.Length.EqualTo(2));
            Assert.That(filter.Rules[0].Type, Is.EqualTo("strip-xml-elements"));
            Assert.That(filter.Rules[1].Type, Is.EqualTo("strip-ini-keys"));
            Assert.That(filter.Rules[1].Patterns, Has.Length.EqualTo(2));
            Assert.That(entry.Extensions!.Bundled, Has.Length.EqualTo(1));
            Assert.That(entry.Extensions.Recommended, Has.Length.EqualTo(2));
        });
    }
}
