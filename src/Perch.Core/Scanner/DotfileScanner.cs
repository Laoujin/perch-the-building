using System.Collections.Immutable;

namespace Perch.Core.Scanner;

public sealed class DotfileScanner : IDotfileScanner
{
    private static readonly (string Name, string RelativePath, string Group)[] KnownDotfiles =
    [
        (".gitconfig", ".gitconfig", "git"),
        (".gitignore_global", ".gitignore_global", "git"),
        (".npmrc", ".npmrc", "Node.js"),
        (".vimrc", ".vimrc", "Editors"),
        (".bashrc", ".bashrc", "Shell"),
        (".zshrc", ".zshrc", "Shell"),
        (".wslconfig", ".wslconfig", "WSL"),
    ];

    private static readonly (string Name, string ExpandedPath, string Group)[] KnownConfigFiles =
    [
        ("PowerShell profile", "%USERPROFILE%/Documents/PowerShell/Microsoft.PowerShell_profile.ps1", "Shell"),
        ("Windows Terminal settings", "%LOCALAPPDATA%/Packages/Microsoft.WindowsTerminal_8wekyb3d8bbwe/LocalState/settings.json", "Shell"),
        ("VS Code settings.json", "%APPDATA%/Code/User/settings.json", "vscode"),
        ("VS Code keybindings.json", "%APPDATA%/Code/User/keybindings.json", "vscode"),
    ];

    public Task<ImmutableArray<DetectedDotfile>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectedDotfile>();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var (name, relativePath, group) in KnownDotfiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.Combine(home, relativePath);
            if (File.Exists(fullPath))
            {
                var info = new FileInfo(fullPath);
                bool isSymlink = info.LinkTarget != null;
                results.Add(new DetectedDotfile(name, fullPath, group, info.Length, info.LastWriteTimeUtc, isSymlink));
            }
            else
            {
                results.Add(new DetectedDotfile(name, fullPath, group, 0, default, false, Exists: false));
            }
        }

        foreach (var (name, expandedPath, group) in KnownConfigFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Environment.ExpandEnvironmentVariables(expandedPath);
            if (File.Exists(fullPath))
            {
                var info = new FileInfo(fullPath);
                bool isSymlink = info.LinkTarget != null;
                results.Add(new DetectedDotfile(name, fullPath, group, info.Length, info.LastWriteTimeUtc, isSymlink));
            }
            else
            {
                results.Add(new DetectedDotfile(name, fullPath, group, 0, default, false, Exists: false));
            }
        }

        return Task.FromResult(results.ToImmutableArray());
    }
}
