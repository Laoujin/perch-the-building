using Perch.Core.Catalog;

namespace Perch.Core.Tweaks;

public interface ITweakService
{
    TweakDetectionResult Detect(TweakCatalogEntry tweak);
    TweakOperationResult Apply(TweakCatalogEntry tweak, bool dryRun = false);
    TweakOperationResult Revert(TweakCatalogEntry tweak, bool dryRun = false);
}
