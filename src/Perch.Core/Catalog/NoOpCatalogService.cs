using System.Collections.Immutable;

namespace Perch.Core.Catalog;

public sealed class NoOpCatalogService : ICatalogService
{
    public Task<CatalogIndex> GetIndexAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CatalogIndex(
            ImmutableArray<CatalogIndexEntry>.Empty,
            ImmutableArray<CatalogIndexEntry>.Empty,
            ImmutableArray<CatalogIndexEntry>.Empty));

    public Task<CatalogEntry?> GetAppAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<CatalogEntry?>(null);

    public Task<FontCatalogEntry?> GetFontAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<FontCatalogEntry?>(null);

    public Task<TweakCatalogEntry?> GetTweakAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<TweakCatalogEntry?>(null);

    public Task<ImmutableArray<CatalogEntry>> GetAllAppsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ImmutableArray<CatalogEntry>.Empty);

    public Task<ImmutableArray<FontCatalogEntry>> GetAllFontsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ImmutableArray<FontCatalogEntry>.Empty);

    public Task<ImmutableArray<TweakCatalogEntry>> GetAllTweaksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ImmutableArray<TweakCatalogEntry>.Empty);

    public Task<ImmutableArray<CatalogEntry>> GetAllDotfileAppsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ImmutableArray<CatalogEntry>.Empty);

    public Task<ImmutableArray<TweakCatalogEntry>> GetAllAppOwnedTweaksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ImmutableArray<TweakCatalogEntry>.Empty);

    public Task<IReadOnlyDictionary<string, int>> GetGitHubStarsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
}
