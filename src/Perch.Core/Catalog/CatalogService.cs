using System.Collections.Immutable;

namespace Perch.Core.Catalog;

public sealed class CatalogService : ICatalogService
{
    private readonly ICatalogFetcher _fetcher;
    private readonly ICatalogCache _cache;
    private readonly CatalogParser _parser;

    private ImmutableArray<CatalogEntry>? _allApps;
    private ImmutableArray<FontCatalogEntry>? _allFonts;
    private ImmutableArray<TweakCatalogEntry>? _allTweaks;
    private IReadOnlyDictionary<string, int>? _gitHubStars;

    public CatalogService(ICatalogFetcher fetcher, ICatalogCache cache, CatalogParser parser)
    {
        _fetcher = fetcher;
        _cache = cache;
        _parser = parser;
    }

    public async Task<CatalogIndex> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        string content = await FetchWithCacheAsync("index.yaml", cancellationToken).ConfigureAwait(false);
        var result = _parser.ParseIndex(content);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to parse catalog index: {result.Error}");
        }

        return result.Value!;
    }

    public async Task<CatalogEntry?> GetAppAsync(string id, CancellationToken cancellationToken = default)
    {
        string path = await ResolvePathAsync("apps", id, cancellationToken).ConfigureAwait(false);
        string content = await FetchWithCacheAsync(path, cancellationToken).ConfigureAwait(false);
        var result = _parser.ParseApp(content, id);
        return result.Value;
    }

    public async Task<FontCatalogEntry?> GetFontAsync(string id, CancellationToken cancellationToken = default)
    {
        string content = await FetchWithCacheAsync($"fonts/{id}.yaml", cancellationToken).ConfigureAwait(false);
        var result = _parser.ParseFont(content, id);
        return result.Value;
    }

    public async Task<TweakCatalogEntry?> GetTweakAsync(string id, CancellationToken cancellationToken = default)
    {
        string path = await ResolvePathAsync("tweaks", id, cancellationToken).ConfigureAwait(false);
        string content = await FetchWithCacheAsync(path, cancellationToken).ConfigureAwait(false);
        var result = _parser.ParseTweak(content, id);
        return result.Value;
    }

    public async Task<ImmutableArray<CatalogEntry>> GetAllAppsAsync(CancellationToken cancellationToken = default)
    {
        if (_allApps.HasValue)
            return _allApps.Value;

        var index = await GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var apps = new List<CatalogEntry>();
        foreach (var entry in index.Apps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fetchPath = entry.Path ?? $"apps/{entry.Id}.yaml";
            string content = await FetchWithCacheAsync(fetchPath, cancellationToken).ConfigureAwait(false);
            var parsed = _parser.ParseApp(content, entry.Id);
            if (parsed.Value != null)
            {
                apps.Add(parsed.Value);
            }
        }

        var result = apps.ToImmutableArray();
        _allApps = result;
        return result;
    }

    public async Task<ImmutableArray<FontCatalogEntry>> GetAllFontsAsync(CancellationToken cancellationToken = default)
    {
        if (_allFonts.HasValue)
            return _allFonts.Value;

        var index = await GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var fonts = new List<FontCatalogEntry>();
        foreach (var entry in index.Fonts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fetchPath = entry.Path ?? $"fonts/{entry.Id}.yaml";
            string content = await FetchWithCacheAsync(fetchPath, cancellationToken).ConfigureAwait(false);
            var parsed = _parser.ParseFont(content, entry.Id);
            if (parsed.Value != null)
            {
                fonts.Add(parsed.Value);
            }
        }

        var result = fonts.ToImmutableArray();
        _allFonts = result;
        return result;
    }

    public async Task<ImmutableArray<TweakCatalogEntry>> GetAllTweaksAsync(CancellationToken cancellationToken = default)
    {
        if (_allTweaks.HasValue)
            return _allTweaks.Value;

        var index = await GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var tweaks = new List<TweakCatalogEntry>();
        foreach (var entry in index.Tweaks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fetchPath = entry.Path ?? $"tweaks/{entry.Id}.yaml";
            string content = await FetchWithCacheAsync(fetchPath, cancellationToken).ConfigureAwait(false);
            var parsed = _parser.ParseTweak(content, entry.Id);
            if (parsed.Value != null)
            {
                tweaks.Add(parsed.Value);
            }
        }

        var result = tweaks.ToImmutableArray();
        _allTweaks = result;
        return result;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetGitHubStarsAsync(CancellationToken cancellationToken = default)
    {
        if (_gitHubStars is not null)
            return _gitHubStars;

        string content = await FetchWithCacheAsync("metadata/github-stars.yaml", cancellationToken).ConfigureAwait(false);
        var result = _parser.ParseGitHubStars(content);
        _gitHubStars = result;
        return result;
    }

    public async Task<ImmutableArray<CatalogEntry>> GetAllDotfileAppsAsync(CancellationToken cancellationToken = default)
    {
        var allApps = await GetAllAppsAsync(cancellationToken).ConfigureAwait(false);
        return allApps
            .Where(a => a.Kind == CatalogKind.Dotfile)
            .ToImmutableArray();
    }

    public async Task<ImmutableArray<TweakCatalogEntry>> GetAllAppOwnedTweaksAsync(CancellationToken cancellationToken = default)
    {
        var allApps = await GetAllAppsAsync(cancellationToken).ConfigureAwait(false);
        return allApps
            .Where(a => !a.Tweaks.IsDefaultOrEmpty)
            .SelectMany(app => app.Tweaks.Select(t => t.ToTweakCatalogEntry(app)))
            .ToImmutableArray();
    }

    private async Task<string> ResolvePathAsync(string type, string id, CancellationToken cancellationToken)
    {
        var index = await GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var entries = type switch
        {
            "apps" => index.Apps,
            "fonts" => index.Fonts,
            "tweaks" => index.Tweaks,
            _ => ImmutableArray<CatalogIndexEntry>.Empty,
        };

        var entry = entries.FirstOrDefault(e => e.Id == id);
        return entry?.Path ?? $"{type}/{id}.yaml";
    }

    private async Task<string> FetchWithCacheAsync(string path, CancellationToken cancellationToken)
    {
        string? cached = await _cache.GetAsync(path, cancellationToken).ConfigureAwait(false);
        if (cached != null)
        {
            return cached;
        }

        string content = await _fetcher.FetchAsync(path, cancellationToken).ConfigureAwait(false);
        await _cache.SetAsync(path, content, cancellationToken).ConfigureAwait(false);
        return content;
    }
}
