using System.Runtime.Versioning;

namespace Perch.Core.EnvPath;

[SupportedOSPlatform("windows")]
public sealed class WindowsPathService : IPathService
{
    private const string EnvironmentKey = @"Environment";

    public bool Contains(string path)
    {
        string expandedPath = System.Environment.ExpandEnvironmentVariables(path);
        string? currentPath = GetUserPath();
        if (string.IsNullOrEmpty(currentPath))
        {
            return false;
        }

        string[] entries = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string entry in entries)
        {
            string expandedEntry = System.Environment.ExpandEnvironmentVariables(entry);
            if (string.Equals(expandedEntry, expandedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public bool Add(string path, bool dryRun = false)
    {
        if (Contains(path))
        {
            return false;
        }

        if (dryRun)
        {
            return true;
        }

        string? currentPath = GetUserPath();
        string newPath = string.IsNullOrEmpty(currentPath)
            ? path
            : $"{currentPath};{path}";

        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(EnvironmentKey, writable: true);
        key?.SetValue("Path", newPath, Microsoft.Win32.RegistryValueKind.ExpandString);

        // Broadcast environment change
        BroadcastEnvironmentChange();

        return true;
    }

    private static string? GetUserPath()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(EnvironmentKey);
        return key?.GetValue("Path", null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    private static void BroadcastEnvironmentChange()
    {
        // Notify other applications that environment variables have changed
        // This uses SendMessageTimeout with WM_SETTINGCHANGE
        nint HWND_BROADCAST = 0xffff;
        uint WM_SETTINGCHANGE = 0x001A;
        nint result;

        NativeMethods.SendMessageTimeout(
            HWND_BROADCAST,
            WM_SETTINGCHANGE,
            0,
            "Environment",
            NativeMethods.SMTO_ABORTIFHUNG,
            5000,
            out result);
    }

    private static class NativeMethods
    {
        public const uint SMTO_ABORTIFHUNG = 0x0002;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern nint SendMessageTimeout(
            nint hWnd,
            uint Msg,
            nint wParam,
            string lParam,
            uint fuFlags,
            uint uTimeout,
            out nint lpdwResult);
    }
}
