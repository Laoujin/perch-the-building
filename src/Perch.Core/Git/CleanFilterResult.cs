using Perch.Core.Deploy;

namespace Perch.Core.Git;

public sealed record CleanFilterResult(string ModuleName, ResultLevel Level, string Message);
