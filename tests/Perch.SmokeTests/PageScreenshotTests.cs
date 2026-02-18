using System.IO;
using System.Threading;
using NUnit.Framework;

namespace Perch.SmokeTests;

[TestFixture]
[Platform("Win")]
[NonParallelizable]
public sealed class PageScreenshotTests
{
    private PerchApp _perch = null!;

    [OneTimeSetUp]
    public void BuildAndLaunch()
    {
        PerchApp.Build();
        _perch = new PerchApp();
        _perch.Launch();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _perch?.Dispose();
    }

    [Test, Order(1)]
    public void Home_PageLoads()
    {
        var path = _perch.ScreenshotWindow("01-home");
        Assert.That(File.Exists(path), Is.True, $"Screenshot saved to {path}");
    }

    [Test, Order(2)]
    public void Dotfiles_PageLoads()
    {
        _perch.NavigateTo("Dotfiles");
        Thread.Sleep(1000);
        var path = _perch.ScreenshotWindow("02-dotfiles");
        Assert.That(File.Exists(path), Is.True, $"Screenshot saved to {path}");
    }

    [Test, Order(3)]
    public void Apps_PageLoads()
    {
        _perch.NavigateTo("Apps");
        Thread.Sleep(2000);
        var path = _perch.ScreenshotWindow("03-apps");
        Assert.That(File.Exists(path), Is.True, $"Screenshot saved to {path}");
    }

    [Test, Order(4)]
    public void WindowsTweaks_PageLoads()
    {
        _perch.NavigateTo("Windows Tweaks");
        Thread.Sleep(1000);
        var path = _perch.ScreenshotWindow("04-tweaks");
        Assert.That(File.Exists(path), Is.True, $"Screenshot saved to {path}");
    }

    [Test, Order(5)]
    public void Settings_PageLoads()
    {
        _perch.NavigateTo("Settings");
        Thread.Sleep(500);
        var path = _perch.ScreenshotWindow("05-settings");
        Assert.That(File.Exists(path), Is.True, $"Screenshot saved to {path}");
    }
}
