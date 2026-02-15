using Perch.Core;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class PackageManagerExtensionsTests
{
    [TestCase(PackageManager.Chocolatey, Platform.Windows, true)]
    [TestCase(PackageManager.Chocolatey, Platform.Linux, false)]
    [TestCase(PackageManager.Chocolatey, Platform.MacOS, false)]
    [TestCase(PackageManager.Winget, Platform.Windows, true)]
    [TestCase(PackageManager.Winget, Platform.Linux, false)]
    [TestCase(PackageManager.Winget, Platform.MacOS, false)]
    [TestCase(PackageManager.Apt, Platform.Linux, true)]
    [TestCase(PackageManager.Apt, Platform.Windows, false)]
    [TestCase(PackageManager.Apt, Platform.MacOS, false)]
    [TestCase(PackageManager.Brew, Platform.MacOS, true)]
    [TestCase(PackageManager.Brew, Platform.Windows, false)]
    [TestCase(PackageManager.Brew, Platform.Linux, false)]
    [TestCase(PackageManager.Npm, Platform.Windows, false)]
    [TestCase(PackageManager.Npm, Platform.Linux, false)]
    [TestCase(PackageManager.VsCode, Platform.Windows, false)]
    public void IsPlatformMatch_ReturnsExpected(PackageManager manager, Platform platform, bool expected)
    {
        Assert.That(manager.IsPlatformMatch(platform), Is.EqualTo(expected));
    }
}
