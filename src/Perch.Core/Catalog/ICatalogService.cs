using System.Collections.Immutable;

namespace Perch.Core.Catalog;

public interface ICatalogService
{
    Task<CatalogIndex> GetIndexAsync(CancellationToken cancellationToken = default);
    Task<CatalogEntry?> GetAppAsync(string id, CancellationToken cancellationToken = default);
    Task<FontCatalogEntry?> GetFontAsync(string id, CancellationToken cancellationToken = default);
    Task<TweakCatalogEntry?> GetTweakAsync(string id, CancellationToken cancellationToken = default);
    Task<ImmutableArray<CatalogEntry>> GetAllAppsAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<FontCatalogEntry>> GetAllFontsAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<TweakCatalogEntry>> GetAllTweaksAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<CatalogEntry>> GetAllDotfileAppsAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<TweakCatalogEntry>> GetAllAppOwnedTweaksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> GetGitHubStarsAsync(CancellationToken cancellationToken = default);
}
