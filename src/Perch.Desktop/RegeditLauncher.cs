using System.Diagnostics;

using Microsoft.Win32;

namespace Perch.Desktop;

internal static class RegeditLauncher
{
    internal static void OpenAt(string registryKeyPath)
    {
        using var applets = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
        applets.SetValue("LastKey", registryKeyPath);

        bool needsElevation = registryKeyPath.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase);
        var psi = new ProcessStartInfo("regedit.exe") { UseShellExecute = true };
        if (needsElevation)
        {
            psi.Verb = "runas";
        }

        try
        {
            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User declined UAC prompt
        }
    }
}
