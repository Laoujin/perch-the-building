namespace Perch.Core.Packages;

public static class PackageManagerExtensions
{
    public static bool IsPlatformMatch(this PackageManager manager, Platform platform) =>
        manager switch
        {
            PackageManager.Chocolatey or PackageManager.Winget => platform == Platform.Windows,
            PackageManager.Apt => platform == Platform.Linux,
            PackageManager.Brew => platform == Platform.MacOS,
            _ => false,
        };
}
