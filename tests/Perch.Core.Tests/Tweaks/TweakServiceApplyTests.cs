using System.Collections.Immutable;

using NSubstitute;

using Perch.Core.Catalog;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Tweaks;

namespace Perch.Core.Tests.Tweaks;

[TestFixture]
public sealed class TweakServiceApplyTests
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
    public void Apply_SetsRegistryValues()
    {
        _registry.GetValue(@"HKCU\Software\Test", "Value1").Returns(0);

        var tweak = MakeTweak(
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord));

        var result = _service.Apply(tweak);

        Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
        _registry.Received(1).SetValue(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord);
    }

    [Test]
    public void Apply_SkipsAlreadyAppliedEntries()
    {
        _registry.GetValue(@"HKCU\Software\Test", "Value1").Returns(1);

        var tweak = MakeTweak(
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord));

        var result = _service.Apply(tweak);

        Assert.That(result.Entries[0].Message, Does.Contain("Already set"));
        _registry.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<RegistryValueType>());
    }

    [Test]
    public void Apply_DryRun_DoesNotWrite()
    {
        var tweak = MakeTweak(
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord));

        var result = _service.Apply(tweak, dryRun: true);

        Assert.That(result.Entries[0].Message, Does.Contain("Would set"));
        _registry.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<RegistryValueType>());
        _registry.DidNotReceive().GetValue(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public void Apply_NullValue_DeletesEntry()
    {
        _registry.GetValue(@"HKCU\Software\Test", "Value1").Returns("old");

        var tweak = MakeTweak(
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", null, RegistryValueType.String));

        var result = _service.Apply(tweak);

        Assert.That(result.Entries[0].Message, Does.Contain("Deleted"));
        _registry.Received(1).DeleteValue(@"HKCU\Software\Test", "Value1");
    }

    [Test]
    public void Apply_MultipleEntries_ReportsEach()
    {
        _registry.GetValue(@"HKCU\Software\Test", "A").Returns(0);
        _registry.GetValue(@"HKCU\Software\Test", "B").Returns(0);

        var tweak = MakeTweak(
            new RegistryEntryDefinition(@"HKCU\Software\Test", "A", 1, RegistryValueType.DWord),
            new RegistryEntryDefinition(@"HKCU\Software\Test", "B", 2, RegistryValueType.DWord));

        var result = _service.Apply(tweak);

        Assert.That(result.Entries, Has.Length.EqualTo(2));
        _registry.Received(1).SetValue(@"HKCU\Software\Test", "A", 1, RegistryValueType.DWord);
        _registry.Received(1).SetValue(@"HKCU\Software\Test", "B", 2, RegistryValueType.DWord);
    }

    private static TweakCatalogEntry MakeTweak(params RegistryEntryDefinition[] entries) =>
        new("test-tweak", "Test Tweak", "Test", [], null, true, [],
            entries.ToImmutableArray());
}
