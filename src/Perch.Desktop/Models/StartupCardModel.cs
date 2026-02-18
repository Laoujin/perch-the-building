using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Startup;

namespace Perch.Desktop.Models;

public partial class StartupCardModel : ObservableObject
{
    public StartupEntry Entry { get; }
    public string Name => Entry.Name;
    public string Command => Entry.Command;
    public string SourceLabel { get; }

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

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Command.Contains(query, StringComparison.OrdinalIgnoreCase)
            || SourceLabel.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
