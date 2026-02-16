namespace Perch.Core.Scanner;

public sealed record DetectedDotfile(string Name, string FullPath, string Group, long SizeBytes, DateTime LastModified, bool IsSymlink);
