namespace Perch.Core;

public sealed class PlatformDetector : IPlatformDetector
{
    public Platform CurrentPlatform =>
        OperatingSystem.IsWindows() ? Platform.Windows :
        OperatingSystem.IsMacOS() ? Platform.MacOS :
        Platform.Linux;
}
