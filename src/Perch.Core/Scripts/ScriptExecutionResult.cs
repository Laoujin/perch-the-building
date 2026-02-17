namespace Perch.Core.Scripts;

public sealed record ScriptExecutionResult(bool Success, string Output, string? Error);
