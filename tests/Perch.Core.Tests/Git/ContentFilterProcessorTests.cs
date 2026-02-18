using System.Collections.Immutable;
using Perch.Core.Git;

namespace Perch.Core.Tests.Git;

[TestFixture]
public sealed class ContentFilterProcessorTests
{
    private ContentFilterProcessor _processor = null!;

    [SetUp]
    public void SetUp()
    {
        _processor = new ContentFilterProcessor();
    }

    [Test]
    public void Apply_StripXmlElements_RemovesSingleElement()
    {
        string content = """
            <Config>
                <Settings>keep</Settings>
                <FindHistory>
                    <entry>search1</entry>
                </FindHistory>
            </Config>
            """;

        var rules = ImmutableArray.Create(new FilterRule("strip-xml-elements", ImmutableArray.Create("FindHistory")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("<Settings>keep</Settings>"));
            Assert.That(result, Does.Not.Contain("FindHistory"));
            Assert.That(result, Does.Not.Contain("search1"));
        });
    }

    [Test]
    public void Apply_StripXmlElements_RemovesMultipleElements()
    {
        string content = """
            <Config>
                <Settings>keep</Settings>
                <FindHistory>
                    <entry>search1</entry>
                </FindHistory>
                <Session>
                    <window>1</window>
                </Session>
            </Config>
            """;

        var rules = ImmutableArray.Create(new FilterRule("strip-xml-elements", ImmutableArray.Create("FindHistory", "Session")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("<Settings>keep</Settings>"));
            Assert.That(result, Does.Not.Contain("FindHistory"));
            Assert.That(result, Does.Not.Contain("Session"));
        });
    }

    [Test]
    public void Apply_StripXmlElements_NoMatchingElements_ReturnsUnchanged()
    {
        string content = "<Config><Settings>keep</Settings></Config>";

        var rules = ImmutableArray.Create(new FilterRule("strip-xml-elements", ImmutableArray.Create("FindHistory")));

        string result = _processor.Apply(content, rules);

        Assert.That(result, Is.EqualTo(content));
    }

    [Test]
    public void Apply_StripXmlElements_SelfClosingElement_Removed()
    {
        string content = "<Config>\n    <Settings>keep</Settings>\n    <Volatile />\n</Config>";

        var rules = ImmutableArray.Create(new FilterRule("strip-xml-elements", ImmutableArray.Create("Volatile")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("<Settings>keep</Settings>"));
            Assert.That(result, Does.Not.Contain("Volatile"));
        });
    }

    [Test]
    public void Apply_StripIniKeys_RemovesSingleKey()
    {
        string content = "[Settings]\nTheme=dark\nLastOpened=2024-01-15\nFontSize=12\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-ini-keys", ImmutableArray.Create("LastOpened")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Theme=dark"));
            Assert.That(result, Does.Contain("FontSize=12"));
            Assert.That(result, Does.Not.Contain("LastOpened"));
        });
    }

    [Test]
    public void Apply_StripIniKeys_RemovesMultipleKeys()
    {
        string content = "[Settings]\nTheme=dark\nLastOpened=2024-01-15\nWindowPosition=100,200\nFontSize=12\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-ini-keys", ImmutableArray.Create("LastOpened", "WindowPosition")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Theme=dark"));
            Assert.That(result, Does.Contain("FontSize=12"));
            Assert.That(result, Does.Not.Contain("LastOpened"));
            Assert.That(result, Does.Not.Contain("WindowPosition"));
        });
    }

    [Test]
    public void Apply_StripIniKeys_NoMatchingKeys_ReturnsUnchanged()
    {
        string content = "[Settings]\nTheme=dark\nFontSize=12\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-ini-keys", ImmutableArray.Create("LastOpened")));

        string result = _processor.Apply(content, rules);

        Assert.That(result, Is.EqualTo(content));
    }

    [Test]
    public void Apply_StripIniKeys_KeyWithSpacesAroundEquals()
    {
        string content = "[Settings]\nTheme = dark\nLastOpened = 2024-01-15\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-ini-keys", ImmutableArray.Create("LastOpened")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Theme = dark"));
            Assert.That(result, Does.Not.Contain("LastOpened"));
        });
    }

    [Test]
    public void Apply_MultipleRulesInSequence_AppliesAll()
    {
        string content = """
            <Config>
                <Settings>
                    Theme=dark
                    LastOpened=2024-01-15
                </Settings>
                <FindHistory>
                    <entry>search1</entry>
                </FindHistory>
            </Config>
            """;

        var rules = ImmutableArray.Create(
            new FilterRule("strip-xml-elements", ImmutableArray.Create("FindHistory")),
            new FilterRule("strip-ini-keys", ImmutableArray.Create("LastOpened")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Theme=dark"));
            Assert.That(result, Does.Not.Contain("FindHistory"));
            Assert.That(result, Does.Not.Contain("LastOpened"));
        });
    }

    [Test]
    public void Apply_EmptyRules_ReturnsUnchanged()
    {
        string content = "some content";

        string result = _processor.Apply(content, ImmutableArray<FilterRule>.Empty);

        Assert.That(result, Is.EqualTo(content));
    }

    [Test]
    public void Apply_DefaultRules_ReturnsUnchanged()
    {
        string content = "some content";

        string result = _processor.Apply(content, default);

        Assert.That(result, Is.EqualTo(content));
    }

    [Test]
    public void Apply_StripJsonKeys_RemovesSingleKey()
    {
        string content = "{\n    \"editor.fontSize\": 14,\n    \"window.zoomLevel\": 2,\n    \"editor.tabSize\": 4\n}\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-json-keys", ImmutableArray.Create("window.zoomLevel")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("\"editor.fontSize\": 14"));
            Assert.That(result, Does.Contain("\"editor.tabSize\": 4"));
            Assert.That(result, Does.Not.Contain("window.zoomLevel"));
        });
    }

    [Test]
    public void Apply_StripJsonKeys_RemovesMultipleKeys()
    {
        string content = "{\n    \"editor.fontSize\": 14,\n    \"window.zoomLevel\": 2,\n    \"sync.gist\": \"abc123\",\n    \"editor.tabSize\": 4\n}\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-json-keys", ImmutableArray.Create("window.zoomLevel", "sync.gist")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("\"editor.fontSize\": 14"));
            Assert.That(result, Does.Contain("\"editor.tabSize\": 4"));
            Assert.That(result, Does.Not.Contain("window.zoomLevel"));
            Assert.That(result, Does.Not.Contain("sync.gist"));
        });
    }

    [Test]
    public void Apply_StripJsonKeys_NoMatchingKeys_ReturnsUnchanged()
    {
        string content = "{\n    \"editor.fontSize\": 14\n}\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-json-keys", ImmutableArray.Create("window.zoomLevel")));

        string result = _processor.Apply(content, rules);

        Assert.That(result, Is.EqualTo(content));
    }

    [Test]
    public void Apply_StripJsonKeys_StringValue()
    {
        string content = "{\n    \"julia.executablePath\": \"C:\\\\Julia\\\\bin\\\\julia.exe\",\n    \"editor.fontSize\": 14\n}\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-json-keys", ImmutableArray.Create("julia.executablePath")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("\"editor.fontSize\": 14"));
            Assert.That(result, Does.Not.Contain("julia.executablePath"));
        });
    }

    [Test]
    public void Apply_StripJsonKeys_NestedObjectValue()
    {
        string content = "{\n    \"editor.fontSize\": 14,\n    \"workbench.colorCustomizations\": {\n        \"statusBar.background\": \"#1e1e1e\"\n    },\n    \"editor.tabSize\": 4\n}\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-json-keys", ImmutableArray.Create("workbench.colorCustomizations")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("\"editor.fontSize\": 14"));
            Assert.That(result, Does.Contain("\"editor.tabSize\": 4"));
            Assert.That(result, Does.Not.Contain("workbench.colorCustomizations"));
            Assert.That(result, Does.Not.Contain("statusBar.background"));
        });
    }

    [Test]
    public void Apply_StripJsonKeys_ArrayValue()
    {
        string content = "{\n    \"editor.fontSize\": 14,\n    \"files.exclude\": [\n        \"**/.git\",\n        \"**/node_modules\"\n    ],\n    \"editor.tabSize\": 4\n}\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-json-keys", ImmutableArray.Create("files.exclude")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("\"editor.fontSize\": 14"));
            Assert.That(result, Does.Contain("\"editor.tabSize\": 4"));
            Assert.That(result, Does.Not.Contain("files.exclude"));
        });
    }

    [Test]
    public void Apply_StripJsonKeys_LastKeyNoTrailingComma()
    {
        string content = "{\n    \"editor.fontSize\": 14,\n    \"window.zoomLevel\": 2\n}\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-json-keys", ImmutableArray.Create("window.zoomLevel")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("\"editor.fontSize\": 14"));
            Assert.That(result, Does.Not.Contain("window.zoomLevel"));
        });
    }

    [Test]
    public void Apply_StripJsonKeys_BooleanAndNullValues()
    {
        string content = "{\n    \"editor.minimap.enabled\": true,\n    \"editor.wordWrap\": \"off\",\n    \"editor.nullSetting\": null\n}\n";

        var rules = ImmutableArray.Create(new FilterRule("strip-json-keys", ImmutableArray.Create("editor.minimap.enabled", "editor.nullSetting")));

        string result = _processor.Apply(content, rules);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("\"editor.wordWrap\": \"off\""));
            Assert.That(result, Does.Not.Contain("editor.minimap.enabled"));
            Assert.That(result, Does.Not.Contain("editor.nullSetting"));
        });
    }

    [Test]
    public void Apply_UnknownRuleType_SkipsRule()
    {
        string content = "some content";

        var rules = ImmutableArray.Create(new FilterRule("unknown-type", ImmutableArray.Create("pattern")));

        string result = _processor.Apply(content, rules);

        Assert.That(result, Is.EqualTo(content));
    }
}
