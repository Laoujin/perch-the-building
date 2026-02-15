using System.Collections.Immutable;
using Perch.Core;
using Perch.Core.Modules;

namespace Perch.Core.Tests.Modules;

[TestFixture]
public sealed class LinkEntryTests
{
    [Test]
    public void GetTargetForPlatform_SimpleTarget_ReturnsTarget()
    {
        var entry = new LinkEntry("src", "target-path", LinkType.Symlink);

        string? result = entry.GetTargetForPlatform(Platform.Windows);

        Assert.That(result, Is.EqualTo("target-path"));
    }

    [Test]
    public void GetTargetForPlatform_PlatformMatch_ReturnsPlatformTarget()
    {
        var targets = ImmutableDictionary.CreateRange(new[]
        {
            KeyValuePair.Create(Platform.Windows, @"C:\Users\test\config"),
            KeyValuePair.Create(Platform.Linux, "/home/test/config"),
        });
        var entry = new LinkEntry("src", null, targets, LinkType.Symlink);

        Assert.Multiple(() =>
        {
            Assert.That(entry.GetTargetForPlatform(Platform.Windows), Is.EqualTo(@"C:\Users\test\config"));
            Assert.That(entry.GetTargetForPlatform(Platform.Linux), Is.EqualTo("/home/test/config"));
        });
    }

    [Test]
    public void GetTargetForPlatform_NoMatch_ReturnsNull()
    {
        var targets = ImmutableDictionary.CreateRange(new[]
        {
            KeyValuePair.Create(Platform.Linux, "/home/test/config"),
        });
        var entry = new LinkEntry("src", null, targets, LinkType.Symlink);

        string? result = entry.GetTargetForPlatform(Platform.MacOS);

        Assert.That(result, Is.Null);
    }
}
