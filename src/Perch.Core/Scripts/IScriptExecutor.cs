namespace Perch.Core.Scripts;

public interface IScriptExecutor
{
    Task<ScriptExecutionResult> ExecuteAsync(string script, CancellationToken cancellationToken = default);
}
