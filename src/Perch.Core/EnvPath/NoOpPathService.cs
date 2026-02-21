namespace Perch.Core.EnvPath;

public sealed class NoOpPathService : IPathService
{
    public bool Contains(string path) => true;

    public bool Add(string path, bool dryRun = false) => false;
}
