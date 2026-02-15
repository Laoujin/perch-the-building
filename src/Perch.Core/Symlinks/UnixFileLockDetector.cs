namespace Perch.Core.Symlinks;

public sealed class UnixFileLockDetector : IFileLockDetector
{
    public bool IsLocked(string path) => false;
}
