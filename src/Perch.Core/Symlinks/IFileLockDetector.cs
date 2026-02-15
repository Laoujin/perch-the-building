namespace Perch.Core.Symlinks;

public interface IFileLockDetector
{
    bool IsLocked(string path);
}
