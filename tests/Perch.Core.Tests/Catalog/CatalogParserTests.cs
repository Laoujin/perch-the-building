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
    public void ParseApp_WithKindDotfile_SetsKindToDotfile()
    {
        string yaml = """
            name: Git
            kind: dotfile
            category: Development/Version Control
            config:
              links:
                - source: .gitconfig
                  target:
                    windows: "%USERPROFILE%/.gitconfig"
            """;

        var result = _parser.ParseApp(yaml, "git");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Kind, Is.EqualTo(CatalogKind.Dotfile));
    }

    [Test]
    public void ParseApp_WithoutKind_DefaultsToApp()
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
        Assert.That(result.Value!.Kind, Is.EqualTo(CatalogKind.App));
    }

    [Test]
    public void ParseIndex_WithKindDotfile_SetsKindOnIndexEntry()
    {
        string yaml = """
            apps:
              - id: git
                name: Git
                category: Development
                tags: [vcs]
                kind: dotfile
              - id: vscode
                name: Visual Studio Code
                category: Development
                tags: [editor]
            """;

        var result = _parser.ParseIndex(yaml);

        Assert.That(result.IsSuccess, Is.True);
        var index = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(index.Apps[0].Kind, Is.EqualTo(CatalogKind.Dotfile));
            Assert.That(index.Apps[1].Kind, Is.EqualTo(CatalogKind.App));
        });
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

    [Test]
    public void ParseTweak_WithDefaultValue_ParsesDefaultValue()
    {
        string yaml = """
            name: Show File Extensions
            category: Explorer/Files
            tags: [explorer, files]
            description: Always show file name extensions in Explorer
            reversible: true
            profiles: [developer]
            registry:
              - key: HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced
                name: HideFileExt
                value: 0
                type: dword
                default-value: 1
            """;

        var result = _parser.ParseTweak(yaml, "show-file-extensions");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Registry[0].DefaultValue, Is.EqualTo(1));
    }

    [Test]
    public void ParseTweak_WithNullDefaultValue_ParsesAsNull()
    {
        string yaml = """
            name: Classic Context Menu
            category: Explorer/Context Menu
            tags: [explorer]
            description: Restore classic context menu
            reversible: true
            registry:
              - key: HKCU\Software\Classes\CLSID\{86ca1aa0}\InprocServer32
                name: ""
                value: ""
                type: string
                default-value: null
            """;

        var result = _parser.ParseTweak(yaml, "classic-context-menu");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Registry[0].DefaultValue, Is.Null);
    }

    [Test]
    public void ParseTweak_WithoutDefaultValue_DefaultValueIsNull()
    {
        string yaml = """
            name: Test Tweak
            category: Test
            tags: [test]
            reversible: true
            registry:
              - key: HKCU\Software\Test
                name: TestValue
                value: 1
                type: dword
            """;

        var result = _parser.ParseTweak(yaml, "test-tweak");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Registry[0].DefaultValue, Is.Null);
    }

    [Test]
    public void ParseTweak_WithScript_ParsesScriptAndUndoScript()
    {
        string yaml = """
            name: Hide This PC Folders
            category: Explorer/Navigation
            tags: [explorer]
            reversible: true
            script: |
              Remove-Item -Path 'HKLM:\test' -Recurse
            undo-script: |
              New-Item -Path 'HKLM:\test' -Force
            """;

        var result = _parser.ParseTweak(yaml, "hide-this-pc-folders");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Script, Does.Contain("Remove-Item"));
            Assert.That(result.Value!.UndoScript, Does.Contain("New-Item"));
            Assert.That(result.Value!.Registry, Is.Empty);
        });
    }

    [Test]
    public void ParseTweak_WithSuggests_ParsesSuggestsList()
    {
        string yaml = """
            name: Hide Copilot Button
            category: Taskbar/Declutter
            tags: [taskbar]
            reversible: true
            suggests: [disable-widgets, disable-chat-icon]
            registry:
              - key: HKCU\Software\Test
                name: ShowCopilotButton
                value: 0
                type: dword
            """;

        var result = _parser.ParseTweak(yaml, "disable-copilot-button");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Suggests, Has.Length.EqualTo(2));
        Assert.That(result.Value!.Suggests[0], Is.EqualTo("disable-widgets"));
    }

    [Test]
    public void ParseTweak_WithoutSuggests_SuggestsIsEmpty()
    {
        string yaml = """
            name: Test Tweak
            category: Test
            tags: [test]
            reversible: true
            registry:
              - key: HKCU\Software\Test
                name: TestValue
                value: 1
                type: dword
            """;

        var result = _parser.ParseTweak(yaml, "test");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Suggests, Is.Empty);
        Assert.That(result.Value!.Requires, Is.Empty);
    }

    [Test]
    public void ParseTweak_WithStringDefaultValue_CoercesCorrectly()
    {
        string yaml = """
            name: Disable Sticky Keys
            category: Accessibility/Keyboard
            tags: [accessibility]
            reversible: true
            registry:
              - key: HKCU\Control Panel\Accessibility\StickyKeys
                name: Flags
                value: "506"
                type: string
                default-value: "510"
            """;

        var result = _parser.ParseTweak(yaml, "disable-sticky-keys");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Registry[0].DefaultValue, Is.EqualTo("510"));
    }

    [Test]
    public void ParseApp_WithTweaks_ParsesAppOwnedTweaks()
    {
        string yaml = """
            name: Spotify
            category: Media/Players
            tags: [music]
            tweaks:
              - id: disable-autostart
                name: Disable Auto-Start
                description: Prevent Spotify from launching at startup
                registry:
                  - key: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
                    name: Spotify
                    value: null
                    type: string
                    default-value: null
            """;

        var result = _parser.ParseApp(yaml, "spotify");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Tweaks, Has.Length.EqualTo(1));
        var tweak = result.Value!.Tweaks[0];
        Assert.Multiple(() =>
        {
            Assert.That(tweak.Id, Is.EqualTo("disable-autostart"));
            Assert.That(tweak.Name, Is.EqualTo("Disable Auto-Start"));
            Assert.That(tweak.Registry, Has.Length.EqualTo(1));
            Assert.That(tweak.Registry[0].Value, Is.Null);
        });
    }

    [Test]
    public void ParseApp_WithoutTweaks_TweaksIsEmpty()
    {
        string yaml = """
            name: TestApp
            category: Test
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Tweaks, Is.Empty);
    }

    [Test]
    public void ParseApp_WithReversibleTweak_ParsesReversibleFlag()
    {
        string yaml = """
            name: Spotify
            category: Media/Players
            tags: [music]
            tweaks:
              - id: disable-autostart
                name: Disable Auto-Start
                reversible: true
                registry:
                  - key: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
                    name: Spotify
                    value: null
                    type: string
            """;

        var result = _parser.ParseApp(yaml, "spotify");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Tweaks[0].Reversible, Is.True);
    }

    [Test]
    public void ParseApp_TweakWithoutReversible_DefaultsToFalse()
    {
        string yaml = """
            name: TestApp
            category: Test
            tweaks:
              - id: test-tweak
                name: Test Tweak
                registry:
                  - key: HKCU\Software\Test
                    name: TestValue
                    value: 1
                    type: dword
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Tweaks[0].Reversible, Is.False);
    }

    [Test]
    public void AppOwnedTweak_ToTweakCatalogEntry_MapsAllFields()
    {
        string yaml = """
            name: Git
            category: Development/Version Control
            tags: [vcs, cli]
            profiles: [developer]
            license: GPL-2.0
            tweaks:
              - id: git-bash-here
                name: Git Bash Here
                description: Add context menu entry
                reversible: true
                registry:
                  - key: HKCR\Directory\Background\shell\git_shell\command
                    name: "(Default)"
                    value: "git-bash.exe"
                    type: string
            """;

        var result = _parser.ParseApp(yaml, "git");
        var tweak = result.Value!.Tweaks[0].ToTweakCatalogEntry(result.Value!);

        Assert.Multiple(() =>
        {
            Assert.That(tweak.Id, Is.EqualTo("git/git-bash-here"));
            Assert.That(tweak.Name, Is.EqualTo("Git Bash Here"));
            Assert.That(tweak.Category, Is.EqualTo("Development/Version Control"));
            Assert.That(tweak.Tags, Is.Empty);
            Assert.That(tweak.Description, Is.EqualTo("Add context menu entry"));
            Assert.That(tweak.Reversible, Is.True);
            Assert.That(tweak.Profiles, Is.EqualTo(new[] { "developer" }));
            Assert.That(tweak.Registry, Has.Length.EqualTo(1));
            Assert.That(tweak.Hidden, Is.False);
            Assert.That(tweak.License, Is.EqualTo("GPL-2.0"));
        });
    }

    [Test]
    public void AppOwnedTweak_ToTweakCatalogEntry_InheritsHiddenFromOwner()
    {
        string yaml = """
            name: HiddenApp
            category: Test
            hidden: true
            tweaks:
              - id: test-tweak
                name: Test
                registry:
                  - key: HKCU\Software\Test
                    name: Val
                    value: 1
                    type: dword
            """;

        var result = _parser.ParseApp(yaml, "hidden-app");
        var tweak = result.Value!.Tweaks[0].ToTweakCatalogEntry(result.Value!);

        Assert.That(tweak.Hidden, Is.True);
    }

    [Test]
    public void ParseApp_WithScriptTweak_ParsesScripts()
    {
        string yaml = """
            name: Cmder
            category: Development/Terminals
            tags: [terminal]
            tweaks:
              - id: open-here
                name: Open Cmder Here
                description: Add context menu entry
                script: |
                  New-Item -Path 'HKCR:\test' -Force
                undo-script: |
                  Remove-Item -Path 'HKCR:\test' -Recurse
            """;

        var result = _parser.ParseApp(yaml, "cmder");

        Assert.That(result.IsSuccess, Is.True);
        var tweak = result.Value!.Tweaks[0];
        Assert.Multiple(() =>
        {
            Assert.That(tweak.Script, Does.Contain("New-Item"));
            Assert.That(tweak.UndoScript, Does.Contain("Remove-Item"));
        });
    }

    [Test]
    public void ParseApp_WithAllNewSchemaFields_ParsesCorrectly()
    {
        string yaml = """
            name: Visual Studio Code
            kind: app
            category: Development/IDEs
            tags: [editor, ide]
            description: Source code editor
            profiles: [developer, power-user]
            os: [windows, linux, macos]
            hidden: false
            license: MIT
            install:
              winget: Microsoft.VisualStudio.Code
              choco: vscode
            alternatives: [sublimetext, notepadplusplus]
            suggests: [git, windows-terminal]
            requires: [dotnet-sdk]
            """;

        var result = _parser.ParseApp(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        var entry = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(entry.Kind, Is.EqualTo(CatalogKind.App));
            Assert.That(entry.Profiles, Is.EqualTo(new[] { "developer", "power-user" }));
            Assert.That(entry.Os, Is.EqualTo(new[] { "windows", "linux", "macos" }));
            Assert.That(entry.Hidden, Is.False);
            Assert.That(entry.License, Is.EqualTo("MIT"));
            Assert.That(entry.Alternatives, Is.EqualTo(new[] { "sublimetext", "notepadplusplus" }));
            Assert.That(entry.Suggests, Is.EqualTo(new[] { "git", "windows-terminal" }));
            Assert.That(entry.Requires, Is.EqualTo(new[] { "dotnet-sdk" }));
        });
    }

    [Test]
    public void ParseApp_WithoutNewFields_DefaultsAreCorrect()
    {
        string yaml = """
            name: TestApp
            category: Test
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        var entry = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(entry.Profiles, Is.Empty);
            Assert.That(entry.Os, Is.Empty);
            Assert.That(entry.Hidden, Is.False);
            Assert.That(entry.License, Is.Null);
            Assert.That(entry.Alternatives, Is.Empty);
            Assert.That(entry.Suggests, Is.Empty);
            Assert.That(entry.Requires, Is.Empty);
        });
    }

    [Test]
    public void ParseApp_WithHiddenTrue_SetsHidden()
    {
        string yaml = """
            name: PuTTY
            category: Networking
            hidden: true
            """;

        var result = _parser.ParseApp(yaml, "putty");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Hidden, Is.True);
    }

    [TestCase("cli-tool", CatalogKind.CliTool)]
    [TestCase("runtime", CatalogKind.Runtime)]
    [TestCase("dotfile", CatalogKind.Dotfile)]
    [TestCase("app", CatalogKind.App)]
    [TestCase(null, CatalogKind.App)]
    public void ParseApp_KindValues_ParseCorrectly(string? kind, CatalogKind expected)
    {
        string yaml = kind != null
            ? $"""
                name: TestApp
                kind: {kind}
                """
            : """
                name: TestApp
                """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Kind, Is.EqualTo(expected));
    }

    [Test]
    public void ParseApp_InstallWithDotnetToolAndNodePackage_ParsesCorrectly()
    {
        string yaml = """
            name: dotnet-ef
            kind: cli-tool
            category: Development/.NET
            install:
              dotnet-tool: dotnet-ef
            """;

        var result = _parser.ParseApp(yaml, "dotnet-ef");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Install!.DotnetTool, Is.EqualTo("dotnet-ef"));
            Assert.That(result.Value!.Install.Winget, Is.Null);
            Assert.That(result.Value!.Install.Choco, Is.Null);
            Assert.That(result.Value!.Install.NodePackage, Is.Null);
        });
    }

    [Test]
    public void ParseApp_InstallWithNodePackage_ParsesCorrectly()
    {
        string yaml = """
            name: TypeScript
            kind: cli-tool
            category: Development/Node
            install:
              node-package: typescript
            """;

        var result = _parser.ParseApp(yaml, "typescript");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Install!.NodePackage, Is.EqualTo("typescript"));
    }

    [Test]
    public void ParseFont_WithNewFields_ParsesCorrectly()
    {
        string yaml = """
            name: Fira Code
            category: Fonts/Programming
            tags: [monospace, ligatures]
            profiles: [developer]
            hidden: false
            license: OFL-1.1
            install:
              choco: firacode
            """;

        var result = _parser.ParseFont(yaml, "fira-code");

        Assert.That(result.IsSuccess, Is.True);
        var entry = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(entry.Profiles, Is.EqualTo(new[] { "developer" }));
            Assert.That(entry.Hidden, Is.False);
            Assert.That(entry.License, Is.EqualTo("OFL-1.1"));
        });
    }

    [Test]
    public void ParseTweak_WithAlternativesAndWindowsVersions_ParsesCorrectly()
    {
        string yaml = """
            name: Dark Mode
            category: Appearance/Theme
            tags: [theme]
            reversible: true
            profiles: [developer, power-user]
            hidden: false
            license: null
            alternatives: [light-mode]
            windows-versions: [10, 11]
            registry:
              - key: HKCU\Software\Test
                name: DarkMode
                value: 1
                type: dword
            """;

        var result = _parser.ParseTweak(yaml, "dark-mode");

        Assert.That(result.IsSuccess, Is.True);
        var entry = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(entry.Alternatives, Is.EqualTo(new[] { "light-mode" }));
            Assert.That(entry.WindowsVersions, Is.EqualTo(new[] { 10, 11 }));
            Assert.That(entry.Hidden, Is.False);
        });
    }

    [Test]
    public void ParseTweak_WithSource_ParsesSource()
    {
        string yaml = """
            name: Show File Extensions
            category: Developer Settings
            tags: [explorer]
            source: winutil
            reversible: true
            registry:
              - key: HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced
                name: HideFileExt
                value: 0
                type: dword
            """;

        var result = _parser.ParseTweak(yaml, "show-file-extensions");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Source, Is.EqualTo("winutil"));
    }

    [Test]
    public void ParseTweak_WithoutSource_SourceIsNull()
    {
        string yaml = """
            name: Test Tweak
            category: Test
            tags: [test]
            reversible: true
            registry:
              - key: HKCU\Software\Test
                name: TestValue
                value: 1
                type: dword
            """;

        var result = _parser.ParseTweak(yaml, "test");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Source, Is.Null);
    }

    [Test]
    public void ParseApp_InvalidYaml_ReturnsFailure()
    {
        var result = _parser.ParseApp("not: [valid: yaml: {{", "test");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("Invalid YAML"));
    }

    [Test]
    public void ParseFont_MissingName_ReturnsFailure()
    {
        string yaml = """
            category: Fonts
            """;

        var result = _parser.ParseFont(yaml, "test");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("missing 'name'"));
    }

    [Test]
    public void ParseFont_InvalidYaml_ReturnsFailure()
    {
        var result = _parser.ParseFont("not: [valid: yaml: {{", "test");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("Invalid YAML"));
    }

    [Test]
    public void ParseTweak_EmptyYaml_ReturnsFailure()
    {
        var result = _parser.ParseTweak("", "test");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo("YAML content is empty."));
    }

    [Test]
    public void ParseTweak_InvalidYaml_ReturnsFailure()
    {
        var result = _parser.ParseTweak("not: [valid: yaml: {{", "test");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("Invalid YAML"));
    }

    [Test]
    public void ParseTweak_MissingName_ReturnsFailure()
    {
        string yaml = """
            category: Test
            """;

        var result = _parser.ParseTweak(yaml, "test");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("missing 'name'"));
    }

    [Test]
    public void ParseGitHubStars_InvalidYaml_ReturnsEmptyDictionary()
    {
        var result = _parser.ParseGitHubStars("not: [valid: yaml: {{");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ParseIndex_InvalidYaml_ReturnsFailure()
    {
        var result = _parser.ParseIndex("not: [valid: yaml: {{");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("Invalid index"));
    }

    [Test]
    public void ParseApp_ConfigLinkMissingSource_SkipsLink()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - target:
                    windows: "%APPDATA%/TestApp/settings.json"
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config, Is.Null);
    }

    [Test]
    public void ParseApp_ConfigLinkMissingTarget_SkipsLink()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: settings.json
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config, Is.Null);
    }

    [Test]
    public void ParseApp_ConfigAllLinksInvalid_ConfigIsNull()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: ""
                  target:
                    windows: "%APPDATA%/TestApp"
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config, Is.Null);
    }

    [Test]
    public void ParseApp_CleanFilterWithJsonKeys_ParsesCorrectly()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: settings.json
                  target:
                    windows: "%APPDATA%/TestApp/settings.json"
              clean-filter:
                files: [settings.json]
                rules:
                  - type: strip-json-keys
                    keys: [window.zoomLevel, sync.gist]
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        var filter = result.Value!.Config!.CleanFilter!;
        Assert.Multiple(() =>
        {
            Assert.That(filter.Rules[0].Type, Is.EqualTo("strip-json-keys"));
            Assert.That(filter.Rules[0].Patterns, Has.Length.EqualTo(2));
        });
    }

    [Test]
    public void ParseApp_CleanFilterWithEmptyRuleType_SkipsRule()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: settings.json
                  target:
                    windows: "%APPDATA%/TestApp/settings.json"
              clean-filter:
                files: [settings.json]
                rules:
                  - type: ""
                    keys: [test]
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config!.CleanFilter, Is.Null);
    }

    [Test]
    public void ParseApp_CleanFilterNoValidRules_CleanFilterIsNull()
    {
        string yaml = """
            name: TestApp
            config:
              links:
                - source: settings.json
                  target:
                    windows: "%APPDATA%/TestApp/settings.json"
              clean-filter:
                files: [settings.json]
                rules:
                  - type: unknown-type
                    keys: [test]
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Config!.CleanFilter, Is.Null);
    }

    [Test]
    public void ParseApp_TweakMissingIdOrName_SkipsTweak()
    {
        string yaml = """
            name: TestApp
            category: Test
            tweaks:
              - name: Missing ID
                registry:
                  - key: HKCU\Software\Test
                    name: Val
                    value: 1
                    type: dword
              - id: missing-name
                registry:
                  - key: HKCU\Software\Test
                    name: Val
                    value: 1
                    type: dword
            """;

        var result = _parser.ParseApp(yaml, "testapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Tweaks, Is.Empty);
    }

    [Test]
    public void ParseTweak_RegistryMissingKeyOrName_SkipsEntry()
    {
        string yaml = """
            name: Test Tweak
            category: Test
            reversible: true
            registry:
              - name: Val
                value: 1
                type: dword
              - key: HKCU\Software\Test
                value: 1
                type: dword
            """;

        var result = _parser.ParseTweak(yaml, "test");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Registry, Is.Empty);
    }

    [Test]
    public void ParseTweak_ExpandStringType_CoercesCorrectly()
    {
        string yaml = """
            name: Test Tweak
            category: Test
            reversible: true
            registry:
              - key: HKCU\Software\Test
                name: Path
                value: "%SystemRoot%\\system32"
                type: expandstring
            """;

        var result = _parser.ParseTweak(yaml, "test");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Registry[0].Kind, Is.EqualTo(RegistryValueType.ExpandString));
            Assert.That(result.Value!.Registry[0].Value, Is.EqualTo("%SystemRoot%\\system32"));
        });
    }

    [Test]
    public void ParseTweak_QWordType_CoercesIntToLong()
    {
        string yaml = """
            name: Test Tweak
            category: Test
            reversible: true
            registry:
              - key: HKCU\Software\Test
                name: BigVal
                value: 42
                type: qword
            """;

        var result = _parser.ParseTweak(yaml, "test");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Registry[0].Value, Is.EqualTo(42L));
    }

    [Test]
    public void ParseTweak_DWordStringValue_CoercesToInt()
    {
        string yaml = """
            name: Test Tweak
            category: Test
            reversible: true
            registry:
              - key: HKCU\Software\Test
                name: Val
                value: "123"
                type: dword
            """;

        var result = _parser.ParseTweak(yaml, "test");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Registry[0].Value, Is.EqualTo(123));
    }

    [Test]
    public void ParseTweak_QWordStringValue_CoercesToLong()
    {
        string yaml = """
            name: Test Tweak
            category: Test
            reversible: true
            registry:
              - key: HKCU\Software\Test
                name: Val
                value: "9999999999"
                type: qword
            """;

        var result = _parser.ParseTweak(yaml, "test");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Registry[0].Value, Is.EqualTo(9999999999L));
    }

    [Test]
    public void ParseIndex_WithProfilesAndHidden_ParsesCorrectly()
    {
        string yaml = """
            apps:
              - id: vscode
                name: VS Code
                category: Development
                tags: [editor]
                profiles: [developer]
                hidden: false
              - id: putty
                name: PuTTY
                category: Networking
                tags: [ssh]
                hidden: true
            fonts: []
            tweaks: []
            """;

        var result = _parser.ParseIndex(yaml);

        Assert.That(result.IsSuccess, Is.True);
        var index = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(index.Apps[0].Profiles, Is.EqualTo(new[] { "developer" }));
            Assert.That(index.Apps[0].Hidden, Is.False);
            Assert.That(index.Apps[1].Hidden, Is.True);
        });
    }
}
