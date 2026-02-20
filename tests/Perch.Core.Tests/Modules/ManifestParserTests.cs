using Perch.Core;
using Perch.Core.Modules;
using Perch.Core.Registry;

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
        Assert.That(result.Error, Does.Contain("actionable section"));
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

    [Test]
    public void Parse_CleanFilterWithRules_ReturnsRules()
    {
        string yaml = """
            clean-filter:
              name: notepadplusplus-clean
              files:
                - config.xml
              rules:
                - type: strip-xml-elements
                  elements:
                    - FindHistory
                    - Session
            links:
              - source: config.xml
                target: "%APPDATA%\\Notepad++\\config.xml"
            """;

        var result = _parser.Parse(yaml, "notepadplusplus");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.CleanFilter, Is.Not.Null);
            Assert.That(result.Manifest!.CleanFilter!.Name, Is.EqualTo("notepadplusplus-clean"));
            Assert.That(result.Manifest!.CleanFilter!.Script, Is.Null);
            Assert.That(result.Manifest!.CleanFilter!.Rules, Has.Length.EqualTo(1));
            Assert.That(result.Manifest!.CleanFilter!.Rules[0].Type, Is.EqualTo("strip-xml-elements"));
            Assert.That(result.Manifest!.CleanFilter!.Rules[0].Patterns, Is.EqualTo(new[] { "FindHistory", "Session" }));
        });
    }

    [Test]
    public void Parse_CleanFilterWithMultipleRuleTypes_ParsesAll()
    {
        string yaml = """
            clean-filter:
              name: app-clean
              files:
                - config.xml
                - settings.ini
              rules:
                - type: strip-xml-elements
                  elements:
                    - FindHistory
                - type: strip-ini-keys
                  keys:
                    - LastOpened
                    - WindowPosition
            links:
              - source: config.xml
                target: "%APPDATA%\\App\\config.xml"
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.CleanFilter!.Rules, Has.Length.EqualTo(2));
            Assert.That(result.Manifest!.CleanFilter!.Rules[0].Type, Is.EqualTo("strip-xml-elements"));
            Assert.That(result.Manifest!.CleanFilter!.Rules[1].Type, Is.EqualTo("strip-ini-keys"));
            Assert.That(result.Manifest!.CleanFilter!.Rules[1].Patterns, Is.EqualTo(new[] { "LastOpened", "WindowPosition" }));
        });
    }

    [Test]
    public void Parse_CleanFilterNoScriptNoRules_ReturnsNull()
    {
        string yaml = """
            clean-filter:
              name: empty-filter
              files:
                - data.json
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.CleanFilter, Is.Null);
    }

    [Test]
    public void Parse_GlobalPackagesWithBun_ReturnsParsedPackages()
    {
        string yaml = """
            global-packages:
              manager: bun
              packages:
                - eslint_d
                - prettier
                - tsx
            """;

        var result = _parser.Parse(yaml, "bun-packages");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.GlobalPackages, Is.Not.Null);
            Assert.That(result.Manifest!.GlobalPackages!.Manager, Is.EqualTo(GlobalPackageManager.Bun));
            Assert.That(result.Manifest!.GlobalPackages!.Packages, Has.Length.EqualTo(3));
            Assert.That(result.Manifest!.GlobalPackages!.Packages[0], Is.EqualTo("eslint_d"));
        });
    }

    [Test]
    public void Parse_GlobalPackagesDefaultsToNpm()
    {
        string yaml = """
            global-packages:
              packages:
                - prettier
            """;

        var result = _parser.Parse(yaml, "npm-packages");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.GlobalPackages!.Manager, Is.EqualTo(GlobalPackageManager.Npm));
    }

    [Test]
    public void Parse_GlobalPackagesOnly_NoLinksRequired()
    {
        string yaml = """
            global-packages:
              manager: bun
              packages:
                - tsx
            """;

        var result = _parser.Parse(yaml, "bun-packages");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Links, Is.Empty);
        Assert.That(result.Manifest!.GlobalPackages, Is.Not.Null);
    }

    [Test]
    public void Parse_NoGlobalPackages_ReturnsNull()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.GlobalPackages, Is.Null);
    }

    [Test]
    public void Parse_EnabledDefaultsToTrue()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Enabled, Is.True);
    }

    [Test]
    public void Parse_EnabledFalse_ReturnsFalse()
    {
        string yaml = """
            enabled: false
            global-packages:
              manager: bun
              packages:
                - tsx
            """;

        var result = _parser.Parse(yaml, "disabled-module");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Enabled, Is.False);
    }

    [Test]
    public void Parse_VscodeExtensions_ReturnsParsedList()
    {
        string yaml = """
            vscode-extensions:
              - ms-dotnettools.csharp
              - esbenp.prettier-vscode
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.VscodeExtensions, Has.Length.EqualTo(2));
            Assert.That(result.Manifest!.VscodeExtensions[0], Is.EqualTo("ms-dotnettools.csharp"));
            Assert.That(result.Manifest!.VscodeExtensions[1], Is.EqualTo("esbenp.prettier-vscode"));
        });
    }

    [Test]
    public void Parse_VscodeExtensionsOnly_NoLinksRequired()
    {
        string yaml = """
            vscode-extensions:
              - ms-dotnettools.csharp
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Links, Is.Empty);
    }

    [Test]
    public void Parse_PsModules_ReturnsParsedList()
    {
        string yaml = """
            ps-modules:
              - posh-git
              - PSReadLine
            """;

        var result = _parser.Parse(yaml, "powershell");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.PsModules, Has.Length.EqualTo(2));
            Assert.That(result.Manifest!.PsModules[0], Is.EqualTo("posh-git"));
            Assert.That(result.Manifest!.PsModules[1], Is.EqualTo("PSReadLine"));
        });
    }

    [Test]
    public void Parse_PsModulesOnly_NoLinksRequired()
    {
        string yaml = """
            ps-modules:
              - posh-git
            """;

        var result = _parser.Parse(yaml, "powershell");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Links, Is.Empty);
    }

    [Test]
    public void Parse_NoVscodeExtensions_ReturnsEmptyArray()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.VscodeExtensions, Is.Empty);
    }

    [Test]
    public void Parse_NoPsModules_ReturnsEmptyArray()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.PsModules, Is.Empty);
    }

    [Test]
    public void Parse_LinkWithTemplateTrue_SetsIsTemplate()
    {
        string yaml = """
            links:
              - source: .gitconfig.template
                target: "%USERPROFILE%\\.gitconfig"
                template: true
            """;

        var result = _parser.Parse(yaml, "git");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Links[0].IsTemplate, Is.True);
    }

    [Test]
    public void Parse_LinkWithoutTemplate_DefaultsToFalse()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\Code\\User\\settings.json"
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Links[0].IsTemplate, Is.False);
    }

    [Test]
    public void Parse_MixedTemplateAndNonTemplateLinks_ParsesBothCorrectly()
    {
        string yaml = """
            links:
              - source: .gitconfig.template
                target: "%USERPROFILE%\\.gitconfig"
                template: true
              - source: .gitignore_global
                target: "%USERPROFILE%\\.gitignore_global"
            """;

        var result = _parser.Parse(yaml, "git");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.Links[0].IsTemplate, Is.True);
            Assert.That(result.Manifest!.Links[1].IsTemplate, Is.False);
        });
    }

    [Test]
    public void Parse_GalleryField_ReturnsGalleryId()
    {
        string yaml = """
            gallery: vscode
            links:
              - source: settings.json
                target: "%APPDATA%\\Code\\User\\settings.json"
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.GalleryId, Is.EqualTo("vscode"));
    }

    [Test]
    public void Parse_GalleryOnlyManifest_IsValid()
    {
        string yaml = """
            gallery: vscode
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.GalleryId, Is.EqualTo("vscode"));
        Assert.That(result.Manifest.Links, Is.Empty);
    }

    [Test]
    public void Parse_NoGalleryField_GalleryIdIsNull()
    {
        string yaml = """
            links:
              - source: settings.json
                target: "%APPDATA%\\Code\\User\\settings.json"
            """;

        var result = _parser.Parse(yaml, "vscode");

        Assert.That(result.Manifest!.GalleryId, Is.Null);
    }

    [Test]
    public void Parse_LinkWithInvalidPlatformTarget_ReturnsError()
    {
        string yaml = """
            links:
              - source: settings.json
                target:
                  invalid_platform: "/some/path"
            """;

        var result = _parser.Parse(yaml, "test");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("invalid 'target'"));
    }

    [Test]
    public void Parse_HooksWithEmptyValues_ReturnsNullHooks()
    {
        string yaml = """
            hooks:
              pre-deploy: ""
              post-deploy: ""
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Hooks, Is.Null);
    }

    [Test]
    public void Parse_CleanFilterMissingName_ReturnsNullCleanFilter()
    {
        string yaml = """
            clean-filter:
              script: scripts/clean.sh
              files:
                - data.json
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.CleanFilter, Is.Null);
    }

    [Test]
    public void Parse_CleanFilterMissingFiles_ReturnsNullCleanFilter()
    {
        string yaml = """
            clean-filter:
              name: test-filter
              script: scripts/clean.sh
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "myapp");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.CleanFilter, Is.Null);
    }

    [Test]
    public void Parse_CleanFilterWithIniKeyRules_ParsesCorrectly()
    {
        string yaml = """
            clean-filter:
              name: ini-filter
              files:
                - settings.ini
              rules:
                - type: strip-ini-keys
                  keys:
                    - LastOpened
                    - WindowPosition
            links:
              - source: settings.ini
                target: "%APPDATA%\\App\\settings.ini"
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.CleanFilter!.Rules, Has.Length.EqualTo(1));
            Assert.That(result.Manifest!.CleanFilter!.Rules[0].Type, Is.EqualTo("strip-ini-keys"));
            Assert.That(result.Manifest!.CleanFilter!.Rules[0].Patterns, Has.Length.EqualTo(2));
        });
    }

    [Test]
    public void Parse_CleanFilterWithJsonKeyRules_ParsesCorrectly()
    {
        string yaml = """
            clean-filter:
              name: json-filter
              files:
                - settings.json
              rules:
                - type: strip-json-keys
                  keys:
                    - window.zoomLevel
                    - sync.gist
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.CleanFilter!.Rules, Has.Length.EqualTo(1));
            Assert.That(result.Manifest!.CleanFilter!.Rules[0].Type, Is.EqualTo("strip-json-keys"));
        });
    }

    [Test]
    public void Parse_RegistryMissingKeyOrNameOrValue_SkipsEntry()
    {
        string yaml = """
            registry:
              - name: Val
                value: 1
                type: dword
              - key: HKCU\Software\Test
                value: 1
                type: dword
              - key: HKCU\Software\Test
                name: Val
                type: dword
            links:
              - source: settings.json
                target: "%APPDATA%\\App\\settings.json"
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Registry, Is.Empty);
    }

    [Test]
    public void Parse_RegistryDWordType_CoercesCorrectly()
    {
        string yaml = """
            registry:
              - key: HKCU\Software\Test
                name: Val
                value: 42
                type: dword
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.Registry[0].Kind, Is.EqualTo(RegistryValueType.DWord));
            Assert.That(result.Manifest!.Registry[0].Value, Is.EqualTo(42));
        });
    }

    [Test]
    public void Parse_RegistryExpandStringType_ParsesCorrectly()
    {
        string yaml = """
            registry:
              - key: HKCU\Software\Test
                name: Path
                value: "%SystemRoot%\\system32"
                type: expandstring
            """;

        var result = _parser.Parse(yaml, "app");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Manifest!.Registry[0].Kind, Is.EqualTo(RegistryValueType.ExpandString));
    }
}
