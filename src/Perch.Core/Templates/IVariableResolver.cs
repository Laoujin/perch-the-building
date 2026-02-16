namespace Perch.Core.Templates;

public interface IVariableResolver
{
    string? Resolve(string variableName, IReadOnlyDictionary<string, string>? variables);
}
