namespace Perch.Core.EnvPath;

public interface IPathService
{
    bool Contains(string path);
    bool Add(string path, bool dryRun = false);
}
