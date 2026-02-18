using System.Collections.Immutable;

using NSubstitute;

using Perch.Core.Catalog;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Tweaks;

namespace Perch.Core.Tests.Tweaks;

[TestFixture]
public sealed class TweakServiceDetectTests
{
    private IRegistryProvider _registry = null!;
    private TweakService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = Substitute.For<IRegistryProvider>();
        _service = new TweakService(_registry);
    }

    [Test]
    public void Detect_AllEntriesMatch_ReturnsApplied()
    {
        _registry.GetValue(@"HKCU\Software\Test", "Value1").Returns(1);
        _registry.GetValue(@"HKCU\Software\Test", "Value2").Returns("hello");

        var tweak = MakeTweak(
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord),
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value2", "hello", RegistryValueType.String));

        var result = _service.Detect(tweak);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TweakStatus.Applied));
            Assert.That(result.Entries, Has.Length.EqualTo(2));
            Assert.That(result.Entries[0].IsApplied, Is.True);
            Assert.That(result.Entries[1].IsApplied, Is.True);
        });
    }

    [Test]
    public void Detect_NoEntriesMatch_ReturnsNotApplied()
    {
        _registry.GetValue(@"HKCU\Software\Test", "Value1").Returns(0);

        var tweak = MakeTweak(
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord));

        var result = _service.Detect(tweak);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TweakStatus.NotApplied));
            Assert.That(result.Entries[0].IsApplied, Is.False);
            Assert.That(result.Entries[0].CurrentValue, Is.EqualTo(0));
        });
    }

    [Test]
    public void Detect_SomeEntriesMatch_ReturnsPartial()
    {
        _registry.GetValue(@"HKCU\Software\Test", "Value1").Returns(1);
        _registry.GetValue(@"HKCU\Software\Test", "Value2").Returns(99);

        var tweak = MakeTweak(
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord),
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value2", 42, RegistryValueType.DWord));

        var result = _service.Detect(tweak);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TweakStatus.Partial));
            Assert.That(result.Entries[0].IsApplied, Is.True);
            Assert.That(result.Entries[1].IsApplied, Is.False);
        });
    }

    [Test]
    public void Detect_MissingRegistryKey_NotApplied()
    {
        _registry.GetValue(@"HKCU\Software\Test", "Value1").Returns((object?)null);

        var tweak = MakeTweak(
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord));

        var result = _service.Detect(tweak);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TweakStatus.NotApplied));
            Assert.That(result.Entries[0].CurrentValue, Is.Null);
            Assert.That(result.Entries[0].IsApplied, Is.False);
        });
    }

    [Test]
    public void Detect_EmptyRegistryArray_ReturnsApplied()
    {
        var tweak = new TweakCatalogEntry(
            "empty", "Empty", "Test", [], null, true, [], ImmutableArray<RegistryEntryDefinition>.Empty);

        var result = _service.Detect(tweak);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TweakStatus.Applied));
            Assert.That(result.Entries, Is.Empty);
        });
    }

    private static TweakCatalogEntry MakeTweak(params RegistryEntryDefinition[] entries) =>
        new("test-tweak", "Test Tweak", "Test", [], null, true, [],
            entries.ToImmutableArray());
}
