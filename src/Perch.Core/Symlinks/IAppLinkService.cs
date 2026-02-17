using Perch.Core.Catalog;
using Perch.Core.Deploy;

namespace Perch.Core.Symlinks;

public interface IAppLinkService
{
    Task<IReadOnlyList<DeployResult>> LinkAppAsync(CatalogEntry app, CancellationToken ct = default);
    Task<IReadOnlyList<DeployResult>> UnlinkAppAsync(CatalogEntry app, CancellationToken ct = default);
    Task<IReadOnlyList<DeployResult>> FixAppLinksAsync(CatalogEntry app, CancellationToken ct = default);
}
