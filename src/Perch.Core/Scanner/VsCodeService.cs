using System.Collections.Immutable;

using Perch.Core.Packages;

namespace Perch.Core.Scanner;

public class VsCodeService : IVsCodeService
{
    private readonly IProcessRunner _processRunner;

    public VsCodeService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public bool IsInstalled => FindCodePath() != null;

    public async Task<ImmutableArray<DetectedVsCodeExtension>> GetInstalledExtensionsAsync(CancellationToken cancellationToken = default)
    {
        string? codePath = FindCodePath();
        if (codePath == null)
        {
            return ImmutableArray<DetectedVsCodeExtension>.Empty;
        }

        var result = await _processRunner.RunAsync(codePath, "--list-extensions --show-versions", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return ImmutableArray<DetectedVsCodeExtension>.Empty;
        }

        var extensions = new List<DetectedVsCodeExtension>();
        foreach (string line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int atIndex = line.LastIndexOf('@');
            if (atIndex > 0)
            {
                string id = line[..atIndex];
                string version = line[(atIndex + 1)..];
                extensions.Add(new DetectedVsCodeExtension(id, null, version));
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                extensions.Add(new DetectedVsCodeExtension(line.Trim(), null, null));
            }
        }

        return extensions.ToImmutableArray();
    }

    protected virtual string? FindCodePath()
    {
        if (OperatingSystem.IsWindows())
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string codePath = Path.Combine(localAppData, "Programs", "Microsoft VS Code", "bin", "code.cmd");
            if (File.Exists(codePath))
            {
                return codePath;
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            codePath = Path.Combine(programFiles, "Microsoft VS Code", "bin", "code.cmd");
            if (File.Exists(codePath))
            {
                return codePath;
            }
        }

        string[] pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        string codeName = OperatingSystem.IsWindows() ? "code.cmd" : "code";
        foreach (string dir in pathDirs)
        {
            string candidate = Path.Combine(dir, codeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
