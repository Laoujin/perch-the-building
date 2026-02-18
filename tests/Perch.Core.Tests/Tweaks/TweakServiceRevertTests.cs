using System.Collections.Immutable;

using NSubstitute;

using Perch.Core.Catalog;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Tweaks;

namespace Perch.Core.Tests.Tweaks;

[TestFixture]
public sealed class TweakServiceRevertTests
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
    public void Revert_RestoresToDefaultValues()
    {
        var tweak = MakeTweak(true,
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 0, RegistryValueType.DWord, 1));

        var result = _service.Revert(tweak);

        Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
        _registry.Received(1).SetValue(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord);
        Assert.That(result.Entries[0].Message, Does.Contain("Restored"));
    }

    [Test]
    public void Revert_DeletesEntriesWithoutDefaultValue()
    {
        var tweak = MakeTweak(true,
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", "created", RegistryValueType.String));

        var result = _service.Revert(tweak);

        Assert.That(result.Level, Is.EqualTo(ResultLevel.Ok));
        _registry.Received(1).DeleteValue(@"HKCU\Software\Test", "Value1");
        Assert.That(result.Entries[0].Message, Does.Contain("Deleted"));
    }

    [Test]
    public void Revert_NonReversible_ReturnsError()
    {
        var tweak = MakeTweak(false,
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 1, RegistryValueType.DWord));

        var result = _service.Revert(tweak);

        Assert.Multiple(() =>
        {
            Assert.That(result.Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(result.Entries[0].Message, Does.Contain("not reversible"));
        });
        _registry.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<RegistryValueType>());
        _registry.DidNotReceive().DeleteValue(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public void Revert_DryRun_DoesNotWrite()
    {
        var tweak = MakeTweak(true,
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", 0, RegistryValueType.DWord, 1));

        var result = _service.Revert(tweak, dryRun: true);

        Assert.That(result.Entries[0].Message, Does.Contain("Would restore"));
        _registry.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<RegistryValueType>());
    }

    [Test]
    public void Revert_DryRun_WithoutDefaultValue_ReportsDelete()
    {
        var tweak = MakeTweak(true,
            new RegistryEntryDefinition(@"HKCU\Software\Test", "Value1", "val", RegistryValueType.String));

        var result = _service.Revert(tweak, dryRun: true);

        Assert.That(result.Entries[0].Message, Does.Contain("Would delete"));
        _registry.DidNotReceive().DeleteValue(Arg.Any<string>(), Arg.Any<string>());
    }

    private static TweakCatalogEntry MakeTweak(bool reversible, params RegistryEntryDefinition[] entries) =>
        new("test-tweak", "Test Tweak", "Test", [], null, reversible, [],
            entries.ToImmutableArray());
}
