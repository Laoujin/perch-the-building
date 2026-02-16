namespace Perch.Core.Templates;

public sealed class MachineVariableResolver : IVariableResolver
{
    public string? Resolve(string variableName, IReadOnlyDictionary<string, string>? variables)
    {
        if (variables != null && variables.TryGetValue(variableName, out var value))
        {
            return value;
        }

        return variableName switch
        {
            "machine.name" => Environment.MachineName,
            "platform" => GetCurrentPlatform(),
            "date" => DateTime.UtcNow.ToString("yyyy-MM-dd"),
            _ => null,
        };
    }

    private static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }
}
