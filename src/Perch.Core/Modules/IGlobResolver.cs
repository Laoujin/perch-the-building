namespace Perch.Core.Modules;

public interface IGlobResolver
{
    IReadOnlyList<string> Resolve(string path);
}
