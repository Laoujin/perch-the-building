namespace Perch.Core.Symlinks;

public sealed class UnixSymlinkProvider : ISymlinkProvider
{
    public void CreateSymlink(string linkPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        else if (Directory.Exists(targetPath))
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        else
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
    }

    public void CreateJunction(string linkPath, string targetPath)
    {
        Directory.CreateSymbolicLink(linkPath, targetPath);
    }

    public bool IsSymlink(string path)
    {
        var info = new FileInfo(path);
        if (info.Exists)
        {
            return info.LinkTarget != null;
        }

        var dirInfo = new DirectoryInfo(path);
        return dirInfo.Exists && dirInfo.LinkTarget != null;
    }

    public string? GetSymlinkTarget(string path)
    {
        var info = new FileInfo(path);
        if (info.Exists)
        {
            return info.LinkTarget;
        }

        var dirInfo = new DirectoryInfo(path);
        return dirInfo.Exists ? dirInfo.LinkTarget : null;
    }
}
