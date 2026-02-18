using System.Diagnostics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Startup;

namespace Perch.Desktop.Models;

public partial class StartupCardModel : ObservableObject
{
    public StartupEntry Entry { get; }
    public string Name => Entry.Name;
    public string Command => Entry.Command;
    public string SourceLabel { get; }
    public bool IsRegistrySource => Entry.Source is StartupSource.RegistryCurrentUser or StartupSource.RegistryLocalMachine;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isTracked;

    public StartupCardModel(StartupEntry entry)
    {
        Entry = entry;
        IsEnabled = entry.IsEnabled;
        SourceLabel = entry.Source switch
        {
            StartupSource.RegistryCurrentUser => "Registry (User)",
            StartupSource.RegistryLocalMachine => "Registry (Machine)",
            StartupSource.StartupFolderUser => "Startup Folder (User)",
            StartupSource.StartupFolderAllUsers => "Startup Folder (All Users)",
            _ => entry.Source.ToString(),
        };
    }

    [RelayCommand]
    private void OpenSource()
    {
        switch (Entry.Source)
        {
            case StartupSource.StartupFolderUser:
                var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{userStartup}\"", UseShellExecute = true });
                break;
            case StartupSource.StartupFolderAllUsers:
                var allStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{allStartup}\"", UseShellExecute = true });
                break;
            case StartupSource.RegistryCurrentUser:
            case StartupSource.RegistryLocalMachine:
                var key = Entry.Source == StartupSource.RegistryCurrentUser
                    ? @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
                    : @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                RegeditLauncher.OpenAt(key);
                break;
        }
    }

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Command.Contains(query, StringComparison.OrdinalIgnoreCase)
            || SourceLabel.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
