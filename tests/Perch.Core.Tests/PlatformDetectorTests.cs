using Perch.Core;

namespace Perch.Core.Tests;

[TestFixture]
public sealed class PlatformDetectorTests
{
    [Test]
    public void CurrentPlatform_ReturnsValidPlatform()
    {
        var detector = new PlatformDetector();

        Platform result = detector.CurrentPlatform;

        Assert.That(result, Is.AnyOf(Platform.Windows, Platform.Linux, Platform.MacOS));
    }
}
