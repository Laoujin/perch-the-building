namespace Perch.Core.Symlinks;

public sealed class WindowsFileLockDetector : IFileLockDetector
{
    public bool IsLocked(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
