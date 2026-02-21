using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
[Category("Integration")]
public sealed class InstalledAppCheckerIntegrationTests
{
    [Test]
    [Explicit("Integration test that requires winget")]
    public async Task GetInstalledPackageIdsAsync_IncludesWingetIds()
    {
        var processRunner = new DefaultProcessRunner();
        var wingetProvider = new WingetPackageManagerProvider(processRunner);
        var checker = new InstalledAppChecker(new[] { wingetProvider });

        IReadOnlySet<string> installedIds = await checker.GetInstalledPackageIdsAsync();

        Console.WriteLine($"Found {installedIds.Count} installed packages");

        // Check for some known packages that should be installed
        string[] expectedPackages = ["Oven-sh.Bun", "Notepad++.Notepad++", "VideoLAN.VLC", "Microsoft.VisualStudioCode"];
        foreach (string pkg in expectedPackages)
        {
            Console.WriteLine($"  {pkg}: {installedIds.Contains(pkg)}");
        }
    }
}
