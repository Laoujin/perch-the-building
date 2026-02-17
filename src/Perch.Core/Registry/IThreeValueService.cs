using System.Collections.Immutable;

using Perch.Core.Modules;

namespace Perch.Core.Registry;

public interface IThreeValueService
{
    ImmutableArray<RegistryThreeValueResult> Evaluate(ImmutableArray<RegistryEntryDefinition> entries);
}
