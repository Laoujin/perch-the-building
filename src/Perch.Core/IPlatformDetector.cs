namespace Perch.Core;

public interface IPlatformDetector
{
    Platform CurrentPlatform { get; }
}
