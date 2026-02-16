using Perch.Core.Templates;

namespace Perch.Core.Tests.Templates;

[TestFixture]
public sealed class TemplateProcessorTests
{
    private TemplateProcessor _processor = null!;

    [SetUp]
    public void SetUp()
    {
        _processor = new TemplateProcessor();
    }

    [Test]
    public void ContainsPlaceholders_WithOpReference_ReturnsTrue()
    {
        Assert.That(_processor.ContainsPlaceholders("token = {{op://vault/item/field}}"), Is.True);
    }

    [Test]
    public void ContainsPlaceholders_WithoutPlaceholders_ReturnsFalse()
    {
        Assert.That(_processor.ContainsPlaceholders("just plain text"), Is.False);
    }

    [Test]
    public void ContainsPlaceholders_EmptyString_ReturnsFalse()
    {
        Assert.That(_processor.ContainsPlaceholders(""), Is.False);
    }

    [Test]
    public void ContainsPlaceholders_MultiplePlaceholders_ReturnsTrue()
    {
        string content = "user={{op://vault/item/user}}\npass={{op://vault/item/pass}}";
        Assert.That(_processor.ContainsPlaceholders(content), Is.True);
    }

    [Test]
    public void FindReferences_SinglePlaceholder_ReturnsReference()
    {
        IReadOnlyList<string> refs = _processor.FindReferences("key = {{op://vault/item/field}}");
        Assert.That(refs, Is.EqualTo(new[] { "op://vault/item/field" }));
    }

    [Test]
    public void FindReferences_MultiplePlaceholders_ReturnsAll()
    {
        string content = "a={{op://v/i/user}} b={{op://v/i/pass}}";
        IReadOnlyList<string> refs = _processor.FindReferences(content);
        Assert.That(refs, Is.EqualTo(new[] { "op://v/i/user", "op://v/i/pass" }));
    }

    [Test]
    public void FindReferences_DuplicatePlaceholders_ReturnsDistinct()
    {
        string content = "a={{op://v/i/f}} b={{op://v/i/f}}";
        IReadOnlyList<string> refs = _processor.FindReferences(content);
        Assert.That(refs, Is.EqualTo(new[] { "op://v/i/f" }));
    }

    [Test]
    public void FindReferences_NoPlaceholders_ReturnsEmpty()
    {
        IReadOnlyList<string> refs = _processor.FindReferences("no placeholders here");
        Assert.That(refs, Is.Empty);
    }

    [Test]
    public void ReplacePlaceholders_SinglePlaceholder_ReplacesValue()
    {
        string content = "token = {{op://vault/item/field}}";
        var values = new Dictionary<string, string> { ["op://vault/item/field"] = "abc123" };

        string result = _processor.ReplacePlaceholders(content, values);

        Assert.That(result, Is.EqualTo("token = abc123"));
    }

    [Test]
    public void ReplacePlaceholders_MultiplePlaceholders_ReplacesAll()
    {
        string content = "user={{op://v/i/user}}\npass={{op://v/i/pass}}";
        var values = new Dictionary<string, string>
        {
            ["op://v/i/user"] = "admin",
            ["op://v/i/pass"] = "hunter2",
        };

        string result = _processor.ReplacePlaceholders(content, values);

        Assert.That(result, Is.EqualTo("user=admin\npass=hunter2"));
    }

    [Test]
    public void ReplacePlaceholders_DuplicatePlaceholder_ReplacesAllOccurrences()
    {
        string content = "a={{op://v/i/f}} b={{op://v/i/f}}";
        var values = new Dictionary<string, string> { ["op://v/i/f"] = "val" };

        string result = _processor.ReplacePlaceholders(content, values);

        Assert.That(result, Is.EqualTo("a=val b=val"));
    }

    [Test]
    public void ReplacePlaceholders_MissingValue_LeavesPlaceholder()
    {
        string content = "token = {{op://vault/item/field}}";
        var values = new Dictionary<string, string>();

        string result = _processor.ReplacePlaceholders(content, values);

        Assert.That(result, Is.EqualTo("token = {{op://vault/item/field}}"));
    }

    [Test]
    public void ContainsPlaceholders_IncompletePattern_ReturnsFalse()
    {
        Assert.That(_processor.ContainsPlaceholders("{{op://missing-close-brace}"), Is.False);
    }

    [Test]
    public void ContainsPlaceholders_GeneralVariable_ReturnsTrue()
    {
        Assert.That(_processor.ContainsPlaceholders("name = {{user.name}}"), Is.True);
    }

    [Test]
    public void ContainsPlaceholders_MixedOpAndVariable_ReturnsTrue()
    {
        Assert.That(_processor.ContainsPlaceholders("{{op://v/i}} {{user.name}}"), Is.True);
    }

    [Test]
    public void FindVariables_SingleVariable_ReturnsIt()
    {
        var vars = _processor.FindVariables("Hello {{user.name}}!");

        Assert.That(vars, Is.EqualTo(new[] { "user.name" }));
    }

    [Test]
    public void FindVariables_ExcludesOpReferences()
    {
        var vars = _processor.FindVariables("{{op://vault/item}} and {{machine.name}}");

        Assert.That(vars, Is.EqualTo(new[] { "machine.name" }));
    }

    [Test]
    public void FindVariables_NoVariables_ReturnsEmpty()
    {
        var vars = _processor.FindVariables("just {{op://vault/item}}");

        Assert.That(vars, Is.Empty);
    }

    [Test]
    public void FindVariables_DuplicateVariables_ReturnsDistinct()
    {
        var vars = _processor.FindVariables("{{user.name}} and {{user.name}} again");

        Assert.That(vars, Is.EqualTo(new[] { "user.name" }));
    }

    [Test]
    public void FindReferences_StillReturnsOnlyOpReferences()
    {
        var refs = _processor.FindReferences("{{op://v/i/f}} and {{user.name}}");

        Assert.That(refs, Is.EqualTo(new[] { "op://v/i/f" }));
    }

    [Test]
    public void ReplacePlaceholders_MixedOpAndVariable_ReplacesBoth()
    {
        string content = "user={{user.name}} token={{op://v/i/f}}";
        var values = new Dictionary<string, string>
        {
            ["user.name"] = "Wouter",
            ["op://v/i/f"] = "secret",
        };

        string result = _processor.ReplacePlaceholders(content, values);

        Assert.That(result, Is.EqualTo("user=Wouter token=secret"));
    }

    [Test]
    public void ReplacePlaceholders_UnresolvedVariable_LeavesPlaceholder()
    {
        string content = "name={{unknown.var}}";
        var values = new Dictionary<string, string>();

        string result = _processor.ReplacePlaceholders(content, values);

        Assert.That(result, Is.EqualTo("name={{unknown.var}}"));
    }
}
