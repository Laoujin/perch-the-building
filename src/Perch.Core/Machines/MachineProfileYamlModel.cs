using Perch.Core.Registry;

namespace Perch.Core.Machines;

internal sealed class MachineProfileYamlModel
{
    public List<string>? IncludeModules { get; set; }
    public List<string>? ExcludeModules { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
    public Dictionary<string, CapturedRegistryEntry>? CapturedRegistry { get; set; }
}
