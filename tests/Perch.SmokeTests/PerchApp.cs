using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace Perch.SmokeTests;

public sealed class PerchApp : IDisposable
{
    private static readonly string SolutionRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string DesktopProject = Path.Combine(
        SolutionRoot, "src", "Perch.Desktop", "Perch.Desktop.csproj");

    private static readonly string ExePath = Path.Combine(
        SolutionRoot, "src", "Perch.Desktop", "bin", "Debug", "net10.0-windows", "Perch.Desktop.exe");

    public static string ScreenshotDir { get; } = Path.Combine(SolutionRoot, "tests", "Perch.SmokeTests", "screenshots");

    private Application? _app;
    private UIA3Automation? _automation;

    public Window MainWindow => _app!.GetMainWindow(_automation!)
        ?? throw new InvalidOperationException("Main window not found");

    public UIA3Automation Automation => _automation!;

    public static void Build()
    {
        var result = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{DesktopProject}\" -c Debug",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        });
        result!.WaitForExit(TimeSpan.FromMinutes(2));
        if (result.ExitCode != 0)
        {
            var stderr = result.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Build failed:\n{stderr}");
        }
    }

    public void Launch()
    {
        _automation = new UIA3Automation();
        _app = Application.Launch(ExePath);
        _app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(15));
        Retry.WhileException(() => MainWindow, TimeSpan.FromSeconds(10));
        BringToFront();
        Thread.Sleep(1000);
    }

    public void Close()
    {
        _app?.Close();
        _app?.Dispose();
        _app = null;
    }

    public void Dispose()
    {
        Close();
        _automation?.Dispose();
        _automation = null;
    }

    public void BringToFront()
    {
        var hwnd = MainWindow.Properties.NativeWindowHandle.Value;
        SetForegroundWindow(hwnd);
    }

    public void NavigateTo(string pageName)
    {
        var descendants = MainWindow.FindAllDescendants();
        var target = descendants.FirstOrDefault(e =>
            e.Name == pageName && e.ControlType == ControlType.ListItem);

        // WPF-UI may expose nav items as different control types
        target ??= descendants.FirstOrDefault(e => e.Name == pageName);

        if (target == null)
        {
            var names = descendants
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .Select(e => $"{e.Name} ({e.ControlType})")
                .Distinct()
                .Take(30);
            throw new InvalidOperationException(
                $"Nav item '{pageName}' not found. Elements: {string.Join(", ", names)}");
        }

        target.Click();
        Thread.Sleep(500);
    }

    public string Screenshot(string name)
    {
        Directory.CreateDirectory(ScreenshotDir);
        var path = Path.Combine(ScreenshotDir, $"{name}.png");
        var image = Capture.MainScreen();
        image.ToFile(path);
        return path;
    }

    public string ScreenshotWindow(string name)
    {
        BringToFront();
        Thread.Sleep(200);
        Directory.CreateDirectory(ScreenshotDir);
        var path = Path.Combine(ScreenshotDir, $"{name}.png");
        var image = Capture.Element(MainWindow);
        image.ToFile(path);
        return path;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);
}
