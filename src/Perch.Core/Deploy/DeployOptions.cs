using Perch.Core.Modules;

namespace Perch.Core.Deploy;

public sealed record DeployOptions
{
    public bool DryRun { get; init; }
    public IProgress<DeployResult>? Progress { get; init; }
    public Func<AppModule, IReadOnlyList<DeployResult>, Task<ModuleAction>>? BeforeModule { get; init; }
}
