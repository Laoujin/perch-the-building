using System.Collections.Immutable;

using NSubstitute;

using Perch.Core.Modules;
using Perch.Core.Registry;

namespace Perch.Core.Tests.Registry;

[TestFixture]
public sealed class ThreeValueServiceTests
{
    private IRegistryProvider _registry = null!;
    private ThreeValueService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = Substitute.For<IRegistryProvider>();
        _service = new ThreeValueService(_registry);
    }

    [Test]
    public void Evaluate_CurrentMatchesDesired_ReturnsApplied()
    {
        var entry = new RegistryEntryDefinition("HKCU\\Test", "Value", 0, RegistryValueType.DWord, 1);
        _registry.GetValue("HKCU\\Test", "Value").Returns(0);

        var results = _service.Evaluate([entry]);

        Assert.That(results[0].Status, Is.EqualTo(ThreeValueStatus.Applied));
    }

    [Test]
    public void Evaluate_CurrentMatchesDefault_ReturnsNotApplied()
    {
        var entry = new RegistryEntryDefinition("HKCU\\Test", "Value", 0, RegistryValueType.DWord, 1);
        _registry.GetValue("HKCU\\Test", "Value").Returns(1);

        var results = _service.Evaluate([entry]);

        Assert.That(results[0].Status, Is.EqualTo(ThreeValueStatus.NotApplied));
    }

    [Test]
    public void Evaluate_CurrentMatchesNeitherDesiredNorDefault_ReturnsDrifted()
    {
        var entry = new RegistryEntryDefinition("HKCU\\Test", "Value", 0, RegistryValueType.DWord, 1);
        _registry.GetValue("HKCU\\Test", "Value").Returns(42);

        var results = _service.Evaluate([entry]);

        Assert.That(results[0].Status, Is.EqualTo(ThreeValueStatus.Drifted));
    }

    [Test]
    public void Evaluate_DesiredIsNullAndCurrentIsNull_ReturnsApplied()
    {
        var entry = new RegistryEntryDefinition("HKCU\\Test", "Value", null, RegistryValueType.String);
        _registry.GetValue("HKCU\\Test", "Value").Returns((object?)null);

        var results = _service.Evaluate([entry]);

        Assert.That(results[0].Status, Is.EqualTo(ThreeValueStatus.Applied));
    }

    [Test]
    public void Evaluate_DesiredIsValueButCurrentIsNull_ReturnsNotApplied()
    {
        var entry = new RegistryEntryDefinition("HKCU\\Test", "Value", 1, RegistryValueType.DWord, 0);
        _registry.GetValue("HKCU\\Test", "Value").Returns((object?)null);

        var results = _service.Evaluate([entry]);

        Assert.That(results[0].Status, Is.EqualTo(ThreeValueStatus.NotApplied));
    }

    [Test]
    public void Evaluate_RegistryThrows_ReturnsError()
    {
        var entry = new RegistryEntryDefinition("HKCU\\Test", "Value", 0, RegistryValueType.DWord, 1);
        _registry.GetValue("HKCU\\Test", "Value").Returns(_ => throw new InvalidOperationException("Access denied"));

        var results = _service.Evaluate([entry]);

        Assert.That(results[0].Status, Is.EqualTo(ThreeValueStatus.Error));
    }

    [Test]
    public void Evaluate_StringComparison_MatchesByToString()
    {
        var entry = new RegistryEntryDefinition("HKCU\\Test", "Flags", "506", RegistryValueType.String, "510");
        _registry.GetValue("HKCU\\Test", "Flags").Returns("506");

        var results = _service.Evaluate([entry]);

        Assert.That(results[0].Status, Is.EqualTo(ThreeValueStatus.Applied));
    }

    [Test]
    public void Evaluate_CurrentValueIsReturned()
    {
        var entry = new RegistryEntryDefinition("HKCU\\Test", "Value", 0, RegistryValueType.DWord, 1);
        _registry.GetValue("HKCU\\Test", "Value").Returns(42);

        var results = _service.Evaluate([entry]);

        Assert.That(results[0].CurrentValue, Is.EqualTo(42));
    }

    [Test]
    public void Evaluate_MultipleEntries_ReturnsResultForEach()
    {
        var entries = ImmutableArray.Create(
            new RegistryEntryDefinition("HKCU\\A", "V1", 0, RegistryValueType.DWord, 1),
            new RegistryEntryDefinition("HKCU\\B", "V2", 1, RegistryValueType.DWord, 0));
        _registry.GetValue("HKCU\\A", "V1").Returns(0);
        _registry.GetValue("HKCU\\B", "V2").Returns(0);

        var results = _service.Evaluate(entries);

        Assert.That(results, Has.Length.EqualTo(2));
        Assert.That(results[0].Status, Is.EqualTo(ThreeValueStatus.Applied));
        Assert.That(results[1].Status, Is.EqualTo(ThreeValueStatus.NotApplied));
    }

    [Test]
    public void Evaluate_DesiredIsNonNullButCurrentIsNull_NoDefault_ReturnsNotApplied()
    {
        var entry = new RegistryEntryDefinition("HKCU\\Test", "Value", "", RegistryValueType.String);
        _registry.GetValue("HKCU\\Test", "Value").Returns((object?)null);

        var results = _service.Evaluate([entry]);

        Assert.That(results[0].Status, Is.EqualTo(ThreeValueStatus.NotApplied));
    }
}
