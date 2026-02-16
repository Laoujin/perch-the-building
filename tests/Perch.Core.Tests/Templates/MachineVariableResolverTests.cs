using System.Collections.Immutable;
using Perch.Core.Templates;

namespace Perch.Core.Tests.Templates;

[TestFixture]
public sealed class MachineVariableResolverTests
{
    private MachineVariableResolver _resolver = null!;

    [SetUp]
    public void SetUp()
    {
        _resolver = new MachineVariableResolver();
    }

    [Test]
    public void Resolve_VariableInDictionary_ReturnsValue()
    {
        var variables = new Dictionary<string, string> { ["user.name"] = "Wouter" }
            .ToImmutableDictionary();

        var result = _resolver.Resolve("user.name", variables);

        Assert.That(result, Is.EqualTo("Wouter"));
    }

    [Test]
    public void Resolve_MachineName_ReturnsEnvironmentMachineName()
    {
        var result = _resolver.Resolve("machine.name", null);

        Assert.That(result, Is.EqualTo(Environment.MachineName));
    }

    [Test]
    public void Resolve_Platform_ReturnsCurrentPlatform()
    {
        var result = _resolver.Resolve("platform", null);

        Assert.That(result, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Resolve_Date_ReturnsIsoDate()
    {
        var result = _resolver.Resolve("date", null);

        Assert.That(result, Does.Match(@"\d{4}-\d{2}-\d{2}"));
    }

    [Test]
    public void Resolve_DictionaryValueShadowsBuiltin_DictionaryWins()
    {
        var variables = new Dictionary<string, string> { ["machine.name"] = "CUSTOM-PC" }
            .ToImmutableDictionary();

        var result = _resolver.Resolve("machine.name", variables);

        Assert.That(result, Is.EqualTo("CUSTOM-PC"));
    }

    [Test]
    public void Resolve_UnknownVariable_ReturnsNull()
    {
        var result = _resolver.Resolve("foo.bar", null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Resolve_UnknownVariableWithDictionary_ReturnsNull()
    {
        var variables = new Dictionary<string, string> { ["user.name"] = "Wouter" }
            .ToImmutableDictionary();

        var result = _resolver.Resolve("nonexistent", variables);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Resolve_UserEmail_FromVariables()
    {
        var variables = new Dictionary<string, string> { ["user.email"] = "w@example.com" }
            .ToImmutableDictionary();

        var result = _resolver.Resolve("user.email", variables);

        Assert.That(result, Is.EqualTo("w@example.com"));
    }
}
